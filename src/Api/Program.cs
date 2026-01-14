using Enterprise.Documentation.Core.Governance;
using Enterprise.Documentation.Core.Infrastructure.Extensions;
using Enterprise.Documentation.Core.Application.Interfaces;
using Enterprise.Documentation.Core.Application.Services.ExcelSync;
using Enterprise.Documentation.Core.Application.Services.DocumentGeneration;
using Enterprise.Documentation.Core.Application.Services.MasterIndex;
using Enterprise.Documentation.Core.Application.Services.Approval;
using Enterprise.Documentation.Core.Application.Services.Workflow;
using Enterprise.Documentation.Core.Application.Services.StoredProcedure;
using Enterprise.Documentation.Core.Application.Services.CodeExtraction;
using Enterprise.Documentation.Core.Application.Services.SqlAnalysis;
using Enterprise.Documentation.Core.Application.Services.Notifications;
using Enterprise.Documentation.Core.Application.Services.Quality;
using Enterprise.Documentation.Core.Application.Services.QueueProcessor;
using Enterprise.Documentation.Core.Application.Services.AI;
using Enterprise.Documentation.Api.Services;
using FluentValidation;
using System.Diagnostics;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<Enterprise.Documentation.Core.Application.Services.DocumentGeneration.IDocumentGenerationService,
    Enterprise.Documentation.Core.Application.Services.DocumentGeneration.DocumentGenerationService>();
builder.Services.AddScoped<Enterprise.Documentation.Core.Application.Services.Metadata.IMasterIndexPersistenceService,
    Enterprise.Documentation.Core.Application.Services.Metadata.MasterIndexPersistenceService>();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Enterprise Documentation Platform API", 
        Version = "v1",
        Description = "API for Enterprise Documentation Platform with Data Governance"
    });

    // Resolve conflicting method/path combinations
    c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());

    // Custom schema ID generator to handle naming conflicts
    c.CustomSchemaIds(type =>
    {
        if (type.FullName?.Contains("Core.Domain.ValueObjects") == true)
            return $"Domain{type.Name}";
        if (type.FullName?.Contains("Shared.Contracts.DTOs") == true)
            return $"Dto{type.Name}";
        return type.Name;
    });

    // Add JWT authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header,
            },
            new List<string>()
        }
    });
});

// Add Enterprise Data Governance Services (MANDATORY for V2)
builder.Services.AddScoped<IDataGovernanceProxy, DataGovernanceProxy>();
builder.Services.AddScoped<GovernanceSecurityEngine>();
builder.Services.AddScoped<GovernancePIIDetector>();
builder.Services.AddScoped<GovernanceAuditLogger>();
builder.Services.AddScoped<GovernanceAuthorizationEngine>();

// Add Azure Key Vault Integration Services
builder.Services.AddSingleton<ISecretManager, Enterprise.Documentation.Core.Infrastructure.Security.SecretManager>();
builder.Services.AddScoped<ISecureConnectionFactory, Enterprise.Documentation.Core.Infrastructure.Data.SecureConnectionFactory>();

// FluentValidation will be added when needed for request validation

// Configure OpenTelemetry for governance observability
builder.Services.AddSingleton<ActivitySource>(provider => 
    new ActivitySource("Enterprise.Documentation.Platform.V2"));

// Add logging configuration
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add HTTP Context Accessor for current user service
builder.Services.AddHttpContextAccessor();

// Add Application Services
builder.Services.AddScoped<ICurrentUserService, Enterprise.Documentation.Api.Services.CurrentUserService>();
builder.Services.AddScoped<IAuthorizationService, Enterprise.Documentation.Api.Services.SimpleAuthorizationService>();

// Add AutoMapper
builder.Services.AddAutoMapper(cfg => {
    cfg.AddProfile<Enterprise.Documentation.Core.Application.Mappings.MappingProfile>();
});

// Add FluentValidation - register all validators from governance assembly
builder.Services.AddValidatorsFromAssemblyContaining<GovernanceQueryRequest>();

// Add MediatR for CQRS
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(
    typeof(Enterprise.Documentation.Core.Application.Commands.Documents.CreateDocumentCommand).Assembly));

// Add Document Generation Services
builder.Services.AddHttpClient<IOpenAIEnhancementService, OpenAIEnhancementService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5); // 5 minute timeout for OpenAI calls
});
builder.Services.AddScoped<IDocIdGeneratorService, DocIdGeneratorService>();
builder.Services.AddScoped<IOpenAIEnhancementService, OpenAIEnhancementService>();
builder.Services.AddScoped<ITemplateExecutorService, TemplateExecutorService>();
builder.Services.AddScoped<IAutoDraftService, AutoDraftService>();

// Add Code Extraction Service (Step 3 of workflow)
builder.Services.AddScoped<ICodeExtractionService, CodeExtractionService>();

// Add SQL Analysis Service
builder.Services.AddScoped<Enterprise.Documentation.Core.Application.Services.SqlAnalysis.ISqlAnalysisService,
    Enterprise.Documentation.Core.Application.Services.SqlAnalysis.SqlAnalysisService>();

// Add Code Quality Audit Service (Step 4 of workflow)
builder.Services.AddScoped<IEnterpriseCodeQualityAuditService, EnterpriseCodeQualityAuditService>();

// Add Template Selection Service
builder.Services.AddScoped<Enterprise.Documentation.Core.Application.Services.DocumentGeneration.Templates.ITemplateSelector, 
    Enterprise.Documentation.Core.Application.Services.DocumentGeneration.Templates.BasicTemplateSelector>();

// Add Draft Generation Service (Step 5 of workflow)
builder.Services.AddScoped<Enterprise.Documentation.Core.Application.Services.DraftGeneration.IDraftGenerationService, 
    Enterprise.Documentation.Core.Application.Services.DraftGeneration.DraftGenerationService>();

// Add Metadata Extraction Service
builder.Services.AddScoped<Enterprise.Documentation.Core.Application.Services.Metadata.IMetadataExtractionService,
    Enterprise.Documentation.Core.Application.Services.Metadata.MetadataExtractionService>();

// Add AI-powered Metadata Service (Phase 2)
builder.Services.AddHttpClient<Enterprise.Documentation.Core.Application.Services.AI.IMetadataAIService,
    Enterprise.Documentation.Core.Application.Services.AI.MetadataAIService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(1); // AI calls timeout
});

// Add Metadata and Workflow Services
builder.Services.AddHttpClient<Enterprise.Documentation.Core.Application.Services.MasterIndex.IComprehensiveMasterIndexService,
    Enterprise.Documentation.Core.Application.Services.MasterIndex.ComprehensiveMasterIndexService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(2); // AI inference timeout
});
builder.Services.AddScoped<Enterprise.Documentation.Core.Application.Services.Approval.IApprovalTrackingService, 
    Enterprise.Documentation.Core.Application.Services.Approval.ApprovalTrackingService>();
builder.Services.AddScoped<Enterprise.Documentation.Core.Application.Services.Workflow.IWorkflowEventService, 
    Enterprise.Documentation.Core.Application.Services.Workflow.WorkflowEventService>();
builder.Services.AddScoped<Enterprise.Documentation.Core.Application.Services.StoredProcedure.IStoredProcedureDocumentationService, 
    Enterprise.Documentation.Core.Application.Services.StoredProcedure.StoredProcedureDocumentationService>();

// Add Notification Services (Steps 9-10)
builder.Services.AddHttpClient<Enterprise.Documentation.Core.Application.Services.Notifications.ITeamsNotificationService, 
    Enterprise.Documentation.Core.Application.Services.Notifications.TeamsNotificationService>();

// Add Infrastructure services (repositories, database)
builder.Services.AddPersistence(builder.Configuration);

// TODO [4]: Add Schema Change Detector services
// builder.Services.AddSchemaChangeDetector();

// Agent #5: Post-Approval Pipeline Services
builder.Services.AddScoped<Enterprise.Documentation.Core.Application.Services.PostApproval.IMetadataFinalizationService,
    Enterprise.Documentation.Core.Application.Services.PostApproval.MetadataFinalizationService>();
builder.Services.AddScoped<Enterprise.Documentation.Core.Application.Services.PostApproval.IMetadataStampingService,
    Enterprise.Documentation.Core.Application.Services.PostApproval.MetadataStampingService>();
builder.Services.AddScoped<Enterprise.Documentation.Core.Application.Services.PostApproval.IMasterIndexPopulationService,
    Enterprise.Documentation.Core.Application.Services.PostApproval.MasterIndexPopulationService>();
builder.Services.AddScoped<Enterprise.Documentation.Core.Application.Services.PostApproval.IColumnLineageService,
    Enterprise.Documentation.Core.Application.Services.PostApproval.ColumnLineageService>();
builder.Services.AddScoped<Enterprise.Documentation.Core.Application.Services.PostApproval.IPostApprovalOrchestrator,
    Enterprise.Documentation.Core.Application.Services.PostApproval.PostApprovalOrchestrator>();

// TODO [5]: Add SignalR for DocumentationHub (already registered below)

// TODO [6]: Add Smart Search services (Qdrant, GraphRAG, hybrid search)

// Agent #7: Gap Intelligence Services
builder.Services.AddScoped<Enterprise.Documentation.Core.Application.Services.GapIntelligence.IGapIntelligenceAgent,
    Enterprise.Documentation.Core.Application.Services.GapIntelligence.GapIntelligenceAgent>();
builder.Services.AddScoped<Enterprise.Documentation.Core.Application.Services.GapIntelligence.IQueryPatternMiner,
    Enterprise.Documentation.Core.Application.Services.GapIntelligence.QueryPatternMiner>();
builder.Services.AddScoped<Enterprise.Documentation.Core.Application.Services.GapIntelligence.ISemanticClusteringService,
    Enterprise.Documentation.Core.Application.Services.GapIntelligence.SemanticClusteringService>();
// TODO [7]: Add IGapNaturalLanguageService, IRLHFLearningEngine when implemented

// JWT Authentication Configuration
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? "your-super-secret-key-that-is-at-least-32-characters-long-for-development";
var issuer = jwtSettings["Issuer"] ?? "Enterprise.Documentation.Api";
var audience = jwtSettings["Audience"] ?? "Enterprise.Documentation.Client";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// Add Controllers and Razor Pages
builder.Services.AddControllers();
builder.Services.AddRazorPages();

// Add SignalR for real-time updates
builder.Services.AddSignalR();

// Add Excel Change Integrator Background Service (if configured)
if (!string.IsNullOrEmpty(builder.Configuration["ExcelChangeIntegrator:ExcelPath"]))
{
    // Register as both interface and hosted service
    builder.Services.AddSingleton<Enterprise.Documentation.Core.Application.Services.ExcelSync.ExcelChangeIntegratorService>();
    builder.Services.AddSingleton<Enterprise.Documentation.Core.Application.Services.ExcelSync.IExcelChangeIntegratorService>(
        provider => provider.GetRequiredService<Enterprise.Documentation.Core.Application.Services.ExcelSync.ExcelChangeIntegratorService>());
    builder.Services.AddHostedService<Enterprise.Documentation.Core.Application.Services.ExcelSync.ExcelChangeIntegratorService>(
        provider => provider.GetRequiredService<Enterprise.Documentation.Core.Application.Services.ExcelSync.ExcelChangeIntegratorService>());
}

// Add Document Change Watcher Background Service (if enabled)
var watcherEnabled = builder.Configuration["DocumentChangeWatcher:Enabled"];
if (string.IsNullOrEmpty(watcherEnabled) || bool.Parse(watcherEnabled))
{
    builder.Services.AddHostedService<Enterprise.Documentation.Core.Application.Services.Watcher.DocumentChangeWatcherService>();
}

// Add Doc Generator Queue Processor Background Service (if enabled)
var queueProcessorEnabled = builder.Configuration["DocGeneratorQueueProcessor:Enabled"];
if (string.IsNullOrEmpty(queueProcessorEnabled) || bool.Parse(queueProcessorEnabled))
{
    builder.Services.AddHostedService<DocGeneratorQueueProcessor>();
}

// Add CORS for dashboard access
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowDashboard", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Enable CORS for dashboard access
app.UseCors("AllowDashboard");

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Add governance middleware for all requests
app.Use(async (context, next) =>
{
    // Add correlation ID to all requests for governance tracking
    if (!context.Request.Headers.ContainsKey("X-Correlation-ID"))
    {
        context.Request.Headers["X-Correlation-ID"] = Guid.NewGuid().ToString();
    }
    
    // Add governance context to response headers
    context.Response.Headers["X-Governance-Protected"] = "true";
    context.Response.Headers["X-Platform-Version"] = "V2";
    
    await next();
});

// Add API Controllers and Razor Pages
app.MapControllers();
app.MapRazorPages();

// SignalR Hubs
app.MapHub<Enterprise.Documentation.Api.Hubs.ApprovalHub>("/hubs/approval");
app.MapHub<Enterprise.Documentation.Api.Hubs.DocumentationHub>("/hubs/documentation");
app.MapHub<Enterprise.Documentation.Api.Hubs.GapIntelligenceHub>("/hubs/gap-intelligence");
// TODO [4]: app.MapHub<Enterprise.Documentation.Api.Hubs.SchemaChangeHub>("/hubs/schema-changes");
// TODO [6]: app.MapHub for search suggestions

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

// Governance testing endpoint
app.MapPost("/governance/validate", async (
    GovernanceQueryRequest request, 
    IDataGovernanceProxy governance,
    HttpContext context) =>
{
    try
    {
        // Ensure correlation ID from headers
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() 
                           ?? Guid.NewGuid().ToString();
        
        // Update request with correlation ID
        var requestWithCorrelation = request with { CorrelationId = correlationId };
        
        // Validate the query through governance
        var validationResult = await governance.ValidateQueryAsync(requestWithCorrelation);
        
        return Results.Ok(new
        {
            IsValid = validationResult.IsValid,
            FailureReason = validationResult.FailureReason,
            SecurityRisks = validationResult.SecurityRisks,
            Warnings = validationResult.Warnings,
            Recommendations = validationResult.Recommendations,
            CorrelationId = correlationId,
            Timestamp = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new
        {
            Error = "Governance validation failed",
            Message = ex.Message,
            Timestamp = DateTime.UtcNow
        });
    }
})
.WithName("ValidateGovernanceQuery")
.WithOpenApi()
.WithSummary("Validates a query through the Data Governance Proxy")
.WithDescription("Tests the comprehensive governance validation including security, PII detection, and authorization");

// Governance authorization testing endpoint
app.MapPost("/governance/authorize", async (
    string agentId,
    string[] requestedTables,
    AgentClearanceLevel clearanceLevel,
    IDataGovernanceProxy governance) =>
{
    try
    {
        var authResult = await governance.AuthorizeAccessAsync(agentId, requestedTables, clearanceLevel);
        
        return Results.Ok(new
        {
            IsAuthorized = authResult.IsAuthorized,
            DenialReason = authResult.DenialReason,
            GrantedClearanceLevel = authResult.GrantedClearanceLevel,
            AuthorizedTables = authResult.AuthorizedTables,
            RateLimit = authResult.RateLimit,
            ExpiresAt = authResult.ExpiresAt,
            Timestamp = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new
        {
            Error = "Authorization failed",
            Message = ex.Message,
            Timestamp = DateTime.UtcNow
        });
    }
})
.WithName("AuthorizeGovernanceAccess")
.WithOpenApi()
.WithSummary("Tests agent authorization through the Data Governance Proxy")
.WithDescription("Validates agent permissions, clearance levels, and rate limiting");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

// Make Program class accessible to integration tests
public partial class Program { }

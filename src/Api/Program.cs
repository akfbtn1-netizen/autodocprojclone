using Enterprise.Documentation.Core.Governance;
using Enterprise.Documentation.Core.Infrastructure.Extensions;
using Enterprise.Documentation.Core.Application.Interfaces;
using Enterprise.Documentation.Core.Application.Services.ExcelSync;
using Enterprise.Documentation.Api.Services;
using FluentValidation;
using System.Diagnostics;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

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

// Add Infrastructure services (repositories, database)
builder.Services.AddPersistence(builder.Configuration);

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

// Add Controllers
builder.Services.AddControllers();

// Add Excel-to-SQL Sync Background Service (if configured)
if (!string.IsNullOrEmpty(builder.Configuration["ExcelSync:LocalFilePath"]))
{
    builder.Services.AddHostedService<ExcelToSqlSyncService>();
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

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

// Add API Controllers
app.MapControllers();

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

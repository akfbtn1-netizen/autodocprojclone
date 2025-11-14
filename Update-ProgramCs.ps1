# ============================================================================
# Update Program.cs with Security & Performance Fixes
# ============================================================================
# This script replaces src/Api/Program.cs with the updated version containing:
# - JWT secret validation (from environment/user secrets)
# - CORS with specific origins
# - Rate limiting (global + auth endpoint)
# - Response compression (Gzip + Brotli)
# - Memory cache
# - Global exception handler
# - Security headers middleware
# - HSTS for production
# ============================================================================

param([string]$ProjectPath = ".")

$ErrorActionPreference = "Stop"
$ProjectPath = Resolve-Path $ProjectPath

Write-Host "Updating Program.cs..." -ForegroundColor Yellow

$programCsPath = Join-Path $ProjectPath "src\Api\Program.cs"

if (-not (Test-Path $programCsPath)) {
    Write-Host "ERROR: Program.cs not found at: $programCsPath" -ForegroundColor Red
    exit 1
}

# Create backup
$backupPath = "$programCsPath.backup-$(Get-Date -Format 'yyyyMMdd_HHmmss')"
Copy-Item $programCsPath $backupPath
Write-Host "Backup created: $backupPath" -ForegroundColor Green

# The complete updated Program.cs content
$programCsContent = @'
using Enterprise.Documentation.Core.Governance;
using Enterprise.Documentation.Core.Infrastructure.Extensions;
using Enterprise.Documentation.Core.Application.Interfaces;
using Enterprise.Documentation.Api.Services;
using FluentValidation;
using System.Diagnostics;
using System.IO.Compression;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
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

// Add Password Hasher for secure password verification
builder.Services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<Enterprise.Documentation.Core.Domain.Entities.User>,
    Microsoft.AspNetCore.Identity.PasswordHasher<Enterprise.Documentation.Core.Domain.Entities.User>>();

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

// CRITICAL: JWT secret MUST come from environment variable or user secrets
// For development: dotnet user-secrets set "JwtSettings:SecretKey" "your-secret-key-here"
// For production: Set JWT_SECRET_KEY environment variable in Azure App Service
var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? builder.Configuration["JwtSettings:SecretKey"]
    ?? throw new InvalidOperationException(
        "JWT Secret Key not configured. Set JWT_SECRET_KEY environment variable or use: " +
        "dotnet user-secrets set \"JwtSettings:SecretKey\" \"your-key-at-least-32-chars\"");

if (secretKey.Length < 32)
{
    throw new InvalidOperationException(
        "JWT Secret Key must be at least 32 characters long for security. " +
        $"Current length: {secretKey.Length}");
}

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

// Add CORS with specific origins (SECURITY FIX)
var corsSettings = builder.Configuration.GetSection("Cors");
var allowedOrigins = corsSettings.GetSection("AllowedOrigins").Get<string[]>() ?? new[] { "https://localhost:5001" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("SecureCorsPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowedMethods("GET", "POST", "PUT", "DELETE")
              .AllowedHeaders("Content-Type", "Authorization", "X-Correlation-ID")
              .AllowCredentials()
              .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});

// Add Rate Limiting (SECURITY FIX)
builder.Services.AddRateLimiter(options =>
{
    // Global rate limit
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User?.Identity?.Name ?? context.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10
            }));

    // Auth endpoint rate limit (stricter)
    options.AddFixedWindowLimiter("auth", options =>
    {
        options.PermitLimit = 5;
        options.Window = TimeSpan.FromMinutes(1);
        options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        options.QueueLimit = 0;
    });
});

// Add Response Compression (PERFORMANCE FIX)
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<GzipCompressionProvider>();
    options.Providers.Add<BrotliCompressionProvider>();
});

builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Optimal;
});

builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Optimal;
});

// Add Memory Cache for performance (PERFORMANCE FIX)
builder.Services.AddMemoryCache();

// Add Controllers
builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.

// Global Exception Handler (SECURITY FIX)
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var exceptionHandlerPathFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
            var exception = exceptionHandlerPathFeature?.Error;

            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(exception, "Unhandled exception for {Path}", context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                title = "An error occurred while processing your request",
                status = 500,
                traceId = Activity.Current?.Id ?? context.TraceIdentifier
            });
        });
    });
    app.UseHsts(); // Enable HSTS for production
}

// Response Compression (PERFORMANCE FIX)
app.UseResponseCompression();

app.UseHttpsRedirection();

// CORS middleware (SECURITY FIX)
app.UseCors("SecureCorsPolicy");

// Security Headers Middleware (SECURITY FIX)
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Content-Security-Policy", "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'");
    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Add("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
    await next();
});

// Rate Limiting (SECURITY FIX)
app.UseRateLimiter();

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
'@

# Write the file
[System.IO.File]::WriteAllText($programCsPath, $programCsContent, [System.Text.Encoding]::UTF8)

Write-Host ""
Write-Host "[OK] Program.cs updated successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "What was added:" -ForegroundColor Cyan
Write-Host "  - JWT secret validation from environment/user secrets" -ForegroundColor White
Write-Host "  - CORS with specific allowed origins" -ForegroundColor White
Write-Host "  - Rate limiting (100/min global, 5/min auth)" -ForegroundColor White
Write-Host "  - Response compression (Gzip + Brotli)" -ForegroundColor White
Write-Host "  - Memory cache registration" -ForegroundColor White
Write-Host "  - Global exception handler" -ForegroundColor White
Write-Host "  - Security headers (CSP, X-Frame-Options, etc.)" -ForegroundColor White
Write-Host "  - HSTS for production" -ForegroundColor White
Write-Host "  - Password hasher service" -ForegroundColor White
Write-Host ""

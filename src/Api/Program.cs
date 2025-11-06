using Enterprise.Documentation.Core.Governance;
using FluentValidation;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Enterprise Data Governance Services (MANDATORY for V2)
builder.Services.AddScoped<IDataGovernanceProxy, DataGovernanceProxy>();
builder.Services.AddScoped<GovernanceSecurityEngine>();
builder.Services.AddScoped<GovernancePIIDetector>();
builder.Services.AddScoped<GovernanceAuditLogger>();
builder.Services.AddScoped<GovernanceAuthorizationEngine>();

// Add FluentValidation for governance request validation
builder.Services.AddScoped<IValidator<GovernanceQueryRequest>, GovernanceQueryRequestValidator>();

// Configure OpenTelemetry for governance observability
builder.Services.AddSingleton<ActivitySource>(provider => 
    new ActivitySource("Enterprise.Documentation.Platform.V2"));

// Add logging configuration
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

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

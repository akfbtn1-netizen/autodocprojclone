using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Core.Infrastructure.Data;
using Core.Application.Interfaces;

namespace Core.Infrastructure.Services;

/// <summary>
/// Health check service for Enterprise Documentation Platform
/// </summary>
public class HealthCheckService : IHealthCheck
{
    private readonly ILogger<HealthCheckService> _logger;
    private readonly DocumentationDbContext _dbContext;
    private readonly INodeJsTemplateExecutor _nodeJsExecutor;

    public HealthCheckService(
        ILogger<HealthCheckService> logger,
        DocumentationDbContext dbContext,
        INodeJsTemplateExecutor nodeJsExecutor)
    {
        _logger = logger;
        _dbContext = dbContext;
        _nodeJsExecutor = nodeJsExecutor;
    }

    /// <summary>
    /// Performs comprehensive health check of the platform
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var checks = new List<(string Name, bool IsHealthy, string Message)>();

            // Check database connectivity
            var dbHealth = await CheckDatabaseHealth(cancellationToken);
            checks.Add(("Database", dbHealth.IsHealthy, dbHealth.Message));

            // Check Node.js environment
            var nodeHealth = await CheckNodeJsEnvironment(cancellationToken);
            checks.Add(("NodeJS", nodeHealth.IsHealthy, nodeHealth.Message));

            // Check file system access
            var fsHealth = await CheckFileSystemHealth();
            checks.Add(("FileSystem", fsHealth.IsHealthy, fsHealth.Message));

            // Check templates availability
            var templateHealth = await CheckTemplatesHealth();
            checks.Add(("Templates", templateHealth.IsHealthy, templateHealth.Message));

            // Determine overall health
            var failedChecks = checks.Where(c => !c.IsHealthy).ToList();
            var isHealthy = !failedChecks.Any();

            var data = checks.ToDictionary(c => c.Name, c => (object)new { IsHealthy = c.IsHealthy, Message = c.Message });

            if (isHealthy)
            {
                _logger.LogInformation("Health check passed - all systems operational");
                return HealthCheckResult.Healthy("All systems operational", data);
            }
            else
            {
                var failureMessages = string.Join("; ", failedChecks.Select(c => $"{c.Name}: {c.Message}"));
                _logger.LogWarning("Health check failed: {Failures}", failureMessages);
                
                // Degraded if some systems are working
                var healthyCount = checks.Count(c => c.IsHealthy);
                if (healthyCount > 0)
                {
                    return HealthCheckResult.Degraded($"Some systems failing: {failureMessages}", null, data);
                }
                else
                {
                    return HealthCheckResult.Unhealthy($"Critical systems failing: {failureMessages}", null, data);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check encountered an error");
            return HealthCheckResult.Unhealthy("Health check failed with exception", ex);
        }
    }

    private async Task<(bool IsHealthy, string Message)> CheckDatabaseHealth(CancellationToken cancellationToken)
    {
        try
        {
            // Test basic connectivity
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                return (false, "Cannot connect to database");
            }

            // Test a simple query
            var count = await _dbContext.MasterIndexes.CountAsync(cancellationToken);
            
            return (true, $"Database accessible, {count} records in MasterIndex");
        }
        catch (Exception ex)
        {
            return (false, $"Database error: {ex.Message}");
        }
    }

    private async Task<(bool IsHealthy, string Message)> CheckNodeJsEnvironment(CancellationToken cancellationToken)
    {
        try
        {
            var isValid = await _nodeJsExecutor.ValidateEnvironmentAsync(cancellationToken);
            if (isValid)
            {
                var templates = await _nodeJsExecutor.GetAvailableTemplatesAsync(cancellationToken);
                return (true, $"Node.js runtime available, {templates.Count} templates found");
            }
            else
            {
                return (false, "Node.js runtime not available or templates not accessible");
            }
        }
        catch (Exception ex)
        {
            return (false, $"Node.js environment error: {ex.Message}");
        }
    }

    private async Task<(bool IsHealthy, string Message)> CheckFileSystemHealth()
    {
        try
        {
            var tempPath = Path.GetTempPath();
            var testFile = Path.Combine(tempPath, $"edp_health_check_{Guid.NewGuid()}.tmp");

            // Test write access
            await File.WriteAllTextAsync(testFile, "Health check test");
            
            // Test read access
            var content = await File.ReadAllTextAsync(testFile);
            
            // Clean up
            File.Delete(testFile);

            if (content == "Health check test")
            {
                return (true, "File system read/write access confirmed");
            }
            else
            {
                return (false, "File system read/write verification failed");
            }
        }
        catch (Exception ex)
        {
            return (false, $"File system error: {ex.Message}");
        }
    }

    private async Task<(bool IsHealthy, string Message)> CheckTemplatesHealth()
    {
        try
        {
            // Check for template directories
            var templatesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templates");
            
            if (!Directory.Exists(templatesPath))
            {
                return (false, "Templates directory not found");
            }

            // Count template files
            var tier1Templates = Directory.GetFiles(Path.Combine(templatesPath, "tier1"), "*.docx", SearchOption.TopDirectoryOnly).Length;
            var tier2Templates = Directory.GetFiles(Path.Combine(templatesPath, "tier2"), "*.docx", SearchOption.TopDirectoryOnly).Length;
            var tier3Templates = Directory.GetFiles(Path.Combine(templatesPath, "tier3"), "*.docx", SearchOption.TopDirectoryOnly).Length;
            
            var totalTemplates = tier1Templates + tier2Templates + tier3Templates;
            
            if (totalTemplates == 0)
            {
                return (false, "No template files found in template directories");
            }

            return (true, $"Templates available - Tier1: {tier1Templates}, Tier2: {tier2Templates}, Tier3: {tier3Templates}");
        }
        catch (Exception ex)
        {
            return (false, $"Templates check error: {ex.Message}");
        }
    }
}
using Hangfire;
using Hangfire.SqlServer;
using Hangfire.Dashboard;
using Hangfire.Client;
using Hangfire.Server;
using Hangfire.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace Enterprise.Documentation.Api.Configuration;

/// <summary>
/// Hangfire configuration for background job processing
/// Supports batch processing, scheduled jobs, and recurring tasks
/// </summary>
public static class HangfireConfiguration
{
    /// <summary>
    /// Add Hangfire services to the application
    /// </summary>
    public static IServiceCollection AddHangfireServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured");

        // Add Hangfire with SQL Server storage
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
            {
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.Zero,
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true,

                // Configure job expiration
                JobExpirationCheckInterval = TimeSpan.FromHours(1),

                // Enable distributed locks
                PrepareSchemaIfNecessary = true,

                // Configure schema - MUST use DaQa schema (only schema with write permissions)
                SchemaName = "DaQa"
            }));

        // Add Hangfire processing server
        services.AddHangfireServer(options =>
        {
            options.WorkerCount = Environment.ProcessorCount * 2;
            options.Queues = new[] { "default", "critical", "batch-processing", "vector-indexing" };
            options.ServerName = $"{Environment.MachineName}:{Guid.NewGuid()}";
        });

        return services;
    }

    /// <summary>
    /// Configure Hangfire dashboard and global settings
    /// </summary>
    public static IApplicationBuilder UseHangfireConfiguration(
        this IApplicationBuilder app,
        IConfiguration configuration)
    {
        // Enable Hangfire dashboard
        var dashboardEnabled = configuration.GetValue<bool>("Hangfire:DashboardEnabled", true);
        if (dashboardEnabled)
        {
            app.UseHangfireDashboard("/hangfire", new DashboardOptions
            {
                DashboardTitle = "Enterprise Documentation - Background Jobs",
                StatsPollingInterval = 2000, // 2 seconds

                // Authorization (configure based on environment)
                Authorization = new[]
                {
                    new HangfireDashboardAuthorizationFilter(configuration)
                }
            });
        }

        // Configure global job filters
        GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute
        {
            Attempts = 3,
            DelaysInSeconds = new[] { 30, 60, 120 },
            OnAttemptsExceeded = AttemptsExceededAction.Delete
        });

        // Get logger from DI container
        var serviceProvider = app.ApplicationServices;
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<JobLoggingFilter>();
        GlobalJobFilters.Filters.Add(new JobLoggingFilter(logger));

        // Configure recurring jobs
        ConfigureRecurringJobs(configuration);

        return app;
    }

    /// <summary>
    /// Configure recurring jobs (scheduled tasks)
    /// </summary>
    private static void ConfigureRecurringJobs(IConfiguration configuration)
    {
        // Example: Clean up old batch jobs every night at 2 AM
        var enableCleanup = configuration.GetValue<bool>("Hangfire:EnableOldBatchCleanup", true);
        if (enableCleanup)
        {
            RecurringJob.AddOrUpdate(
                "cleanup-old-batches",
                () => CleanupOldBatchJobs(90), // 90 days
                Cron.Daily(2)); // 2 AM daily
        }

        // Example: Update vector index statistics hourly
        var enableVectorStats = configuration.GetValue<bool>("Hangfire:EnableVectorStatsUpdate", false);
        if (enableVectorStats)
        {
            RecurringJob.AddOrUpdate(
                "update-vector-stats",
                () => UpdateVectorIndexStatistics(),
                Cron.Hourly);
        }

        // Example: Generate batch processing reports weekly
        var enableReports = configuration.GetValue<bool>("Hangfire:EnableWeeklyReports", false);
        if (enableReports)
        {
            RecurringJob.AddOrUpdate(
                "weekly-batch-report",
                () => GenerateWeeklyBatchReport(),
                Cron.Weekly(DayOfWeek.Monday, 9)); // Monday 9 AM
        }
    }

    #region Recurring Job Implementations

    /// <summary>
    /// Clean up old batch jobs from database
    /// </summary>
    [AutomaticRetry(Attempts = 3)]
    public static void CleanupOldBatchJobs(int daysOld, ILogger<HangfireConfiguration> logger)
    {
        // This would be implemented to clean up old completed/failed batches
        // For now, just a placeholder
        logger.LogInformation("Cleaning up batch jobs older than {DaysOld} days", daysOld);
    }

    /// <summary>
    /// Update vector index statistics
    /// </summary>
    [AutomaticRetry(Attempts = 3)]
    public static void UpdateVectorIndexStatistics(ILogger<HangfireConfiguration> logger)
    {
        // This would query the vector database and update statistics
        logger.LogInformation("Updating vector index statistics");
    }

    /// <summary>
    /// Generate weekly batch processing report
    /// </summary>
    [AutomaticRetry(Attempts = 3)]
    public static void GenerateWeeklyBatchReport(ILogger<HangfireConfiguration> logger)
    {
        // This would generate and email a weekly report
        logger.LogInformation("Generating weekly batch processing report");
    }

    #endregion
}

/// <summary>
/// Custom authorization filter for Hangfire dashboard
/// </summary>
public class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    private readonly IConfiguration _configuration;

    public HangfireDashboardAuthorizationFilter(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool Authorize(DashboardContext context)
    {
        // In development, allow all access
        if (_configuration.GetValue<bool>("Hangfire:AllowAnonymousAccess", false))
        {
            return true;
        }

        // In production, implement proper authentication
        // This could check for admin role, specific claims, etc.
        var httpContext = context.GetHttpContext();

        // Example: Check if user is authenticated
        return httpContext.User.Identity?.IsAuthenticated == true;

        // Example: Check for admin role
        // return httpContext.User.IsInRole("Admin");

        // Example: Check for specific claim
        // return httpContext.User.HasClaim("Permission", "ViewHangfireDashboard");
    }
}

/// <summary>
/// Job logging filter for comprehensive logging
/// </summary>
public class JobLoggingFilter : IClientFilter, IServerFilter
{
    private readonly ILogger<JobLoggingFilter> _logger;

    public JobLoggingFilter(ILogger<JobLoggingFilter> logger)
    {
        _logger = logger;
    }

    public void OnCreating(CreatingContext context)
    {
        _logger.LogDebug("Job creating: {JobType}.{JobMethod}",
            context.Job.Type.Name, context.Job.Method.Name);
    }

    public void OnCreated(CreatedContext context)
    {
        _logger.LogInformation("Job created: {JobId}", context.BackgroundJob.Id);
    }

    public void OnPerforming(PerformingContext context)
    {
        _logger.LogInformation("Job starting: {JobId}", context.BackgroundJob.Id);
    }

    public void OnPerformed(PerformedContext context)
    {
        if (context.Exception != null)
        {
            _logger.LogError(context.Exception, "Job failed: {JobId}", context.BackgroundJob.Id);
        }
        else
        {
            _logger.LogInformation("Job completed: {JobId}", context.BackgroundJob.Id);
        }
    }
}

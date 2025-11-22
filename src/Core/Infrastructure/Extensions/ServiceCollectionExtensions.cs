using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Enterprise.Documentation.Core.Infrastructure.Persistence;
using Enterprise.Documentation.Core.Application.Interfaces;
using Enterprise.Documentation.Core.Infrastructure.Persistence.Repositories;
using Enterprise.Documentation.Core.Application.Services.MasterIndex;
using Enterprise.Documentation.Core.Application.Services.ExcelSync;
using Enterprise.Documentation.Core.Application.Services.MetadataExtraction;
using Enterprise.Documentation.Core.Application.Services.DocumentGeneration;
using Enterprise.Documentation.Core.Application.Services.Notifications;
using Enterprise.Documentation.Core.Application.Services.VectorIndexing;
using Enterprise.Documentation.Core.Infrastructure.Services.MasterIndex;
using Enterprise.Documentation.Core.Infrastructure.Services.ExcelSync;
using Enterprise.Documentation.Core.Infrastructure.Services.MetadataExtraction;
using Enterprise.Documentation.Core.Infrastructure.Services.DocumentGeneration;
using Enterprise.Documentation.Core.Infrastructure.Services.Notifications;
using Enterprise.Documentation.Core.Infrastructure.Services.VectorIndexing;

namespace Enterprise.Documentation.Core.Infrastructure.Extensions;

/// <summary>
/// Extension methods for configuring infrastructure services in the dependency injection container.
/// Simple, working implementation focused on basic functionality.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds basic infrastructure services to the dependency injection container.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPersistence(configuration);
        services.AddInfrastructureServices(configuration);
        return services;
    }

    /// <summary>
    /// Adds all infrastructure service implementations
    /// </summary>
    private static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // HttpClient for API services
        services.AddHttpClient<IOpenAIEnhancementService, OpenAIEnhancementService>();
        services.AddHttpClient<ITeamsNotificationService, TeamsNotificationService>();
        services.AddHttpClient<IVectorIndexingService, VectorIndexingService>();

        // Service registrations
        services.AddScoped<IMasterIndexService, MasterIndexService>();
        services.AddScoped<IExcelToSqlSyncService, ExcelToSqlSyncService>();
        services.AddScoped<IMetadataExtractionService, MetadataExtractionService>();
        services.AddScoped<IExcelUpdateService, ExcelUpdateService>();

        // Background services (conditionally registered in Program.cs if needed)
        // services.AddHostedService<ExcelToSqlSyncService>();  // Register in Program.cs
        // services.AddHostedService<NotificationBatchingService>();  // Register in Program.cs

        return services;
    }

    /// <summary>
    /// Adds persistence services including Entity Framework and repository pattern.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        // Database context configuration
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=(localdb)\\mssqllocaldb;Database=EnterpriseDocumentationPlatform;Trusted_Connection=true;MultipleActiveResultSets=true";

        services.AddDbContext<DocumentationDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(DocumentationDbContext).Assembly.FullName);
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
            });

            // Basic configuration
            options.EnableSensitiveDataLogging(false);
            options.EnableDetailedErrors(true);
        });

        // Register DbContext as the base context for repositories
        services.AddScoped<DbContext>(provider => provider.GetRequiredService<DocumentationDbContext>());

        // Repository registrations
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITemplateRepository, TemplateRepository>();
        services.AddScoped<IVersionRepository, VersionRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        
        // Unit of Work registration
        services.AddScoped<IUnitOfWork, SimpleUnitOfWork>();

        return services;
    }
}
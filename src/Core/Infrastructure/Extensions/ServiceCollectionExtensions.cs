using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Enterprise.Documentation.Core.Infrastructure.Persistence;
using Enterprise.Documentation.Core.Application.Interfaces;
using Enterprise.Documentation.Core.Infrastructure.Persistence.Repositories;

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
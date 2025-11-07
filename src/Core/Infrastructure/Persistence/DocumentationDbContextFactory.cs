using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Logging;

namespace Enterprise.Documentation.Core.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for DocumentationDbContext to support EF Core migrations
/// </summary>
public class DocumentationDbContextFactory : IDesignTimeDbContextFactory<DocumentationDbContext>
{
    public DocumentationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DocumentationDbContext>();
        
        // Use a default connection string for migrations
        var connectionString = "Server=(localdb)\\mssqllocaldb;Database=EnterpriseDocumentationDB;Trusted_Connection=true;MultipleActiveResultSets=true";
        
        optionsBuilder.UseSqlServer(connectionString, options =>
        {
            options.MigrationsAssembly(typeof(DocumentationDbContext).Assembly.FullName);
        });

        // Create a simple logger for design-time
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<DocumentationDbContext>();

        return new DocumentationDbContext(optionsBuilder.Options, logger);
    }
}
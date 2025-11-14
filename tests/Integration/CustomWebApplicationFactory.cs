using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Enterprise.Documentation.Core.Infrastructure.Persistence;
using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.ValueObjects;

namespace Tests.Integration;

/// <summary>
/// Custom WebApplicationFactory for integration testing.
/// Configures in-memory database and test-specific services.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Add test configuration
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:SecretKey"] = "this-is-a-super-secret-key-for-development-testing-purposes-only-at-least-32-characters",
                ["JwtSettings:Issuer"] = "Enterprise.Documentation.Api",
                ["JwtSettings:Audience"] = "Enterprise.Documentation.Client",
                ["JwtSettings:ExpirationHours"] = "8"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<DocumentationDbContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add DbContext using in-memory database for testing
            services.AddDbContext<DocumentationDbContext>(options =>
            {
                options.UseInMemoryDatabase("TestDatabase");
                options.EnableSensitiveDataLogging();
            });

            // Build the service provider
            var sp = services.BuildServiceProvider();

            // Create a scope to obtain a reference to the database context
            using (var scope = sp.CreateScope())
            {
                var scopedServices = scope.ServiceProvider;
                var db = scopedServices.GetRequiredService<DocumentationDbContext>();
                var logger = scopedServices.GetRequiredService<ILogger<CustomWebApplicationFactory>>();

                // Ensure the database is created
                db.Database.EnsureCreated();

                try
                {
                    // Seed test data if needed
                    SeedTestData(db);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred seeding the test database.");
                    throw;
                }
            }
        });
    }

    private static void SeedTestData(DocumentationDbContext context)
    {
        // Clear existing data to ensure clean state
        context.Users.RemoveRange(context.Users);
        context.Templates.RemoveRange(context.Templates);
        context.Documents.RemoveRange(context.Documents);
        context.SaveChanges();

        // Add test users
        var testUser = User.Create(
            email: "testadmin@example.com",
            displayName: "Test Admin",
            securityClearance: SecurityClearance.Confidential,
            roles: new List<UserRole> { UserRole.Admin, UserRole.DocumentEditor }
        );

        var testUser2 = User.Create(
            email: "testuser@example.com",
            displayName: "Test User",
            securityClearance: SecurityClearance.Restricted,
            roles: new List<UserRole> { UserRole.DocumentViewer }
        );

        context.Users.AddRange(testUser, testUser2);
        context.SaveChanges();
    }
}

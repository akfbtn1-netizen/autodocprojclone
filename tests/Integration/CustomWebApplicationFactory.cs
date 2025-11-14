using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Enterprise.Documentation.Infrastructure.Data;

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
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add DbContext using in-memory database for testing
            services.AddDbContext<ApplicationDbContext>(options =>
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
                var db = scopedServices.GetRequiredService<ApplicationDbContext>();
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

    private static void SeedTestData(ApplicationDbContext context)
    {
        // Add any test data seeding here
        // For example:
        // - Test users
        // - Test templates
        // - Test documents

        // This will be called once when the factory is created
        // You can add specific test data as needed
    }
}

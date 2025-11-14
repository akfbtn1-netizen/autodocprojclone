# PowerShell script to apply integration test fixes
# Run this script from the project root directory

Write-Host "Applying integration test fixes..." -ForegroundColor Cyan
Write-Host ""

# Fix 1: Update CustomWebApplicationFactory.cs
Write-Host "1. Updating CustomWebApplicationFactory.cs..." -ForegroundColor Yellow

$factoryFile = "tests\Integration\CustomWebApplicationFactory.cs"
$factoryContent = @'
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
'@

Set-Content -Path $factoryFile -Value $factoryContent -NoNewline
Write-Host "   ✓ CustomWebApplicationFactory.cs updated" -ForegroundColor Green

# Fix 2: Update IntegrationTestHelpers.cs
Write-Host "2. Updating IntegrationTestHelpers.cs..." -ForegroundColor Yellow

$helpersFile = "tests\Integration\Helpers\IntegrationTestHelpers.cs"
$helpersContent = @'
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Tests.Integration.Helpers;

/// <summary>
/// Helper methods for integration tests.
/// </summary>
public static class IntegrationTestHelpers
{
    /// <summary>
    /// Creates an authenticated HTTP client with a valid JWT token.
    /// Uses pre-seeded test users from CustomWebApplicationFactory.
    /// </summary>
    /// <param name="factory">The web application factory</param>
    /// <param name="email">Email of the test user (default: testadmin@example.com)</param>
    /// <param name="password">Password for the test user (any value works in test environment)</param>
    /// <returns>An authenticated HTTP client</returns>
    public static async Task<HttpClient> CreateAuthenticatedClientAsync(
        CustomWebApplicationFactory factory,
        string email = "testadmin@example.com",
        string password = "TestPassword123!")
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        try
        {
            // Login to get JWT token using pre-seeded test user
            var token = await GetAuthTokenAsync(client, email, password);

            // Add JWT token to client default headers
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            return client;
        }
        catch (Exception ex)
        {
            client.Dispose();
            throw new InvalidOperationException(
                $"Failed to create authenticated client: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets an authentication token for a test user.
    /// </summary>
    /// <param name="client">HTTP client</param>
    /// <param name="email">User email</param>
    /// <param name="password">User password</param>
    /// <returns>JWT token</returns>
    public static async Task<string> GetAuthTokenAsync(
        HttpClient client,
        string email,
        string password)
    {
        var loginRequest = new
        {
            Email = email,
            Password = password
        };

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
        loginResponse.EnsureSuccessStatusCode();

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        if (loginResult?.Token == null)
        {
            throw new InvalidOperationException("Failed to obtain authentication token");
        }

        return loginResult.Token;
    }

    /// <summary>
    /// Creates a test user via the registration endpoint.
    /// </summary>
    /// <param name="client">HTTP client</param>
    /// <param name="email">User email</param>
    /// <param name="password">User password</param>
    /// <param name="firstName">First name</param>
    /// <param name="lastName">Last name</param>
    /// <returns>Task representing the async operation</returns>
    public static async Task CreateTestUserAsync(
        HttpClient client,
        string email,
        string password = "TestPassword123!",
        string firstName = "Test",
        string lastName = "User")
    {
        var registerRequest = new
        {
            Email = email,
            Password = password,
            FirstName = firstName,
            LastName = lastName,
            SecurityClearanceLevel = "Restricted"
        };

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Response model for login endpoint.
    /// </summary>
    private class LoginResponse
    {
        public string? Token { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime ExpiresAt { get; set; }
        public UserInfo? User { get; set; }
    }

    /// <summary>
    /// User information model.
    /// </summary>
    private class UserInfo
    {
        public Guid Id { get; set; }
        public string? Email { get; set; }
        public string? DisplayName { get; set; }
        public List<string>? Roles { get; set; }
    }
}
'@

Set-Content -Path $helpersFile -Value $helpersContent -NoNewline
Write-Host "   ✓ IntegrationTestHelpers.cs updated" -ForegroundColor Green

# Fix 3: Update AuthController.cs - Add RegisterRequest model
Write-Host "3. Updating AuthController.cs..." -ForegroundColor Yellow

$authFile = "src\Api\Controllers\AuthController.cs"
$authContent = Get-Content -Path $authFile -Raw

# Add RegisterRequest after LoginRequest
$loginRequestEnd = @'
public class LoginRequest
{
    /// <summary>User email address</summary>
    public string Email { get; set; } = string.Empty;
    /// <summary>User password</summary>
    public string Password { get; set; } = string.Empty;
}
'@

$withRegisterRequest = @'
public class LoginRequest
{
    /// <summary>User email address</summary>
    public string Email { get; set; } = string.Empty;
    /// <summary>User password</summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>User registration request model</summary>
public class RegisterRequest
{
    /// <summary>User email address</summary>
    public string Email { get; set; } = string.Empty;
    /// <summary>User password</summary>
    public string Password { get; set; } = string.Empty;
    /// <summary>First name</summary>
    public string FirstName { get; set; } = string.Empty;
    /// <summary>Last name</summary>
    public string LastName { get; set; } = string.Empty;
    /// <summary>Security clearance level</summary>
    public string SecurityClearanceLevel { get; set; } = "Restricted";
}
'@

$authContent = $authContent -replace [regex]::Escape($loginRequestEnd), $withRegisterRequest

# Add Register endpoint before Logout
$logoutMethodStart = @'
    /// <summary>
    /// Logs out the current user
    /// </summary>
    /// <returns>Success response</returns>
    [HttpPost("logout")]
'@

$registerAndLogout = @'
    /// <summary>
    /// Registers a new user
    /// </summary>
    /// <param name="request">Registration details</param>
    /// <returns>User information and JWT token</returns>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { error = "Email and password are required" });
            }

            if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            {
                return BadRequest(new { error = "First name and last name are required" });
            }

            // Check if user already exists
            var existingUser = await _userRepository.GetByEmailAsync(request.Email);
            if (existingUser != null)
            {
                _logger.LogWarning("Registration failed - user already exists: {Email}", request.Email);
                return Conflict(new { error = "User with this email already exists" });
            }

            // Parse security clearance
            if (!Enum.TryParse<SecurityClearance>(request.SecurityClearanceLevel, true, out var securityClearance))
            {
                securityClearance = SecurityClearance.Restricted;
            }

            // Create new user
            var newUser = User.Create(
                email: request.Email,
                displayName: $"{request.FirstName} {request.LastName}",
                securityClearance: securityClearance,
                roles: new List<UserRole> { UserRole.DocumentViewer }
            );

            // Note: In a real implementation, you would hash the password here
            // For now, we're not storing passwords for demo purposes

            // Save user
            await _userRepository.AddAsync(newUser);

            // Generate JWT token
            var token = GenerateJwtToken(newUser);
            var refreshToken = GenerateRefreshToken();

            var response = new LoginResponse
            {
                Token = token,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddHours(8),
                User = new UserInfo
                {
                    Id = newUser.Id.Value,
                    Email = newUser.Email,
                    DisplayName = newUser.DisplayName,
                    Roles = newUser.Roles.Select(r => r.ToString()).ToList()
                }
            };

            _logger.LogInformation("User registered successfully: {Email}", request.Email);
            return CreatedAtAction(nameof(GetCurrentUser), response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for user: {Email}", request.Email);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred during registration" });
        }
    }

    /// <summary>
    /// Logs out the current user
    /// </summary>
    /// <returns>Success response</returns>
    [HttpPost("logout")]
'@

$authContent = $authContent -replace [regex]::Escape($logoutMethodStart), $registerAndLogout

Set-Content -Path $authFile -Value $authContent -NoNewline
Write-Host "   ✓ AuthController.cs updated with Register endpoint" -ForegroundColor Green

Write-Host ""
Write-Host "All fixes applied successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Run: dotnet build" -ForegroundColor White
Write-Host "2. Run: dotnet test" -ForegroundColor White
Write-Host ""
Write-Host "The integration tests should now pass!" -ForegroundColor Green

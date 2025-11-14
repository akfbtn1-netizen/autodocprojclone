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
    /// </summary>
    /// <param name="factory">The web application factory</param>
    /// <param name="username">Optional username (defaults to test user)</param>
    /// <param name="password">Optional password (defaults to test password)</param>
    /// <returns>An authenticated HTTP client</returns>
    public static async Task<HttpClient> CreateAuthenticatedClientAsync(
        WebApplicationFactory<Program> factory,
        string username = "testadmin@example.com",
        string password = "TestPassword123!")
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        try
        {
            // First, register the test user if not exists
            var registerRequest = new
            {
                Email = username,
                Password = password,
                FirstName = "Test",
                LastName = "Admin",
                SecurityClearanceLevel = "Restricted"
            };

            var registerResponse = await client.PostAsJsonAsync("/api/auth/register", registerRequest);
            // Ignore if user already exists (409 Conflict)

            // Login to get JWT token
            var loginRequest = new
            {
                Email = username,
                Password = password
            };

            var loginResponse = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
            loginResponse.EnsureSuccessStatusCode();

            var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

            if (loginResult?.Token == null)
            {
                throw new InvalidOperationException("Failed to obtain authentication token");
            }

            // Add JWT token to client default headers
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", loginResult.Token);

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
    /// Response model for login endpoint.
    /// </summary>
    private class LoginResponse
    {
        public string? Token { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }
}

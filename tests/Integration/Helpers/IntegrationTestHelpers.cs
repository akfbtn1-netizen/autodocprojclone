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

    /// <summary>
    /// Sets a document version to under review status.
    /// Note: This is a helper method for testing. The actual implementation may vary
    /// depending on your API endpoints for document workflow management.
    /// </summary>
    /// <param name="client">HTTP client</param>
    /// <param name="documentId">Document ID</param>
    /// <param name="versionNumber">Version number</param>
    /// <returns>Task representing the async operation</returns>
    public static async Task SetDocumentVersionUnderReviewAsync(
        HttpClient client,
        Guid documentId,
        int versionNumber)
    {
        // This is a placeholder implementation
        // You may need to adjust the endpoint and payload based on your actual API
        var request = new
        {
            DocumentId = documentId,
            VersionNumber = versionNumber,
            Status = "UnderReview"
        };

        var response = await client.PutAsJsonAsync(
            $"/api/documents/{documentId}/versions/{versionNumber}/status",
            request);

        response.EnsureSuccessStatusCode();
    }
}

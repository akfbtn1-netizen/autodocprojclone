using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using Tests.Integration.Helpers;

namespace Tests.Integration.Controllers;

public class TemplatesControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public TemplatesControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        // Use authenticated client for all requests
        _client = IntegrationTestHelpers.CreateAuthenticatedClientAsync(factory).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task GetTemplates_ReturnsSuccessAsync()
    {
        // Seed a template first
        var templateCommand = new
        {
            Name = "Test Template",
            Category = "TestCategory",
            Content = "Sample content",
            Description = "Test template description",
            ContentType = "markdown",
            Variables = new List<object>(),
            RequiresApproval = false
        };
        var templateResponse = await _client.PostAsJsonAsync("/api/templates", templateCommand);
        templateResponse.EnsureSuccessStatusCode();

        var response = await _client.GetAsync("/api/templates");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

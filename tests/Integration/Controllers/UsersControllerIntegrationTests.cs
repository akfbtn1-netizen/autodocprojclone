using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;
using Tests.Integration.Helpers;

namespace Tests.Integration.Controllers;

public class UsersControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public UsersControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        // Use authenticated client for all requests
        _client = IntegrationTestHelpers.CreateAuthenticatedClientAsync(factory).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task GetUsers_ReturnsSuccessAsync()
    {
        var response = await _client.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

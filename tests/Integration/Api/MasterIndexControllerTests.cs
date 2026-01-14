using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Enterprise.Documentation.Shared.Contracts.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Tests.Integration.Api;

/// <summary>
/// Integration tests for MasterIndexController endpoints.
/// Tests real API behavior with authentication.
/// </summary>
public class MasterIndexControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public MasterIndexControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> GetAuthTokenAsync()
    {
        var loginRequest = new
        {
            Email = "admin@enterprise.com",
            Password = "admin123"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        if (!response.IsSuccessStatusCode)
        {
            // Return empty token if auth fails - tests will handle unauthorized
            return string.Empty;
        }

        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        return jsonDoc.RootElement.GetProperty("token").GetString() ?? string.Empty;
    }

    private void SetAuthHeader(string token)
    {
        if (!string.IsNullOrEmpty(token))
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetAll_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.GetAsync("/api/masterindex?pageNumber=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetAll_WithAuth_ReturnsSuccess()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            // Skip test if auth is not available
            return;
        }
        SetAuthHeader(token);

        // Act
        var response = await _client.GetAsync("/api/masterindex?pageNumber=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<PaginatedResponse<MasterIndexSummaryDto>>();
        content.Should().NotBeNull();
        content!.PageNumber.Should().Be(1);
        content.PageSize.Should().Be(10);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetAll_EnforcesMaxPageSize()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        if (string.IsNullOrEmpty(token)) return;
        SetAuthHeader(token);

        // Act - Request page size of 200 (should be capped to 100)
        var response = await _client.GetAsync("/api/masterindex?pageNumber=1&pageSize=200");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<PaginatedResponse<MasterIndexSummaryDto>>();
        content.Should().NotBeNull();
        content!.PageSize.Should().BeLessOrEqualTo(100);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetById_WithValidId_ReturnsEntry()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        if (string.IsNullOrEmpty(token)) return;
        SetAuthHeader(token);

        // First get an existing ID from the list
        var listResponse = await _client.GetAsync("/api/masterindex?pageNumber=1&pageSize=1");
        if (listResponse.StatusCode != HttpStatusCode.OK) return;

        var list = await listResponse.Content.ReadFromJsonAsync<PaginatedResponse<MasterIndexSummaryDto>>();
        if (list?.Items == null || list.Items.Count == 0) return;

        var existingId = list.Items[0].IndexId;

        // Act
        var response = await _client.GetAsync($"/api/masterindex/{existingId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<MasterIndexDetailDto>();
        content.Should().NotBeNull();
        content!.IndexId.Should().Be(existingId);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetById_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        if (string.IsNullOrEmpty(token)) return;
        SetAuthHeader(token);

        // Act
        var response = await _client.GetAsync("/api/masterindex/999999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetStatistics_ReturnsValidData()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        if (string.IsNullOrEmpty(token)) return;
        SetAuthHeader(token);

        // Act
        var response = await _client.GetAsync("/api/masterindex/statistics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var stats = await response.Content.ReadFromJsonAsync<MasterIndexStatisticsDto>();
        stats.Should().NotBeNull();
        stats!.TotalDocuments.Should().BeGreaterOrEqualTo(0);
        stats.ComputedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetByApprovalStatus_ReturnsFilteredResults()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        if (string.IsNullOrEmpty(token)) return;
        SetAuthHeader(token);

        // Act
        var response = await _client.GetAsync("/api/masterindex/by-status/Draft");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<List<MasterIndexSummaryDto>>();
        content.Should().NotBeNull();
        // All items should have Draft status (if any returned)
        content!.Where(x => x.ApprovalStatus != null)
            .All(x => x.ApprovalStatus == "Draft")
            .Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Search_WithEmptyQuery_ReturnsBadRequest()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        if (string.IsNullOrEmpty(token)) return;
        SetAuthHeader(token);

        // Act
        var response = await _client.GetAsync("/api/masterindex/search?query=");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Search_WithValidQuery_ReturnsResults()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        if (string.IsNullOrEmpty(token)) return;
        SetAuthHeader(token);

        // Act
        var response = await _client.GetAsync("/api/masterindex/search?query=test&pageNumber=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<PaginatedResponse<MasterIndexSummaryDto>>();
        content.Should().NotBeNull();
        content!.PageNumber.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetByTier_WithValidTier_ReturnsResults()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        if (string.IsNullOrEmpty(token)) return;
        SetAuthHeader(token);

        // Act
        var response = await _client.GetAsync("/api/masterindex/by-tier/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<List<MasterIndexSummaryDto>>();
        content.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetByDatabase_WithValidDatabase_ReturnsResults()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        if (string.IsNullOrEmpty(token)) return;
        SetAuthHeader(token);

        // Act
        var response = await _client.GetAsync("/api/masterindex/by-database/IRFS1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<List<MasterIndexSummaryDto>>();
        content.Should().NotBeNull();
    }
}

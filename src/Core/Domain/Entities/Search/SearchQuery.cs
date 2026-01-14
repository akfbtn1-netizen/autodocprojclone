namespace Enterprise.Documentation.Core.Domain.Entities.Search;

/// <summary>
/// Represents a search query executed by a user.
/// Tracked for analytics and continuous learning.
/// </summary>
public class SearchQuery
{
    public Guid QueryId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string QueryText { get; private set; } = string.Empty;
    public string QueryType { get; private set; } = string.Empty;
    public string RoutingPath { get; private set; } = string.Empty;
    public int ExecutionTimeMs { get; private set; }
    public int ResultCount { get; private set; }
    public DateTime SearchedAt { get; private set; }
    public string? SessionId { get; private set; }
    public string? IPAddress { get; private set; }

    private readonly List<SearchResult> _results = new();
    public IReadOnlyCollection<SearchResult> Results => _results.AsReadOnly();

    private readonly List<UserInteraction> _interactions = new();
    public IReadOnlyCollection<UserInteraction> Interactions => _interactions.AsReadOnly();

    private SearchQuery() { } // EF Core

    public static SearchQuery Create(
        string userId,
        string queryText,
        string queryType,
        string routingPath,
        string? sessionId = null,
        string? ipAddress = null)
    {
        return new SearchQuery
        {
            QueryId = Guid.NewGuid(),
            UserId = userId,
            QueryText = queryText,
            QueryType = queryType,
            RoutingPath = routingPath,
            SearchedAt = DateTime.UtcNow,
            SessionId = sessionId,
            IPAddress = ipAddress
        };
    }

    public void SetExecutionResults(int executionTimeMs, int resultCount)
    {
        ExecutionTimeMs = executionTimeMs;
        ResultCount = resultCount;
    }

    public void AddResult(SearchResult result)
    {
        _results.Add(result);
    }

    public void AddInteraction(UserInteraction interaction)
    {
        _interactions.Add(interaction);
    }
}

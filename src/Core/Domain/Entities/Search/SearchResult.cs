namespace Enterprise.Documentation.Core.Domain.Entities.Search;

/// <summary>
/// Represents a single search result returned for a query.
/// Tracks user engagement for learning.
/// </summary>
public class SearchResult : BaseEntity
{
    public Guid QueryId { get; private set; }
    public string DocumentId { get; private set; } = string.Empty;
    public string ObjectType { get; private set; } = string.Empty;
    public string ObjectName { get; private set; } = string.Empty;
    public string? SchemaName { get; private set; }
    public int Rank { get; private set; }
    public decimal RelevanceScore { get; private set; }
    public bool WasClicked { get; private set; }
    public int? TimeSpentSeconds { get; private set; }
    public bool WasExported { get; private set; }
    public bool WasShared { get; private set; }

    private SearchResult() { } // EF Core

    public static SearchResult Create(
        Guid queryId,
        string documentId,
        string objectType,
        string objectName,
        string? schemaName,
        int rank,
        decimal relevanceScore)
    {
        return new SearchResult
        {
            QueryId = queryId,
            DocumentId = documentId,
            ObjectType = objectType,
            ObjectName = objectName,
            SchemaName = schemaName,
            Rank = rank,
            RelevanceScore = relevanceScore
        };
    }

    public void MarkClicked()
    {
        WasClicked = true;
    }

    public void SetTimeSpent(int seconds)
    {
        TimeSpentSeconds = seconds;
    }

    public void MarkExported()
    {
        WasExported = true;
    }

    public void MarkShared()
    {
        WasShared = true;
    }
}

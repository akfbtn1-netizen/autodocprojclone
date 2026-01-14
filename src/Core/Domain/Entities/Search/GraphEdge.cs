namespace Enterprise.Documentation.Core.Domain.Entities.Search;

/// <summary>
/// Graph edge representing relationships between nodes.
/// Critical for PII_FLOW tracking (compliance requirement).
/// </summary>
public class GraphEdge : BaseEntity
{
    public Guid EdgeId { get; private set; }
    public string SourceNodeId { get; private set; } = string.Empty;
    public string TargetNodeId { get; private set; } = string.Empty;
    public string EdgeType { get; private set; } = string.Empty;
    public decimal? EdgeWeight { get; private set; }
    public string? Properties { get; private set; } // JSON metadata
    public DateTime CreatedAt { get; private set; }

    // Navigation properties
    public GraphNode? SourceNode { get; private set; }
    public GraphNode? TargetNode { get; private set; }

    private GraphEdge() { } // EF Core

    public static GraphEdge Create(
        string sourceNodeId,
        string targetNodeId,
        string edgeType,
        decimal? edgeWeight = null,
        string? properties = null)
    {
        return new GraphEdge
        {
            EdgeId = Guid.NewGuid(),
            SourceNodeId = sourceNodeId,
            TargetNodeId = targetNodeId,
            EdgeType = edgeType,
            EdgeWeight = edgeWeight,
            Properties = properties,
            CreatedAt = DateTime.UtcNow
        };
    }

    public bool IsPiiFlow => EdgeType == EdgeTypes.PiiFlow;
}

/// <summary>
/// Edge type constants for relationship classification
/// </summary>
public static class EdgeTypes
{
    public const string DependsOn = "DEPENDS_ON";
    public const string Contains = "CONTAINS";
    public const string ReadsFrom = "READS_FROM";
    public const string WritesTo = "WRITES_TO";
    public const string PiiFlow = "PII_FLOW";
    public const string References = "REFERENCES";
    public const string Calls = "CALLS";
    public const string Inherits = "INHERITS";
}

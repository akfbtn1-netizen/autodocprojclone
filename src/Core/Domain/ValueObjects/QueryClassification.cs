namespace Enterprise.Documentation.Core.Domain.ValueObjects;

/// <summary>
/// Value object representing the classification of a search query.
/// Determines routing path for optimal search strategy.
/// </summary>
public record QueryClassification
{
    public RoutingPath RoutingPath { get; init; }
    public QueryComplexity QueryComplexity { get; init; }
    public decimal Confidence { get; init; }
    public int EstimatedLatencyMs { get; init; }
    public string Reasoning { get; init; } = string.Empty;

    public bool IsSimple => QueryComplexity == QueryComplexity.Simple;
    public bool IsComplex => QueryComplexity == QueryComplexity.Complex;
    public bool IsHighConfidence => Confidence >= 0.8m;
}

/// <summary>
/// 5-path routing system for query handling
/// </summary>
public enum RoutingPath
{
    /// <summary>Pure keyword search (ColBERT only)</summary>
    Path1_Keyword = 1,

    /// <summary>Semantic search (Vector only)</summary>
    Path2_Semantic = 2,

    /// <summary>Relationship-heavy (GraphRAG)</summary>
    Path3_Relationship = 3,

    /// <summary>Metadata-heavy (Structured embeddings + filters)</summary>
    Path4_Metadata = 4,

    /// <summary>Multi-agent agentic (complex orchestration)</summary>
    Path5_Agentic = 5
}

/// <summary>
/// Query complexity levels
/// </summary>
public enum QueryComplexity
{
    Simple,
    Medium,
    Complex
}

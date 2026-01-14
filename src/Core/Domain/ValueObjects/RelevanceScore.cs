namespace Enterprise.Documentation.Core.Domain.ValueObjects;

/// <summary>
/// Value object representing a relevance score (0.0 to 1.0).
/// Supports multiple scoring sources and fusion.
/// </summary>
public readonly record struct RelevanceScore
{
    public decimal Value { get; }
    public string Source { get; }

    private RelevanceScore(decimal value, string source)
    {
        Value = Math.Clamp(value, 0m, 1m);
        Source = source;
    }

    public static RelevanceScore FromVector(decimal score) => new(score, "Vector");
    public static RelevanceScore FromColBert(decimal score) => new(score, "ColBERT");
    public static RelevanceScore FromGraph(decimal score) => new(score, "Graph");
    public static RelevanceScore FromMetadata(decimal score) => new(score, "Metadata");
    public static RelevanceScore FromFusion(decimal score) => new(score, "Fusion");

    /// <summary>
    /// Reciprocal Rank Fusion for combining multiple scores
    /// </summary>
    public static RelevanceScore RRFFusion(params RelevanceScore[] scores)
    {
        if (scores.Length == 0)
            return new RelevanceScore(0m, "Fusion");

        // RRF formula: score = sum(1 / (k + rank_i)) where k = 60
        const decimal k = 60m;
        var fusedScore = scores.Sum(s => 1m / (k + (1m - s.Value) * 100m));
        var normalized = Math.Min(fusedScore / scores.Length, 1m);

        return FromFusion(normalized);
    }

    public bool IsHighRelevance => Value >= 0.8m;
    public bool IsMediumRelevance => Value >= 0.5m && Value < 0.8m;
    public bool IsLowRelevance => Value < 0.5m;

    public override string ToString() => $"{Value:P1} ({Source})";
}

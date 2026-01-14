// =============================================================================
// Agent #7: Gap Intelligence Agent - Domain Models
// ML-style documentation gap detection with RLHF learning capabilities
// =============================================================================

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Enterprise.Documentation.Core.Application.Services.GapIntelligence;

#region Core Detection Models

/// <summary>
/// Result of a gap detection run
/// </summary>
public class GapDetectionResult
{
    public int RunId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ObjectsScanned { get; set; }
    public int GapsDetected { get; set; }
    public int NewGaps { get; set; }
    public int ResolvedGaps { get; set; }
    public Dictionary<string, int> PatternResults { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Learned detection pattern with RLHF metrics
/// </summary>
public class GapPattern
{
    public int PatternId { get; set; }
    public string PatternName { get; set; } = string.Empty;
    public string PatternType { get; set; } = string.Empty;
    public string? PatternDescription { get; set; }
    public string DetectionRules { get; set; } = "{}";
    public int TruePositives { get; set; }
    public int FalsePositives { get; set; }
    public decimal Precision { get; set; }
    public bool IsActive { get; set; } = true;
    public decimal ConfidenceThreshold { get; set; } = 0.7m;
    public DateTime? LastTriggered { get; set; }
    public int TriggerCount { get; set; }
}

/// <summary>
/// A detected documentation gap
/// </summary>
public class DetectedGap
{
    public int GapId { get; set; }
    public string SchemaName { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public string? ColumnName { get; set; }
    public string GapType { get; set; } = string.Empty;
    public int? PatternId { get; set; }
    public string Severity { get; set; } = "MEDIUM";
    public int Priority { get; set; }
    public decimal Confidence { get; set; }
    public string? Evidence { get; set; }
    public string Status { get; set; } = "OPEN";
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public int AgeInDays { get; set; }
    public string? AssignedTo { get; set; }
}

#endregion

#region Importance & Scoring Models

/// <summary>
/// ML-derived importance score for an object
/// </summary>
public class ObjectImportanceScore
{
    public int ScoreId { get; set; }
    public string SchemaName { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public int UsageScore { get; set; }
    public int DependencyScore { get; set; }
    public int DataVolumeScore { get; set; }
    public int ChangeFrequencyScore { get; set; }
    public int BusinessCriticalityScore { get; set; }
    public int PIIScore { get; set; }
    public int LineageDepthScore { get; set; }
    public decimal CompositeScore =>
        (UsageScore * 0.25m) + (DependencyScore * 0.20m) + (DataVolumeScore * 0.10m) +
        (ChangeFrequencyScore * 0.10m) + (BusinessCriticalityScore * 0.15m) +
        (PIIScore * 0.10m) + (LineageDepthScore * 0.10m);
    public bool HasMasterIndexEntry { get; set; }
    public decimal GapPriority => HasMasterIndexEntry ? CompositeScore * 0.5m : CompositeScore * 1.5m;
    public DateTime LastCalculatedAt { get; set; }
}

/// <summary>
/// Documentation velocity metrics for a group
/// </summary>
public class DocumentationVelocity
{
    public int VelocityId { get; set; }
    public string GroupType { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public int DocsCreated30d { get; set; }
    public int DocsApproved30d { get; set; }
    public decimal? AvgApprovalTimeHours { get; set; }
    public int TotalObjects { get; set; }
    public int DocumentedObjects { get; set; }
    public decimal CoveragePercent => TotalObjects > 0 ? DocumentedObjects * 100m / TotalObjects : 0;
    public string? VelocityTrend { get; set; }
    public int? DaysToFullCoverage { get; set; }
}

#endregion

#region Clustering Models

/// <summary>
/// A semantic cluster of related objects
/// </summary>
public class SemanticCluster
{
    public int ClusterId { get; set; }
    public string ClusterName { get; set; } = string.Empty;
    public string? DomainTag { get; set; }
    public int MemberCount { get; set; }
    public int DocumentedCount { get; set; }
    public decimal CoveragePercent => MemberCount > 0 ? DocumentedCount * 100m / MemberCount : 0;
    public float[]? CentroidEmbedding { get; set; }
    public int OutlierCount { get; set; }
    public List<ClusterMember> Members { get; set; } = new();
}

/// <summary>
/// A member of a semantic cluster
/// </summary>
public class ClusterMember
{
    public string SchemaName { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public decimal? DistanceFromCentroid { get; set; }
    public bool IsOutlier { get; set; }
    public bool IsDocumented { get; set; }
}

/// <summary>
/// Object with embedding for clustering
/// </summary>
public class ObjectEmbedding
{
    public string SchemaName { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public float[]? Embedding { get; set; }
    public bool IsDocumented { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Result of a clustering operation
/// </summary>
public class ClusteringResult
{
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalObjects { get; set; }
    public int ClustersCreated { get; set; }
    public List<SemanticCluster> Clusters { get; set; } = new();
}

/// <summary>
/// Gap detected through cluster analysis
/// </summary>
public class ClusterGap
{
    public string SchemaName { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public int ClusterId { get; set; }
    public string ClusterName { get; set; } = string.Empty;
    public decimal ClusterCoverage { get; set; }
    public decimal? DistanceFromCentroid { get; set; }
    public decimal Confidence { get; set; }
    public string GapType => "SEMANTIC";

    public DetectedGap ToDetectedGap() => new()
    {
        SchemaName = SchemaName,
        ObjectName = ObjectName,
        ObjectType = ObjectType,
        GapType = GapType,
        Severity = ClusterCoverage > 90 ? "HIGH" : "MEDIUM",
        Priority = (int)(ClusterCoverage * Confidence),
        Confidence = Confidence,
        Evidence = JsonSerializer.Serialize(new { ClusterId, ClusterName, ClusterCoverage })
    };
}

#endregion

#region Prediction Models

/// <summary>
/// A predicted future documentation gap
/// </summary>
public class PredictedGap
{
    public int PredictionId { get; set; }
    public string SchemaName { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public string PredictedGapType { get; set; } = string.Empty;
    public decimal PredictionConfidence { get; set; }
    public int? DaysUntilGap { get; set; }
    public List<string> RiskFactors { get; set; } = new();
    public string? RecommendedPriority { get; set; }
    public DateTime PredictedAt { get; set; }
}

#endregion

#region Feedback & Learning Models

/// <summary>
/// Human feedback on a detected gap for RLHF learning
/// </summary>
public class GapFeedback
{
    public int FeedbackId { get; set; }
    public string SchemaName { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public int? PatternId { get; set; }
    public string DetectedGapType { get; set; } = string.Empty;
    public decimal DetectedConfidence { get; set; }
    public string FeedbackType { get; set; } = string.Empty; // CONFIRMED, REJECTED, DEFERRED
    public string FeedbackBy { get; set; } = string.Empty;
    public DateTime FeedbackAt { get; set; } = DateTime.UtcNow;
    public string? FeedbackReason { get; set; }
}

/// <summary>
/// Suggested evolution of a detection pattern
/// </summary>
public class PatternEvolutionSuggestion
{
    public int PatternId { get; set; }
    public string EvolutionType { get; set; } = string.Empty;
    public string NewRules { get; set; } = "{}";
    public string Explanation { get; set; } = string.Empty;
    public decimal ExpectedPrecisionGain { get; set; }
}

#endregion

#region Usage & Heatmap Models

/// <summary>
/// Query usage statistics from DMVs
/// </summary>
public class UsageHeatmapEntry
{
    public string SchemaName { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public long ExecutionCount30d { get; set; }
    public decimal? AvgCpuTimeMs { get; set; }
    public long? AvgLogicalReads { get; set; }
    public int? UniqueUsers7d { get; set; }
    public decimal HeatScore { get; set; }
    public string? UsageTrend { get; set; }
}

/// <summary>
/// High-usage object without documentation
/// </summary>
public class UndocumentedHotspot
{
    public string SchemaName { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public decimal HeatScore { get; set; }
    public long ExecutionCount30d { get; set; }
}

#endregion

#region Dashboard Models

/// <summary>
/// Aggregated data for the Gap Intelligence dashboard
/// </summary>
public class GapDashboardData
{
    public Dictionary<string, int> GapsBySeverity { get; set; } = new();
    public Dictionary<string, int> GapsByType { get; set; } = new();
    public List<DetectedGap> TopPriorityGaps { get; set; } = new();
    public List<SchemaCoverage> CoverageBySchema { get; set; } = new();
    public List<PatternEffectiveness> PatternEffectiveness { get; set; } = new();
    public List<PredictedGap> UpcomingPredictions { get; set; } = new();
    public int TotalOpenGaps { get; set; }
    public int CriticalGaps { get; set; }
    public int HighPriorityGaps { get; set; }
}

/// <summary>
/// Documentation coverage for a schema
/// </summary>
public class SchemaCoverage
{
    public string SchemaName { get; set; } = string.Empty;
    public int TotalObjects { get; set; }
    public int DocumentedObjects { get; set; }
    public decimal CoveragePercent => TotalObjects > 0 ? DocumentedObjects * 100m / TotalObjects : 0;
}

/// <summary>
/// Effectiveness metrics for a detection pattern
/// </summary>
public class PatternEffectiveness
{
    public int PatternId { get; set; }
    public string PatternName { get; set; } = string.Empty;
    public decimal Precision { get; set; }
    public int TriggerCount { get; set; }
    public int ActiveGaps { get; set; }
}

#endregion

#region Natural Language Models

/// <summary>
/// Result of a natural language gap query
/// </summary>
public class GapQueryResult
{
    public string OriginalQuery { get; set; } = string.Empty;
    public ParsedGapQuery ParsedIntent { get; set; } = new();
    public List<DetectedGap> Gaps { get; set; } = new();
    public Dictionary<string, object> Statistics { get; set; } = new();
    public string NaturalLanguageSummary { get; set; } = string.Empty;
    public List<string> SuggestedFollowUps { get; set; } = new();
}

/// <summary>
/// Parsed intent from a natural language query
/// </summary>
public class ParsedGapQuery
{
    public string Intent { get; set; } = "LIST_GAPS";
    public Dictionary<string, object>? Filters { get; set; }
    public string? SortBy { get; set; }
    public int Limit { get; set; } = 50;
}

/// <summary>
/// Insight about documentation gaps
/// </summary>
public class GapInsight
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Action { get; set; }
    public string Priority { get; set; } = "MEDIUM";
}

#endregion

#region Utility Models

/// <summary>
/// Paginated result container
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

/// <summary>
/// Schema change event for queue processing
/// </summary>
public class SchemaChangeEvent
{
    public string SchemaName { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
}

#endregion

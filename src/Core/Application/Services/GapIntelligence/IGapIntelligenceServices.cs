// =============================================================================
// Agent #7: Gap Intelligence Agent - Service Interfaces
// Defines contracts for gap detection, clustering, learning, and NL queries
// =============================================================================

namespace Enterprise.Documentation.Core.Application.Services.GapIntelligence;

/// <summary>
/// Core Gap Intelligence Agent - ML-style documentation gap detection
/// </summary>
public interface IGapIntelligenceAgent
{
    #region Detection

    /// <summary>
    /// Run full gap detection across all database objects
    /// </summary>
    Task<GapDetectionResult> RunFullDetectionAsync(CancellationToken ct = default);

    /// <summary>
    /// Run incremental detection for recently modified objects
    /// </summary>
    Task<GapDetectionResult> RunIncrementalDetectionAsync(DateTime since, CancellationToken ct = default);

    /// <summary>
    /// Detect gaps for a specific object
    /// </summary>
    Task<List<DetectedGap>> DetectGapsForObjectAsync(string schema, string objectName, CancellationToken ct = default);

    #endregion

    #region Analysis

    /// <summary>
    /// Calculate importance score for an object
    /// </summary>
    Task<ObjectImportanceScore> CalculateImportanceScoreAsync(string schema, string objectName, CancellationToken ct = default);

    /// <summary>
    /// Refresh usage heatmap from DMVs
    /// </summary>
    Task RefreshUsageHeatmapAsync(CancellationToken ct = default);

    /// <summary>
    /// Predict future documentation gaps
    /// </summary>
    Task<List<PredictedGap>> PredictFutureGapsAsync(int daysAhead = 30, CancellationToken ct = default);

    #endregion

    #region Clustering

    /// <summary>
    /// Run semantic clustering using embeddings
    /// </summary>
    Task<ClusteringResult> RunSemanticClusteringAsync(CancellationToken ct = default);

    /// <summary>
    /// Find undocumented outliers in documented clusters
    /// </summary>
    Task<List<ClusterGap>> FindClusterOutliersAsync(CancellationToken ct = default);

    #endregion

    #region Learning (RLHF)

    /// <summary>
    /// Record human feedback on a detected gap
    /// </summary>
    Task RecordFeedbackAsync(GapFeedback feedback, CancellationToken ct = default);

    /// <summary>
    /// Update pattern precision from feedback
    /// </summary>
    Task UpdatePatternPrecisionAsync(int patternId, CancellationToken ct = default);

    /// <summary>
    /// Get all active detection patterns
    /// </summary>
    Task<List<GapPattern>> GetActivePatternsAsync(CancellationToken ct = default);

    #endregion

    #region Reporting

    /// <summary>
    /// Get dashboard summary data
    /// </summary>
    Task<GapDashboardData> GetDashboardDataAsync(CancellationToken ct = default);

    /// <summary>
    /// Get documentation velocity metrics for a group
    /// </summary>
    Task<DocumentationVelocity> GetVelocityMetricsAsync(string groupType, string groupName, CancellationToken ct = default);

    #endregion
}

/// <summary>
/// Mines query patterns from DMVs
/// </summary>
public interface IQueryPatternMiner
{
    /// <summary>
    /// Mine query patterns from execution stats
    /// </summary>
    Task<List<UsageHeatmapEntry>> MineQueryPatternsAsync(int lookbackDays = 30, CancellationToken ct = default);

    /// <summary>
    /// Find high-usage undocumented objects
    /// </summary>
    Task<List<UndocumentedHotspot>> FindUndocumentedHotspotsAsync(CancellationToken ct = default);
}

/// <summary>
/// Semantic clustering using Azure OpenAI embeddings
/// </summary>
public interface ISemanticClusteringService
{
    /// <summary>
    /// Generate embedding for an object based on its metadata
    /// </summary>
    Task<float[]> GenerateObjectEmbeddingAsync(string schema, string objectName, string? description = null, CancellationToken ct = default);

    /// <summary>
    /// Cluster objects using K-means
    /// </summary>
    Task<List<SemanticCluster>> ClusterObjectsAsync(List<ObjectEmbedding> embeddings, int k = 10, CancellationToken ct = default);

    /// <summary>
    /// Find outliers in a cluster
    /// </summary>
    Task<List<ClusterMember>> FindOutliersAsync(int clusterId, CancellationToken ct = default);
}

/// <summary>
/// Natural language query interface
/// </summary>
public interface IGapNaturalLanguageService
{
    /// <summary>
    /// Query gaps using natural language
    /// </summary>
    Task<GapQueryResult> QueryAsync(string naturalLanguageQuery, CancellationToken ct = default);

    /// <summary>
    /// Get insights about a topic
    /// </summary>
    Task<List<GapInsight>> GetInsightsAsync(string topic, CancellationToken ct = default);

    /// <summary>
    /// Explain why a gap was detected
    /// </summary>
    Task<string> ExplainGapAsync(DetectedGap gap, CancellationToken ct = default);

    /// <summary>
    /// Suggest follow-up questions
    /// </summary>
    Task<List<string>> SuggestQuestionsAsync(CancellationToken ct = default);
}

/// <summary>
/// RLHF-style learning from human feedback
/// </summary>
public interface IRLHFLearningEngine
{
    /// <summary>
    /// Process accumulated feedback
    /// </summary>
    Task ProcessFeedbackBatchAsync(CancellationToken ct = default);

    /// <summary>
    /// Update pattern weights based on feedback
    /// </summary>
    Task UpdatePatternWeightsAsync(int patternId, CancellationToken ct = default);

    /// <summary>
    /// Calibrate confidence thresholds
    /// </summary>
    Task CalibrateConfidenceAsync(CancellationToken ct = default);

    /// <summary>
    /// Suggest pattern evolution based on feedback
    /// </summary>
    Task<List<PatternEvolutionSuggestion>> SuggestPatternEvolutionsAsync(CancellationToken ct = default);
}

/// <summary>
/// Integration with existing platform services
/// </summary>
public interface IGapIntegrationService
{
    /// <summary>
    /// Check if object has a MasterIndex entry
    /// </summary>
    Task<bool> HasMasterIndexEntryAsync(string schema, string objectName, CancellationToken ct = default);

    /// <summary>
    /// Get documentation coverage by schema
    /// </summary>
    Task<List<SchemaCoverage>> GetCoverageBySchemaAsync(CancellationToken ct = default);
}

/// <summary>
/// Hangfire job service for scheduled tasks
/// </summary>
public interface IGapIntelligenceJobService
{
    /// <summary>
    /// Run scheduled full detection (nightly)
    /// </summary>
    Task RunScheduledFullDetectionAsync();

    /// <summary>
    /// Run scheduled incremental detection (hourly)
    /// </summary>
    Task RunScheduledIncrementalDetectionAsync();

    /// <summary>
    /// Process a schema change event from the queue
    /// </summary>
    Task ProcessSchemaChangeEventAsync(SchemaChangeEvent changeEvent);
}

using Enterprise.Documentation.Core.Domain.ValueObjects;

namespace Enterprise.Documentation.Core.Application.DTOs.Search;

/// <summary>
/// Main search request DTO with support for all 5 routing paths.
/// </summary>
public record SearchRequest(
    string Query,
    string UserId,
    SearchOptions? Options = null);

/// <summary>
/// Options for customizing search behavior.
/// </summary>
public record SearchOptions(
    int MaxResults = 20,
    bool IncludeLineage = false,
    bool IncludePiiFlows = false,
    bool EnableReranking = true,
    decimal MinConfidence = 0.5m,
    List<string>? FilterDatabases = null,
    List<string>? FilterObjectTypes = null,
    List<string>? FilterCategories = null,
    RoutingPath? ForceRoutingPath = null);

/// <summary>
/// Comprehensive search response with results, suggestions, and metadata.
/// </summary>
public record SearchResponse(
    Guid QueryId,
    string OriginalQuery,
    string? ExpandedQuery,
    RoutingPath RoutingPath,
    List<SearchResultItem> Results,
    List<FollowUpSuggestion> FollowUpSuggestions,
    SearchMetadata Metadata);

/// <summary>
/// Individual search result item with scores and metadata.
/// </summary>
public record SearchResultItem(
    string DocumentId,
    string ObjectType,
    string ObjectName,
    string? SchemaName,
    string? DatabaseName,
    string? Description,
    string? BusinessPurpose,
    string? Category,
    string? DataClassification,
    RelevanceScore Score,
    List<string>? MatchedTerms = null,
    LineageInfo? Lineage = null,
    PiiInfo? PiiInfo = null);

/// <summary>
/// Lineage information for a search result.
/// </summary>
public record LineageInfo(
    int UpstreamCount,
    int DownstreamCount,
    List<string>? ImmediateUpstream = null,
    List<string>? ImmediateDownstream = null);

/// <summary>
/// PII information for a search result.
/// </summary>
public record PiiInfo(
    bool IsPii,
    string? PiiType,
    int FlowPathCount);

/// <summary>
/// Search metadata for analytics and debugging.
/// </summary>
public record SearchMetadata(
    int TotalCandidates,
    int FilteredResults,
    TimeSpan ProcessingTime,
    Dictionary<string, TimeSpan> StageTimings,
    bool CacheHit,
    string? ClassificationReason);

/// <summary>
/// Follow-up suggestion for iterative search refinement.
/// </summary>
public record FollowUpSuggestion(
    string SuggestionText,
    string SuggestionType,
    decimal Confidence,
    string? Rationale = null);

/// <summary>
/// Vector search result from Qdrant.
/// </summary>
public record VectorSearchResult(
    string DocumentId,
    float Score,
    string CollectionName,
    string PointId,
    Dictionary<string, object>? Payload = null);

/// <summary>
/// Graph search result from in-memory graph.
/// </summary>
public record GraphSearchResult(
    string NodeId,
    string NodeType,
    string ObjectName,
    string? SchemaName,
    string? DatabaseName,
    int Depth,
    string? RelationshipType,
    string? ParentNodeId,
    Dictionary<string, object>? Properties = null);

/// <summary>
/// Query classification result from the classifier.
/// </summary>
public record QueryClassificationResult(
    string Query,
    RoutingPath PrimaryPath,
    RoutingPath? SecondaryPath,
    QueryComplexity Complexity,
    decimal Confidence,
    string? Reasoning,
    List<string>? ExtractedEntities = null,
    Dictionary<string, string>? ExtractedFilters = null);

/// <summary>
/// Result of combining multiple search paths using RRF.
/// </summary>
public record FusedSearchResult(
    string DocumentId,
    decimal FusedScore,
    Dictionary<string, decimal> PathScores,
    int FinalRank);

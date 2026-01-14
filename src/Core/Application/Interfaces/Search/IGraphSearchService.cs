using Enterprise.Documentation.Core.Application.DTOs.Search;
using Enterprise.Documentation.Core.Domain.ValueObjects;

namespace Enterprise.Documentation.Core.Application.Interfaces.Search;

/// <summary>
/// Graph search service for relationship-based queries (GraphRAG).
/// Uses in-memory graph rebuilt from SQL on startup.
/// </summary>
public interface IGraphSearchService
{
    /// <summary>
    /// Find all objects that depend on the specified object.
    /// </summary>
    Task<List<GraphSearchResult>> FindDependentsAsync(
        string nodeId,
        int maxDepth = 3,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find all objects that the specified object depends on.
    /// </summary>
    Task<List<GraphSearchResult>> FindDependenciesAsync(
        string nodeId,
        int maxDepth = 3,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find the lineage path between two objects.
    /// </summary>
    Task<List<GraphSearchResult>> FindLineagePathAsync(
        string sourceNodeId,
        string targetNodeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Trace PII flow paths from a source column.
    /// Critical for compliance tracking.
    /// </summary>
    Task<List<PiiFlowPath>> TracePiiFlowAsync(
        string sourceNodeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find all PII columns and their flow paths.
    /// </summary>
    Task<List<PiiFlowPath>> GetAllPiiFlowsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get nodes by type (Table, Column, Procedure, etc.)
    /// </summary>
    Task<List<GraphSearchResult>> GetNodesByTypeAsync(
        string nodeType,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rebuild the in-memory graph from SQL database.
    /// </summary>
    Task RebuildGraphAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get graph statistics.
    /// </summary>
    Task<GraphStats> GetGraphStatsAsync(CancellationToken cancellationToken = default);
}

public record GraphStats(int NodeCount, int EdgeCount, int PiiFlowCount, DateTime LastRebuilt);

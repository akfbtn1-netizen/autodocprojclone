using System.Collections.Concurrent;
using Dapper;
using Enterprise.Documentation.Core.Application.DTOs.Search;
using Enterprise.Documentation.Core.Application.Interfaces.Search;
using Enterprise.Documentation.Core.Domain.ValueObjects;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Enterprise.Documentation.Infrastructure.Search;

/// <summary>
/// In-memory graph service for GraphRAG operations.
/// Rebuilds graph from SQL on startup, supports lineage traversal and PII flow tracking.
/// </summary>
public class InMemoryGraphService : IGraphSearchService
{
    private readonly string _connectionString;
    private readonly ILogger<InMemoryGraphService> _logger;

    // In-memory graph structure
    private ConcurrentDictionary<string, GraphNode> _nodes = new();
    private ConcurrentDictionary<string, List<GraphEdge>> _outgoingEdges = new();
    private ConcurrentDictionary<string, List<GraphEdge>> _incomingEdges = new();
    private ConcurrentDictionary<string, List<PiiFlowPath>> _piiFlows = new();

    private DateTime _lastRebuilt = DateTime.MinValue;
    private readonly SemaphoreSlim _rebuildLock = new(1, 1);

    public InMemoryGraphService(
        IConfiguration configuration,
        ILogger<InMemoryGraphService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new ArgumentNullException(nameof(configuration), "Connection string required");
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<GraphSearchResult>> FindDependentsAsync(
        string nodeId,
        int maxDepth = 3,
        CancellationToken cancellationToken = default)
    {
        var results = new List<GraphSearchResult>();
        var visited = new HashSet<string>();

        await TraverseAsync(nodeId, maxDepth, 0, visited, results, TraversalDirection.Downstream, null, cancellationToken);

        return results;
    }

    /// <inheritdoc />
    public async Task<List<GraphSearchResult>> FindDependenciesAsync(
        string nodeId,
        int maxDepth = 3,
        CancellationToken cancellationToken = default)
    {
        var results = new List<GraphSearchResult>();
        var visited = new HashSet<string>();

        await TraverseAsync(nodeId, maxDepth, 0, visited, results, TraversalDirection.Upstream, null, cancellationToken);

        return results;
    }

    /// <inheritdoc />
    public async Task<List<GraphSearchResult>> FindLineagePathAsync(
        string sourceNodeId,
        string targetNodeId,
        CancellationToken cancellationToken = default)
    {
        // BFS to find shortest path
        var queue = new Queue<(string NodeId, List<string> Path)>();
        var visited = new HashSet<string>();

        queue.Enqueue((sourceNodeId, new List<string> { sourceNodeId }));
        visited.Add(sourceNodeId);

        while (queue.Count > 0)
        {
            var (currentId, path) = queue.Dequeue();

            if (currentId == targetNodeId)
            {
                // Found path - convert to results
                return await Task.FromResult(path.Select((id, idx) =>
                {
                    var node = _nodes.GetValueOrDefault(id);
                    return new GraphSearchResult(
                        NodeId: id,
                        NodeType: node?.ObjectType ?? "Unknown",
                        ObjectName: node?.ObjectName ?? id,
                        SchemaName: node?.SchemaName,
                        DatabaseName: node?.DatabaseName,
                        Depth: idx,
                        RelationshipType: idx > 0 ? "LINEAGE" : null,
                        ParentNodeId: idx > 0 ? path[idx - 1] : null,
                        Properties: null);
                }).ToList());
            }

            // Explore both directions
            var neighbors = GetNeighbors(currentId);
            foreach (var neighborId in neighbors)
            {
                if (!visited.Contains(neighborId))
                {
                    visited.Add(neighborId);
                    var newPath = new List<string>(path) { neighborId };
                    queue.Enqueue((neighborId, newPath));
                }
            }
        }

        return new List<GraphSearchResult>(); // No path found
    }

    /// <inheritdoc />
    public async Task<List<PiiFlowPath>> TracePiiFlowAsync(
        string sourceNodeId,
        CancellationToken cancellationToken = default)
    {
        if (_piiFlows.TryGetValue(sourceNodeId, out var flows))
        {
            return await Task.FromResult(flows);
        }

        // If not cached, compute PII flows
        var piiFlows = new List<PiiFlowPath>();
        var node = _nodes.GetValueOrDefault(sourceNodeId);

        if (node == null || !node.IsPii)
        {
            return piiFlows;
        }

        // Trace downstream to find all PII destinations
        var visited = new HashSet<string>();
        var pathNodes = new List<string>();

        await TracePiiDownstreamAsync(sourceNodeId, visited, pathNodes, piiFlows, cancellationToken);

        // Cache the result
        _piiFlows[sourceNodeId] = piiFlows;

        return piiFlows;
    }

    /// <inheritdoc />
    public async Task<List<PiiFlowPath>> GetAllPiiFlowsAsync(CancellationToken cancellationToken = default)
    {
        var allFlows = new List<PiiFlowPath>();

        // Find all PII source nodes
        var piiNodes = _nodes.Values.Where(n => n.IsPii).ToList();

        foreach (var piiNode in piiNodes)
        {
            var flows = await TracePiiFlowAsync(piiNode.NodeId, cancellationToken);
            allFlows.AddRange(flows);
        }

        return allFlows;
    }

    /// <inheritdoc />
    public async Task<List<GraphSearchResult>> GetNodesByTypeAsync(
        string nodeType,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(_nodes.Values
            .Where(n => n.ObjectType.Equals(nodeType, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .Select(n => new GraphSearchResult(
                NodeId: n.NodeId,
                NodeType: n.ObjectType,
                ObjectName: n.ObjectName,
                SchemaName: n.SchemaName,
                DatabaseName: n.DatabaseName,
                Depth: 0,
                RelationshipType: null,
                ParentNodeId: null,
                Properties: n.Properties))
            .ToList());
    }

    /// <inheritdoc />
    public async Task RebuildGraphAsync(CancellationToken cancellationToken = default)
    {
        await _rebuildLock.WaitAsync(cancellationToken);

        try
        {
            _logger.LogInformation("Starting graph rebuild from SQL...");
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var newNodes = new ConcurrentDictionary<string, GraphNode>();
            var newOutgoing = new ConcurrentDictionary<string, List<GraphEdge>>();
            var newIncoming = new ConcurrentDictionary<string, List<GraphEdge>>();

            await using var conn = new SqlConnection(_connectionString);

            // Load nodes from GraphNodes table
            var nodes = await conn.QueryAsync<GraphNodeRecord>(
                @"SELECT NodeId, NodeType, ObjectName, SchemaName, DatabaseName,
                         IsPii, PiiType, Properties
                  FROM DaQa.GraphNodes",
                cancellationToken);

            foreach (var node in nodes)
            {
                var graphNode = new GraphNode(
                    node.NodeId,
                    node.NodeType,
                    node.ObjectName,
                    node.SchemaName,
                    node.DatabaseName,
                    node.IsPii,
                    node.PiiType,
                    DeserializeProperties(node.Properties));

                newNodes[node.NodeId] = graphNode;
            }

            // Load edges from GraphEdges table
            var edges = await conn.QueryAsync<GraphEdgeRecord>(
                @"SELECT SourceNodeId, TargetNodeId, RelationshipType, Weight
                  FROM DaQa.GraphEdges",
                cancellationToken);

            foreach (var edge in edges)
            {
                var graphEdge = new GraphEdge(
                    edge.SourceNodeId,
                    edge.TargetNodeId,
                    edge.RelationshipType,
                    edge.Weight);

                // Add to outgoing edges
                newOutgoing.AddOrUpdate(
                    edge.SourceNodeId,
                    _ => new List<GraphEdge> { graphEdge },
                    (_, list) => { list.Add(graphEdge); return list; });

                // Add to incoming edges
                newIncoming.AddOrUpdate(
                    edge.TargetNodeId,
                    _ => new List<GraphEdge> { graphEdge },
                    (_, list) => { list.Add(graphEdge); return list; });
            }

            // Swap in new graph
            _nodes = newNodes;
            _outgoingEdges = newOutgoing;
            _incomingEdges = newIncoming;
            _piiFlows = new ConcurrentDictionary<string, List<PiiFlowPath>>();
            _lastRebuilt = DateTime.UtcNow;

            sw.Stop();
            _logger.LogInformation(
                "Graph rebuild complete: {NodeCount} nodes, {EdgeCount} edges in {Elapsed}ms",
                newNodes.Count, edges.Count(), sw.ElapsedMilliseconds);
        }
        finally
        {
            _rebuildLock.Release();
        }
    }

    /// <inheritdoc />
    public Task<GraphStats> GetGraphStatsAsync(CancellationToken cancellationToken = default)
    {
        var edgeCount = _outgoingEdges.Values.Sum(list => list.Count);
        var piiFlowCount = _piiFlows.Values.Sum(list => list.Count);

        return Task.FromResult(new GraphStats(
            _nodes.Count,
            edgeCount,
            piiFlowCount,
            _lastRebuilt));
    }

    private async Task TraverseAsync(
        string nodeId,
        int maxDepth,
        int currentDepth,
        HashSet<string> visited,
        List<GraphSearchResult> results,
        TraversalDirection direction,
        string? parentId,
        CancellationToken cancellationToken)
    {
        if (currentDepth > maxDepth || visited.Contains(nodeId))
            return;

        visited.Add(nodeId);

        if (_nodes.TryGetValue(nodeId, out var node))
        {
            // Don't add the starting node (depth 0) to results
            if (currentDepth > 0)
            {
                results.Add(new GraphSearchResult(
                    NodeId: node.NodeId,
                    NodeType: node.ObjectType,
                    ObjectName: node.ObjectName,
                    SchemaName: node.SchemaName,
                    DatabaseName: node.DatabaseName,
                    Depth: currentDepth,
                    RelationshipType: direction == TraversalDirection.Downstream ? "DEPENDS_ON" : "DEPENDENCY_OF",
                    ParentNodeId: parentId,
                    Properties: node.Properties));
            }

            // Get edges based on direction
            var edges = direction == TraversalDirection.Downstream
                ? _outgoingEdges.GetValueOrDefault(nodeId, new List<GraphEdge>())
                : _incomingEdges.GetValueOrDefault(nodeId, new List<GraphEdge>());

            foreach (var edge in edges)
            {
                var nextNodeId = direction == TraversalDirection.Downstream
                    ? edge.TargetNodeId
                    : edge.SourceNodeId;

                await TraverseAsync(
                    nextNodeId,
                    maxDepth,
                    currentDepth + 1,
                    visited,
                    results,
                    direction,
                    nodeId,
                    cancellationToken);
            }
        }
    }

    private async Task TracePiiDownstreamAsync(
        string nodeId,
        HashSet<string> visited,
        List<string> currentPath,
        List<PiiFlowPath> flows,
        CancellationToken cancellationToken)
    {
        if (visited.Contains(nodeId))
            return;

        visited.Add(nodeId);
        currentPath.Add(nodeId);

        var outgoing = _outgoingEdges.GetValueOrDefault(nodeId, new List<GraphEdge>());

        if (outgoing.Count == 0 && currentPath.Count > 1)
        {
            // Leaf node - this is an endpoint of PII flow
            var sourceNode = _nodes.GetValueOrDefault(currentPath[0]);
            var destNode = _nodes.GetValueOrDefault(nodeId);

            if (sourceNode != null && destNode != null)
            {
                flows.Add(PiiFlowPath.Create(
                    sourceNode.NodeId,
                    sourceNode.PiiType ?? "PII",
                    destNode.NodeId,
                    new List<string>(currentPath)));
            }
        }
        else
        {
            foreach (var edge in outgoing)
            {
                await TracePiiDownstreamAsync(
                    edge.TargetNodeId,
                    visited,
                    currentPath,
                    flows,
                    cancellationToken);
            }
        }

        currentPath.RemoveAt(currentPath.Count - 1);
        visited.Remove(nodeId);
    }

    private IEnumerable<string> GetNeighbors(string nodeId)
    {
        var outgoing = _outgoingEdges.GetValueOrDefault(nodeId, new List<GraphEdge>());
        var incoming = _incomingEdges.GetValueOrDefault(nodeId, new List<GraphEdge>());

        return outgoing.Select(e => e.TargetNodeId)
            .Concat(incoming.Select(e => e.SourceNodeId))
            .Distinct();
    }

    private static Dictionary<string, object>? DeserializeProperties(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        }
        catch
        {
            return null;
        }
    }

    private enum TraversalDirection
    {
        Upstream,
        Downstream
    }

    // Internal record types for Dapper mapping
    private record GraphNodeRecord(
        string NodeId,
        string NodeType,
        string ObjectName,
        string? SchemaName,
        string? DatabaseName,
        bool IsPii,
        string? PiiType,
        string? Properties);

    private record GraphEdgeRecord(
        string SourceNodeId,
        string TargetNodeId,
        string RelationshipType,
        decimal Weight);

    // Internal graph node type
    private record GraphNode(
        string NodeId,
        string ObjectType,
        string ObjectName,
        string? SchemaName,
        string? DatabaseName,
        bool IsPii,
        string? PiiType,
        Dictionary<string, object>? Properties);

    // Internal graph edge type
    private record GraphEdge(
        string SourceNodeId,
        string TargetNodeId,
        string RelationshipType,
        decimal Weight);
}

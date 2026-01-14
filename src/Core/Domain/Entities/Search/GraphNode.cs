namespace Enterprise.Documentation.Core.Domain.Entities.Search;

/// <summary>
/// Graph node for in-memory GraphRAG.
/// Represents database objects (tables, columns, procedures, etc.)
/// </summary>
public class GraphNode : BaseEntity
{
    public string NodeId { get; private set; } = string.Empty;
    public string NodeType { get; private set; } = string.Empty;
    public string NodeName { get; private set; } = string.Empty;
    public string? SchemaName { get; private set; }
    public string? DatabaseName { get; private set; }
    public string? Properties { get; private set; } // JSON: {classification, pii_type, owner}
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<GraphEdge> _outgoingEdges = new();
    public IReadOnlyCollection<GraphEdge> OutgoingEdges => _outgoingEdges.AsReadOnly();

    private readonly List<GraphEdge> _incomingEdges = new();
    public IReadOnlyCollection<GraphEdge> IncomingEdges => _incomingEdges.AsReadOnly();

    private GraphNode() { } // EF Core

    public static GraphNode Create(
        string nodeId,
        string nodeType,
        string nodeName,
        string? schemaName = null,
        string? databaseName = null,
        string? properties = null)
    {
        return new GraphNode
        {
            NodeId = nodeId,
            NodeType = nodeType,
            NodeName = nodeName,
            SchemaName = schemaName,
            DatabaseName = databaseName,
            Properties = properties,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void UpdateProperties(string properties)
    {
        Properties = properties;
        UpdatedAt = DateTime.UtcNow;
    }

    public string FullName => string.IsNullOrEmpty(SchemaName)
        ? NodeName
        : $"{SchemaName}.{NodeName}";
}

/// <summary>
/// Node type constants
/// </summary>
public static class NodeTypes
{
    public const string Table = "Table";
    public const string Column = "Column";
    public const string Procedure = "Procedure";
    public const string View = "View";
    public const string Function = "Function";
    public const string Index = "Index";
    public const string Constraint = "Constraint";
}

namespace Enterprise.Documentation.Core.Domain.Entities.Lineage;

/// <summary>
/// Represents a node in the lineage graph (table, column, procedure, view, or function).
/// Used for visualizing data flow and impact analysis.
/// </summary>
public class LineageNode
{
    public int Id { get; private set; }
    public string NodeId { get; private set; } = string.Empty;
    public LineageNodeType NodeType { get; private set; }
    public string? DatabaseName { get; private set; }
    public string SchemaName { get; private set; } = string.Empty;
    public string ObjectName { get; private set; } = string.Empty;
    public string? ColumnName { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public bool IsPiiNode { get; private set; }
    public string? PiiType { get; private set; }
    public DataClassification? DataClassification { get; private set; }
    public string? Properties { get; private set; }
    public int RiskScore { get; private set; }
    public int InDegree { get; private set; }
    public int OutDegree { get; private set; }
    public string? ClusterGroup { get; private set; }
    public int? GraphNodeId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<LineageEdge> _outgoingEdges = new();
    public IReadOnlyCollection<LineageEdge> OutgoingEdges => _outgoingEdges.AsReadOnly();

    private readonly List<LineageEdge> _incomingEdges = new();
    public IReadOnlyCollection<LineageEdge> IncomingEdges => _incomingEdges.AsReadOnly();

    private LineageNode() { } // EF Core

    public static LineageNode CreateTableNode(
        string schemaName,
        string tableName,
        string? databaseName = null)
    {
        var nodeId = BuildNodeId(databaseName, schemaName, tableName, null);
        return new LineageNode
        {
            NodeId = nodeId,
            NodeType = LineageNodeType.Table,
            DatabaseName = databaseName,
            SchemaName = schemaName,
            ObjectName = tableName,
            DisplayName = $"{schemaName}.{tableName}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static LineageNode CreateColumnNode(
        string schemaName,
        string tableName,
        string columnName,
        string? dataType = null,
        bool isPii = false,
        string? piiType = null,
        string? databaseName = null)
    {
        var nodeId = BuildNodeId(databaseName, schemaName, tableName, columnName);
        return new LineageNode
        {
            NodeId = nodeId,
            NodeType = LineageNodeType.Column,
            DatabaseName = databaseName,
            SchemaName = schemaName,
            ObjectName = tableName,
            ColumnName = columnName,
            DisplayName = $"{schemaName}.{tableName}.{columnName}",
            IsPiiNode = isPii,
            PiiType = piiType,
            Properties = dataType != null ? $"{{\"dataType\":\"{dataType}\"}}" : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static LineageNode CreateProcedureNode(
        string schemaName,
        string procedureName,
        string? databaseName = null)
    {
        var nodeId = BuildNodeId(databaseName, schemaName, procedureName, null);
        return new LineageNode
        {
            NodeId = nodeId,
            NodeType = LineageNodeType.Procedure,
            DatabaseName = databaseName,
            SchemaName = schemaName,
            ObjectName = procedureName,
            DisplayName = $"{schemaName}.{procedureName}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static LineageNode CreateViewNode(
        string schemaName,
        string viewName,
        string? databaseName = null)
    {
        var nodeId = BuildNodeId(databaseName, schemaName, viewName, null);
        return new LineageNode
        {
            NodeId = nodeId,
            NodeType = LineageNodeType.View,
            DatabaseName = databaseName,
            SchemaName = schemaName,
            ObjectName = viewName,
            DisplayName = $"{schemaName}.{viewName}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static LineageNode CreateFunctionNode(
        string schemaName,
        string functionName,
        string? databaseName = null)
    {
        var nodeId = BuildNodeId(databaseName, schemaName, functionName, null);
        return new LineageNode
        {
            NodeId = nodeId,
            NodeType = LineageNodeType.Function,
            DatabaseName = databaseName,
            SchemaName = schemaName,
            ObjectName = functionName,
            DisplayName = $"{schemaName}.{functionName}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void MarkAsPii(string piiType)
    {
        IsPiiNode = true;
        PiiType = piiType;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetDataClassification(DataClassification classification)
    {
        DataClassification = classification;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateRiskScore(int score)
    {
        RiskScore = score;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateDegrees(int inDegree, int outDegree)
    {
        InDegree = inDegree;
        OutDegree = outDegree;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetClusterGroup(string clusterGroup)
    {
        ClusterGroup = clusterGroup;
        UpdatedAt = DateTime.UtcNow;
    }

    public void LinkToGraphNode(int graphNodeId)
    {
        GraphNodeId = graphNodeId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateProperties(string properties)
    {
        Properties = properties;
        UpdatedAt = DateTime.UtcNow;
    }

    public string FullyQualifiedName => ColumnName != null
        ? $"{SchemaName}.{ObjectName}.{ColumnName}"
        : $"{SchemaName}.{ObjectName}";

    private static string BuildNodeId(string? database, string schema, string obj, string? column)
    {
        if (column != null)
            return $"{schema}.{obj}.{column}";
        return $"{schema}.{obj}";
    }
}

/// <summary>
/// Types of nodes in the lineage graph
/// </summary>
public enum LineageNodeType
{
    Table,
    Column,
    Procedure,
    View,
    Function
}

/// <summary>
/// Data classification levels for compliance
/// </summary>
public enum DataClassification
{
    Public,
    Internal,
    Confidential,
    Restricted
}

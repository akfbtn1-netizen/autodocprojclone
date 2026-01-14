namespace Enterprise.Documentation.Core.Domain.Entities.Lineage;

/// <summary>
/// Represents an edge/relationship in the lineage graph.
/// Connects nodes with specific operation types (READS, WRITES, PII_FLOW, etc.)
/// </summary>
public class LineageEdge
{
    public int Id { get; private set; }
    public Guid EdgeId { get; private set; }
    public string SourceNodeId { get; private set; } = string.Empty;
    public string TargetNodeId { get; private set; } = string.Empty;
    public LineageEdgeType EdgeType { get; private set; }
    public OperationType? OperationType { get; private set; }
    public string? TransformationHint { get; private set; }
    public string? SourceProcedure { get; private set; }
    public decimal Weight { get; private set; } = 1.0m;
    public bool IsPiiFlow { get; private set; }
    public string? Properties { get; private set; }
    public int? GraphEdgeId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Navigation properties
    public LineageNode? SourceNode { get; private set; }
    public LineageNode? TargetNode { get; private set; }

    private LineageEdge() { } // EF Core

    public static LineageEdge Create(
        string sourceNodeId,
        string targetNodeId,
        LineageEdgeType edgeType,
        OperationType? operationType = null,
        string? sourceProcedure = null,
        string? transformationHint = null,
        bool isPiiFlow = false)
    {
        return new LineageEdge
        {
            EdgeId = Guid.NewGuid(),
            SourceNodeId = sourceNodeId,
            TargetNodeId = targetNodeId,
            EdgeType = edgeType,
            OperationType = operationType,
            SourceProcedure = sourceProcedure,
            TransformationHint = transformationHint,
            IsPiiFlow = isPiiFlow,
            Weight = CalculateWeight(edgeType, operationType),
            CreatedAt = DateTime.UtcNow
        };
    }

    public static LineageEdge CreateReadEdge(
        string sourceColumn,
        string targetProcedure,
        string? transformationHint = null)
    {
        return Create(
            sourceColumn,
            targetProcedure,
            LineageEdgeType.Reads,
            Lineage.OperationType.Read,
            targetProcedure,
            transformationHint);
    }

    public static LineageEdge CreateWriteEdge(
        string sourceProcedure,
        string targetColumn,
        OperationType operationType,
        string? transformationHint = null)
    {
        return Create(
            sourceProcedure,
            targetColumn,
            LineageEdgeType.Writes,
            operationType,
            sourceProcedure,
            transformationHint);
    }

    public static LineageEdge CreateTransformEdge(
        string sourceColumn,
        string targetColumn,
        string sourceProcedure,
        string transformationExpression)
    {
        return Create(
            sourceColumn,
            targetColumn,
            LineageEdgeType.Transforms,
            null,
            sourceProcedure,
            transformationExpression);
    }

    public static LineageEdge CreatePiiFlowEdge(
        string sourceColumn,
        string targetColumn,
        string? sourceProcedure = null)
    {
        return Create(
            sourceColumn,
            targetColumn,
            LineageEdgeType.PiiFlow,
            null,
            sourceProcedure,
            null,
            isPiiFlow: true);
    }

    public void MarkAsPiiFlow()
    {
        IsPiiFlow = true;
    }

    public void SetWeight(decimal weight)
    {
        Weight = weight;
    }

    public void LinkToGraphEdge(int graphEdgeId)
    {
        GraphEdgeId = graphEdgeId;
    }

    public void UpdateProperties(string properties)
    {
        Properties = properties;
    }

    private static decimal CalculateWeight(LineageEdgeType edgeType, OperationType? opType)
    {
        // Weight based on operation type for risk assessment
        return opType switch
        {
            Lineage.OperationType.Read => 1.0m,
            Lineage.OperationType.Insert => 2.0m,
            Lineage.OperationType.Update => 3.0m,
            Lineage.OperationType.Delete => 5.0m,
            Lineage.OperationType.MergeUpdate => 3.0m,
            Lineage.OperationType.MergeInsert => 2.0m,
            _ => edgeType switch
            {
                LineageEdgeType.PiiFlow => 10.0m,
                LineageEdgeType.Transforms => 2.0m,
                _ => 1.0m
            }
        };
    }
}

/// <summary>
/// Types of edges in the lineage graph
/// </summary>
public enum LineageEdgeType
{
    Uses,       // General usage relationship
    Produces,   // Procedure produces output
    Transforms, // Column transformation (with expression)
    Reads,      // SELECT from column
    Writes,     // INSERT/UPDATE/DELETE to column
    PiiFlow,    // PII data movement (compliance critical)
    DependsOn,  // Object dependency
    Contains,   // Table contains column
    References  // Foreign key reference
}

/// <summary>
/// SQL operation types for column lineage
/// </summary>
public enum OperationType
{
    Read,
    Insert,
    Update,
    Delete,
    MergeUpdate,
    MergeInsert
}

// ═══════════════════════════════════════════════════════════════════════════
// Agent #4: Schema Change Detector - Main Entity
// Aggregate root for detected schema changes
// ═══════════════════════════════════════════════════════════════════════════
// TODO [4]: Integrate with Agent #3 Lineage for column-level impact queries
// TODO [4]: Add MassTransit event publishing for saga coordination

namespace Enterprise.Documentation.Core.Domain.Entities.SchemaChange;

/// <summary>
/// Aggregate root for a detected schema change.
/// Tracks the full lifecycle from detection to acknowledgment.
/// </summary>
public class SchemaChange
{
    public Guid ChangeId { get; private set; }

    // Change identification
    public string DatabaseName { get; private set; } = string.Empty;
    public string SchemaName { get; private set; } = string.Empty;
    public string ObjectName { get; private set; } = string.Empty;
    public ObjectType ObjectType { get; private set; }
    public ChangeType ChangeType { get; private set; }

    // Change details
    public string? ChangeDescription { get; private set; }
    public string? ChangedColumnsJson { get; private set; }
    public string? OldDefinition { get; private set; }
    public string? NewDefinition { get; private set; }
    public string? DdlStatement { get; private set; }

    // Detection metadata
    public DateTime DetectedAt { get; private set; }
    public DetectionMethod DetectedBy { get; private set; }
    public string? LoginName { get; private set; }
    public string? HostName { get; private set; }
    public string? ApplicationName { get; private set; }

    // Impact assessment
    public int ImpactScore { get; private set; }
    public RiskLevel RiskLevel { get; private set; }
    public int AffectedProcedures { get; private set; }
    public int AffectedViews { get; private set; }
    public int AffectedFunctions { get; private set; }
    public bool HasPiiColumns { get; private set; }
    public bool HasLineageDownstream { get; private set; }

    // Processing status
    public ProcessingStatus Status { get; private set; }
    public string? AcknowledgedBy { get; private set; }
    public DateTime? AcknowledgedAt { get; private set; }
    public string? AcknowledgementNotes { get; private set; }

    // Workflow integration
    public bool ApprovalRequired { get; private set; }
    public Guid? ApprovalWorkflowId { get; private set; }
    public bool DocumentationTriggered { get; private set; }
    public DateTime? DocumentationTriggeredAt { get; private set; }

    // Audit
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // Related entities
    private readonly List<ChangeImpact> _impacts = new();
    public IReadOnlyCollection<ChangeImpact> Impacts => _impacts.AsReadOnly();

    private readonly List<ColumnChange> _columnChanges = new();
    public IReadOnlyCollection<ColumnChange> ColumnChanges => _columnChanges.AsReadOnly();

    private SchemaChange() { } // EF Core / Dapper

    public static SchemaChange Create(
        string databaseName,
        string schemaName,
        string objectName,
        ObjectType objectType,
        ChangeType changeType,
        string? ddlStatement,
        DetectionMethod detectedBy,
        string? loginName = null)
    {
        return new SchemaChange
        {
            ChangeId = Guid.NewGuid(),
            DatabaseName = databaseName,
            SchemaName = schemaName,
            ObjectName = objectName,
            ObjectType = objectType,
            ChangeType = changeType,
            DdlStatement = ddlStatement,
            DetectedAt = DateTime.UtcNow,
            DetectedBy = detectedBy,
            LoginName = loginName,
            Status = ProcessingStatus.Pending,
            RiskLevel = RiskLevel.Low,
            ImpactScore = 0,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SetDefinitions(string? oldDefinition, string? newDefinition)
    {
        OldDefinition = oldDefinition;
        NewDefinition = newDefinition;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetDescription(string description)
    {
        ChangeDescription = description;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetDetectionContext(string? hostName, string? appName)
    {
        HostName = hostName;
        ApplicationName = appName;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssessImpact(
        int impactScore,
        RiskLevel riskLevel,
        int affectedProcs,
        int affectedViews,
        int affectedFuncs,
        bool hasPii,
        bool hasDownstream)
    {
        ImpactScore = Math.Min(impactScore, 100);
        RiskLevel = riskLevel;
        AffectedProcedures = affectedProcs;
        AffectedViews = affectedViews;
        AffectedFunctions = affectedFuncs;
        HasPiiColumns = hasPii;
        HasLineageDownstream = hasDownstream;
        Status = ProcessingStatus.Assessed;
        UpdatedAt = DateTime.UtcNow;

        // Auto-require approval for high-risk changes
        if (riskLevel is RiskLevel.High or RiskLevel.Critical)
        {
            ApprovalRequired = true;
        }
    }

    public void AddImpact(ChangeImpact impact)
    {
        _impacts.Add(impact);
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddColumnChange(ColumnChange columnChange)
    {
        _columnChanges.Add(columnChange);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Acknowledge(string acknowledgedBy, string? notes = null)
    {
        if (Status == ProcessingStatus.Acknowledged)
            throw new InvalidOperationException("Change already acknowledged");

        AcknowledgedBy = acknowledgedBy;
        AcknowledgedAt = DateTime.UtcNow;
        AcknowledgementNotes = notes;
        Status = ProcessingStatus.Acknowledged;
        UpdatedAt = DateTime.UtcNow;
    }

    public void TriggerDocumentation()
    {
        DocumentationTriggered = true;
        DocumentationTriggeredAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void LinkApprovalWorkflow(Guid approvalId)
    {
        ApprovalWorkflowId = approvalId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = ProcessingStatus.Failed;
        ChangeDescription = $"Processing failed: {errorMessage}";
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Fully qualified object name for display.
    /// </summary>
    public string FullObjectName => $"{SchemaName}.{ObjectName}";
}

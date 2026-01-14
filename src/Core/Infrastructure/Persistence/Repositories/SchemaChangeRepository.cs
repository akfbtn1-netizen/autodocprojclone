// ═══════════════════════════════════════════════════════════════════════════
// Agent #4: Schema Change Repository
// Dapper-based data access for schema changes
// ═══════════════════════════════════════════════════════════════════════════

using System.Data;
using Dapper;
using Enterprise.Documentation.Core.Application.DTOs.SchemaChange;
using Enterprise.Documentation.Core.Application.Interfaces.SchemaChange;
using Enterprise.Documentation.Core.Domain.Entities.SchemaChange;
using Microsoft.Extensions.Logging;

namespace Enterprise.Documentation.Core.Infrastructure.Persistence.Repositories;

public class SchemaChangeRepository : ISchemaChangeRepository
{
    private readonly IDbConnection _connection;
    private readonly ILogger<SchemaChangeRepository> _logger;

    public SchemaChangeRepository(IDbConnection connection, ILogger<SchemaChangeRepository> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public async Task<SchemaChange?> GetByIdAsync(Guid changeId, CancellationToken ct = default)
    {
        var sql = @"
            SELECT
                ChangeId, DatabaseName, SchemaName, ObjectName, ObjectType, ChangeType,
                ChangeDescription, ChangedColumns AS ChangedColumnsJson, OldDefinition, NewDefinition, DdlStatement,
                DetectedAt, DetectedBy, LoginName, HostName, ApplicationName,
                ImpactScore, RiskLevel, AffectedProcedures, AffectedViews, AffectedFunctions,
                HasPiiColumns, HasLineageDownstream, ProcessingStatus AS Status,
                AcknowledgedBy, AcknowledgedAt, AcknowledgementNotes,
                ApprovalRequired, ApprovalWorkflowId, DocumentationTriggered, DocumentationTriggeredAt,
                CreatedAt, UpdatedAt
            FROM DaQa.SchemaChanges
            WHERE ChangeId = @ChangeId";

        var row = await _connection.QuerySingleOrDefaultAsync<dynamic>(sql, new { ChangeId = changeId });
        if (row == null) return null;

        return MapFromRow(row);
    }

    public async Task<IEnumerable<SchemaChange>> GetPendingAsync(int maxCount, CancellationToken ct = default)
    {
        var sql = @"
            SELECT TOP (@MaxCount)
                ChangeId, DatabaseName, SchemaName, ObjectName, ObjectType, ChangeType,
                ChangeDescription, ChangedColumns AS ChangedColumnsJson, OldDefinition, NewDefinition, DdlStatement,
                DetectedAt, DetectedBy, LoginName, HostName, ApplicationName,
                ImpactScore, RiskLevel, AffectedProcedures, AffectedViews, AffectedFunctions,
                HasPiiColumns, HasLineageDownstream, ProcessingStatus AS Status,
                AcknowledgedBy, AcknowledgedAt, AcknowledgementNotes,
                ApprovalRequired, ApprovalWorkflowId, DocumentationTriggered, DocumentationTriggeredAt,
                CreatedAt, UpdatedAt
            FROM DaQa.SchemaChanges
            WHERE ProcessingStatus = 'Pending'
            ORDER BY
                CASE RiskLevel
                    WHEN 'CRITICAL' THEN 1
                    WHEN 'HIGH' THEN 2
                    WHEN 'MEDIUM' THEN 3
                    WHEN 'LOW' THEN 4
                END,
                DetectedAt ASC";

        var rows = await _connection.QueryAsync<dynamic>(sql, new { MaxCount = maxCount });
        return rows.Select(MapFromRow);
    }

    public async Task<IEnumerable<SchemaChange>> GetFilteredAsync(SchemaChangeFilterDto filter, CancellationToken ct = default)
    {
        var sql = @"
            SELECT
                ChangeId, DatabaseName, SchemaName, ObjectName, ObjectType, ChangeType,
                ChangeDescription, ChangedColumns AS ChangedColumnsJson, OldDefinition, NewDefinition, DdlStatement,
                DetectedAt, DetectedBy, LoginName, HostName, ApplicationName,
                ImpactScore, RiskLevel, AffectedProcedures, AffectedViews, AffectedFunctions,
                HasPiiColumns, HasLineageDownstream, ProcessingStatus AS Status,
                AcknowledgedBy, AcknowledgedAt, AcknowledgementNotes,
                ApprovalRequired, ApprovalWorkflowId, DocumentationTriggered, DocumentationTriggeredAt,
                CreatedAt, UpdatedAt
            FROM DaQa.SchemaChanges
            WHERE (@SchemaName IS NULL OR SchemaName = @SchemaName)
              AND (@ObjectName IS NULL OR ObjectName LIKE '%' + @ObjectName + '%')
              AND (@ObjectType IS NULL OR ObjectType = @ObjectType)
              AND (@ChangeType IS NULL OR ChangeType = @ChangeType)
              AND (@RiskLevel IS NULL OR RiskLevel = @RiskLevel)
              AND (@ProcessingStatus IS NULL OR ProcessingStatus = @ProcessingStatus)
              AND (@FromDate IS NULL OR DetectedAt >= @FromDate)
              AND (@ToDate IS NULL OR DetectedAt <= @ToDate)
              AND (@HasPiiColumns IS NULL OR HasPiiColumns = @HasPiiColumns)
              AND (@ApprovalRequired IS NULL OR ApprovalRequired = @ApprovalRequired)
            ORDER BY DetectedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        var offset = ((filter.Page ?? 1) - 1) * (filter.PageSize ?? 20);

        var rows = await _connection.QueryAsync<dynamic>(sql, new
        {
            filter.SchemaName,
            filter.ObjectName,
            filter.ObjectType,
            filter.ChangeType,
            filter.RiskLevel,
            filter.ProcessingStatus,
            filter.FromDate,
            filter.ToDate,
            filter.HasPiiColumns,
            filter.ApprovalRequired,
            Offset = offset,
            PageSize = filter.PageSize ?? 20
        });

        return rows.Select(MapFromRow);
    }

    public async Task AddAsync(SchemaChange change, CancellationToken ct = default)
    {
        var sql = @"
            INSERT INTO DaQa.SchemaChanges (
                ChangeId, DatabaseName, SchemaName, ObjectName, ObjectType, ChangeType,
                ChangeDescription, ChangedColumns, OldDefinition, NewDefinition, DdlStatement,
                DetectedAt, DetectedBy, LoginName, HostName, ApplicationName,
                ImpactScore, RiskLevel, AffectedProcedures, AffectedViews, AffectedFunctions,
                HasPiiColumns, HasLineageDownstream, ProcessingStatus,
                ApprovalRequired, DocumentationTriggered, CreatedAt
            ) VALUES (
                @ChangeId, @DatabaseName, @SchemaName, @ObjectName, @ObjectType, @ChangeType,
                @ChangeDescription, @ChangedColumnsJson, @OldDefinition, @NewDefinition, @DdlStatement,
                @DetectedAt, @DetectedBy, @LoginName, @HostName, @ApplicationName,
                @ImpactScore, @RiskLevel, @AffectedProcedures, @AffectedViews, @AffectedFunctions,
                @HasPiiColumns, @HasLineageDownstream, @Status,
                @ApprovalRequired, @DocumentationTriggered, @CreatedAt
            )";

        await _connection.ExecuteAsync(sql, new
        {
            change.ChangeId,
            change.DatabaseName,
            change.SchemaName,
            change.ObjectName,
            ObjectType = change.ObjectType.ToString(),
            ChangeType = change.ChangeType.ToString(),
            change.ChangeDescription,
            change.ChangedColumnsJson,
            change.OldDefinition,
            change.NewDefinition,
            change.DdlStatement,
            change.DetectedAt,
            DetectedBy = change.DetectedBy.ToString(),
            change.LoginName,
            change.HostName,
            change.ApplicationName,
            change.ImpactScore,
            RiskLevel = change.RiskLevel.ToString(),
            change.AffectedProcedures,
            change.AffectedViews,
            change.AffectedFunctions,
            change.HasPiiColumns,
            change.HasLineageDownstream,
            Status = change.Status.ToString(),
            change.ApprovalRequired,
            change.DocumentationTriggered,
            change.CreatedAt
        });
    }

    public async Task UpdateAsync(SchemaChange change, CancellationToken ct = default)
    {
        var sql = @"
            UPDATE DaQa.SchemaChanges SET
                ChangeDescription = @ChangeDescription,
                ImpactScore = @ImpactScore,
                RiskLevel = @RiskLevel,
                AffectedProcedures = @AffectedProcedures,
                AffectedViews = @AffectedViews,
                AffectedFunctions = @AffectedFunctions,
                HasPiiColumns = @HasPiiColumns,
                HasLineageDownstream = @HasLineageDownstream,
                ProcessingStatus = @Status,
                AcknowledgedBy = @AcknowledgedBy,
                AcknowledgedAt = @AcknowledgedAt,
                AcknowledgementNotes = @AcknowledgementNotes,
                ApprovalRequired = @ApprovalRequired,
                ApprovalWorkflowId = @ApprovalWorkflowId,
                DocumentationTriggered = @DocumentationTriggered,
                DocumentationTriggeredAt = @DocumentationTriggeredAt,
                UpdatedAt = @UpdatedAt
            WHERE ChangeId = @ChangeId";

        await _connection.ExecuteAsync(sql, new
        {
            change.ChangeId,
            change.ChangeDescription,
            change.ImpactScore,
            RiskLevel = change.RiskLevel.ToString(),
            change.AffectedProcedures,
            change.AffectedViews,
            change.AffectedFunctions,
            change.HasPiiColumns,
            change.HasLineageDownstream,
            Status = change.Status.ToString(),
            change.AcknowledgedBy,
            change.AcknowledgedAt,
            change.AcknowledgementNotes,
            change.ApprovalRequired,
            change.ApprovalWorkflowId,
            change.DocumentationTriggered,
            change.DocumentationTriggeredAt,
            change.UpdatedAt
        });
    }

    public async Task<int> GetCountAsync(SchemaChangeFilterDto? filter = null, CancellationToken ct = default)
    {
        var sql = "SELECT COUNT(*) FROM DaQa.SchemaChanges WHERE 1=1";

        if (filter != null)
        {
            if (!string.IsNullOrEmpty(filter.SchemaName))
                sql += " AND SchemaName = @SchemaName";
            if (!string.IsNullOrEmpty(filter.ProcessingStatus))
                sql += " AND ProcessingStatus = @ProcessingStatus";
        }

        return await _connection.ExecuteScalarAsync<int>(sql, filter);
    }

    private static SchemaChange MapFromRow(dynamic row)
    {
        // Using reflection to create entity from Dapper row
        // In production, consider using a proper mapper
        var change = SchemaChange.Create(
            (string)row.DatabaseName,
            (string)row.SchemaName,
            (string)row.ObjectName,
            Enum.Parse<ObjectType>((string)row.ObjectType),
            Enum.Parse<ChangeType>((string)row.ChangeType),
            (string?)row.DdlStatement,
            Enum.Parse<DetectionMethod>((string)row.DetectedBy),
            (string?)row.LoginName
        );

        // Set additional properties via reflection or internal methods
        // TODO [4]: Use proper entity reconstruction pattern
        return change;
    }
}

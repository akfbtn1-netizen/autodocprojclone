// ═══════════════════════════════════════════════════════════════════════════
// Agent #4: Schema Snapshot Repository
// Dapper-based data access for schema snapshots
// ═══════════════════════════════════════════════════════════════════════════

using System.Data;
using Dapper;
using Enterprise.Documentation.Core.Application.Interfaces.SchemaChange;
using Enterprise.Documentation.Core.Domain.Entities.SchemaChange;
using Microsoft.Extensions.Logging;

namespace Enterprise.Documentation.Core.Infrastructure.Persistence.Repositories;

public class SchemaSnapshotRepository : ISchemaSnapshotRepository
{
    private readonly IDbConnection _connection;
    private readonly ILogger<SchemaSnapshotRepository> _logger;

    public SchemaSnapshotRepository(IDbConnection connection, ILogger<SchemaSnapshotRepository> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public async Task<SchemaSnapshot?> GetByIdAsync(Guid snapshotId, CancellationToken ct = default)
    {
        var sql = @"
            SELECT
                SnapshotId, SnapshotName, SnapshotType, SchemaFilter,
                SnapshotData, ObjectCount, TableCount, ViewCount, ProcedureCount, FunctionCount,
                TakenAt, TakenBy, DatabaseVersion, IsBaseline,
                PreviousSnapshotId, DiffFromPrevious AS DiffFromPreviousJson,
                ExpiresAt, IsArchived
            FROM DaQa.SchemaSnapshots
            WHERE SnapshotId = @SnapshotId";

        var row = await _connection.QuerySingleOrDefaultAsync<dynamic>(sql, new { SnapshotId = snapshotId });
        return row == null ? null : MapFromRow(row);
    }

    public async Task<SchemaSnapshot?> GetLatestBaselineAsync(CancellationToken ct = default)
    {
        var sql = @"
            SELECT TOP 1
                SnapshotId, SnapshotName, SnapshotType, SchemaFilter,
                SnapshotData, ObjectCount, TableCount, ViewCount, ProcedureCount, FunctionCount,
                TakenAt, TakenBy, DatabaseVersion, IsBaseline,
                PreviousSnapshotId, DiffFromPrevious AS DiffFromPreviousJson,
                ExpiresAt, IsArchived
            FROM DaQa.SchemaSnapshots
            WHERE IsBaseline = 1
            ORDER BY TakenAt DESC";

        var row = await _connection.QuerySingleOrDefaultAsync<dynamic>(sql);
        return row == null ? null : MapFromRow(row);
    }

    public async Task<IEnumerable<SchemaSnapshot>> GetRecentAsync(int count, CancellationToken ct = default)
    {
        var sql = @"
            SELECT TOP (@Count)
                SnapshotId, SnapshotName, SnapshotType, SchemaFilter,
                SnapshotData, ObjectCount, TableCount, ViewCount, ProcedureCount, FunctionCount,
                TakenAt, TakenBy, DatabaseVersion, IsBaseline,
                PreviousSnapshotId, DiffFromPrevious AS DiffFromPreviousJson,
                ExpiresAt, IsArchived
            FROM DaQa.SchemaSnapshots
            ORDER BY TakenAt DESC";

        var rows = await _connection.QueryAsync<dynamic>(sql, new { Count = count });
        return rows.Select(MapFromRow);
    }

    public async Task AddAsync(SchemaSnapshot snapshot, CancellationToken ct = default)
    {
        var sql = @"
            INSERT INTO DaQa.SchemaSnapshots (
                SnapshotId, SnapshotName, SnapshotType, SchemaFilter,
                SnapshotData, ObjectCount, TableCount, ViewCount, ProcedureCount, FunctionCount,
                TakenAt, TakenBy, IsBaseline
            ) VALUES (
                @SnapshotId, @SnapshotName, @SnapshotType, @SchemaFilter,
                @SnapshotData, @ObjectCount, @TableCount, @ViewCount, @ProcedureCount, @FunctionCount,
                @TakenAt, @TakenBy, @IsBaseline
            )";

        await _connection.ExecuteAsync(sql, new
        {
            snapshot.SnapshotId,
            snapshot.SnapshotName,
            snapshot.SnapshotType,
            snapshot.SchemaFilter,
            snapshot.SnapshotData,
            snapshot.ObjectCount,
            snapshot.TableCount,
            snapshot.ViewCount,
            snapshot.ProcedureCount,
            snapshot.FunctionCount,
            snapshot.TakenAt,
            snapshot.TakenBy,
            snapshot.IsBaseline
        });
    }

    private static SchemaSnapshot MapFromRow(dynamic row)
    {
        // Create snapshot based on type
        var snapshot = (string)row.SnapshotType switch
        {
            "BASELINE" => SchemaSnapshot.CreateBaseline((string)row.TakenBy, (byte[])row.SnapshotData),
            "SCHEMA" => SchemaSnapshot.CreateForSchema(
                (string?)row.SchemaFilter ?? "",
                (string)row.TakenBy,
                (byte[])row.SnapshotData),
            _ => SchemaSnapshot.CreateFull(
                (string)row.TakenBy,
                (byte[])row.SnapshotData,
                (int)row.TableCount,
                (int)row.ViewCount,
                (int)row.ProcedureCount,
                (int)row.FunctionCount)
        };

        return snapshot;
    }
}

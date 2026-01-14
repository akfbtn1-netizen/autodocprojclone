
using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.ValueObjects;

namespace Enterprise.Documentation.Core.Application.Interfaces;

/// <summary>
/// Repository interface for audit logs.
/// </summary>
public interface IAuditLogRepository
{
    /// <summary>
    /// Gets an audit log by ID.
    /// </summary>
    Task<AuditLog?> GetByIdAsync(AuditLogId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets audit logs for a specific entity.
    /// </summary>
    Task<IReadOnlyList<AuditLog>> GetByEntityAsync(
        string entityType, 
        string entityId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets audit logs by user.
    /// </summary>
    Task<IReadOnlyList<AuditLog>> GetByUserAsync(
        UserId userId, 
        int pageNumber = 1, 
        int pageSize = 50, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets audit logs within a date range.
    /// </summary>
    Task<IReadOnlyList<AuditLog>> GetByDateRangeAsync(
        DateTime fromDate, 
        DateTime toDate, 
        int pageNumber = 1, 
        int pageSize = 50, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new audit log.
    /// </summary>
    Task<AuditLog> AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts audit logs matching criteria.
    /// </summary>
    Task<int> CountAsync(string? entityType = null, string? action = null, CancellationToken cancellationToken = default);
}
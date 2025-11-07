using Microsoft.EntityFrameworkCore;
using Enterprise.Documentation.Core.Application.Interfaces;
using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.ValueObjects;

namespace Enterprise.Documentation.Core.Infrastructure.Persistence.Repositories;

/// <summary>
/// AuditLog repository implementation using Entity Framework Core
/// </summary>
public class AuditLogRepository : Repository<AuditLog, AuditLogId>, IAuditLogRepository
{
    public AuditLogRepository(DocumentationDbContext context) : base(context)
    {
    }

    public new async Task<AuditLog?> GetByIdAsync(AuditLogId id, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLog>> GetByEntityAsync(
        string entityType, 
        string entityId, 
        CancellationToken cancellationToken = default)
    {
        var result = await DbSet
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.OccurredAt)
            .ToListAsync(cancellationToken);
        return result.AsReadOnly();
    }

    public async Task<IReadOnlyList<AuditLog>> GetByUserAsync(
        UserId userId, 
        int pageNumber = 1, 
        int pageSize = 50, 
        CancellationToken cancellationToken = default)
    {
        var result = await DbSet
            .Where(a => a.CreatedBy == userId)
            .OrderByDescending(a => a.OccurredAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return result.AsReadOnly();
    }

    public async Task<IReadOnlyList<AuditLog>> GetByDateRangeAsync(
        DateTime fromDate, 
        DateTime toDate, 
        int pageNumber = 1, 
        int pageSize = 50, 
        CancellationToken cancellationToken = default)
    {
        var result = await DbSet
            .Where(a => a.OccurredAt >= fromDate && a.OccurredAt <= toDate)
            .OrderByDescending(a => a.OccurredAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return result.AsReadOnly();
    }

    public async Task<AuditLog> AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
    {
        var entry = await DbSet.AddAsync(auditLog, cancellationToken);
        return entry.Entity;
    }

    public async Task<int> CountAsync(string? entityType = null, string? action = null, CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsQueryable();
        
        if (!string.IsNullOrEmpty(entityType))
        {
            query = query.Where(a => a.EntityType == entityType);
        }
        
        if (!string.IsNullOrEmpty(action))
        {
            query = query.Where(a => a.Action == action);
        }
        
        return await query.CountAsync(cancellationToken);
    }

    // Note: AuditLogs are typically read-only after creation, so no Update/Remove methods
}
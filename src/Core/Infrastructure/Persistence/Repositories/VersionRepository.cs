using Microsoft.EntityFrameworkCore;
using Enterprise.Documentation.Core.Application.Interfaces;
using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.ValueObjects;
using DomainVersion = Enterprise.Documentation.Core.Domain.Entities.Version;

namespace Enterprise.Documentation.Core.Infrastructure.Persistence.Repositories;

/// <summary>
/// Version repository implementation using Entity Framework Core
/// </summary>
public class VersionRepository : Repository<DomainVersion, VersionId>, IVersionRepository
{
    public VersionRepository(DocumentationDbContext context) : base(context)
    {
    }

    public new async Task<DomainVersion?> GetByIdAsync(VersionId id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<DomainVersion>> GetByDocumentIdAsync(DocumentId documentId, CancellationToken cancellationToken = default)
    {
        var result = await DbSet
            .AsNoTracking()
            .Where(v => v.DocumentId == documentId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(cancellationToken);
        return result.AsReadOnly();
    }

    public async Task<DomainVersion?> GetCurrentVersionAsync(DocumentId documentId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(v => v.DocumentId == documentId)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<VersionApproval>> GetApprovalsAsync(VersionId versionId, CancellationToken cancellationToken = default)
    {
        // This would typically be in a separate VersionApproval repository
        // For now, return empty list as placeholder
        await Task.CompletedTask;
        return Array.Empty<VersionApproval>().ToList().AsReadOnly();
    }

    public async Task<bool> ExistsAsync(VersionId id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .AnyAsync(v => v.Id == id, cancellationToken);
    }

    public async Task<DomainVersion> AddAsync(DomainVersion version, CancellationToken cancellationToken = default)
    {
        var entry = await DbSet.AddAsync(version, cancellationToken);
        return entry.Entity;
    }

    public async Task<DomainVersion> UpdateAsync(DomainVersion version, CancellationToken cancellationToken = default)
    {
        DbSet.Update(version);
        await Task.CompletedTask;
        return version;
    }
}
using Microsoft.EntityFrameworkCore;
using Enterprise.Documentation.Core.Application.Interfaces;
using Enterprise.Documentation.Core.Application.DTOs;
using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.ValueObjects;
using System.Linq.Expressions;

namespace Enterprise.Documentation.Core.Infrastructure.Persistence.Repositories;

/// <summary>
/// Document repository implementation using Entity Framework Core
/// </summary>
public class DocumentRepository : Repository<Document, DocumentId>, IDocumentRepository
{
    public DocumentRepository(DocumentationDbContext context) : base(context)
    {
    }

    // IDocumentRepository interface implementations  
    public new async Task<Document?> GetByIdAsync(DocumentId id, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<List<Document>> GetByUserIdAsync(UserId userId)
    {
        return await DbSet
            .Where(d => d.CreatedBy == userId)
            .ToListAsync();
    }

    public async Task<List<Document>> GetPendingApprovalsAsync()
    {
        return await DbSet
            .Where(d => d.Status.Value == "Pending Approval")
            .ToListAsync();
    }

    public async Task<List<Document>> SearchAsync(string query)
    {
        return await DbSet
            .Where(d => d.Title.Contains(query) || (d.Content != null && d.Content.Contains(query)))
            .ToListAsync();
    }

    public async Task<List<Document>> FindAsync(Expression<Func<Document, bool>> predicate)
    {
        return await DbSet
            .Where(predicate)
            .ToListAsync();
    }

    public async Task<List<Document>> FindAsync(Expression<Func<Document, bool>> predicate, int skip, int take, CancellationToken cancellationToken)
    {
        return await DbSet
            .Where(predicate)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(Expression<Func<Document, bool>> predicate)
    {
        return await DbSet
            .Where(predicate)
            .CountAsync();
    }

    public override async Task<int> CountAsync(Expression<Func<Document, bool>>? predicate, CancellationToken cancellationToken)
    {
        return await DbSet
            .Where(predicate ?? (d => true))
            .CountAsync(cancellationToken);
    }

    public async Task<PagedResult<Document>> GetPagedAsync(int pageNumber, int pageSize, string? filter = null)
    {
        return await GetPagedAsync(pageNumber, pageSize, filter, CancellationToken.None);
    }

    public async Task<PagedResult<Document>> GetPagedAsync(int pageNumber, int pageSize, string? filter, CancellationToken cancellationToken)
    {
        var query = DbSet.AsQueryable();

        if (!string.IsNullOrEmpty(filter))
        {
            query = query.Where(d => d.Status.Value == filter || d.DocumentType.Contains(filter));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Document>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<Document> AddAsync(Document document)
    {
        return await AddAsync(document, CancellationToken.None);
    }

    public async Task<Document> AddAsync(Document document, CancellationToken cancellationToken)
    {
        var result = await DbSet.AddAsync(document, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);
        return result.Entity;
    }

    public async Task UpdateAsync(Document document)
    {
        await UpdateAsync(document, CancellationToken.None);
    }

    public async Task UpdateAsync(Document document, CancellationToken cancellationToken)
    {
        DbSet.Update(document);
        await Context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(DocumentId id)
    {
        var document = await GetByIdAsync(id);
        if (document != null)
        {
            DbSet.Remove(document);
            await Context.SaveChangesAsync();
        }
    }
}
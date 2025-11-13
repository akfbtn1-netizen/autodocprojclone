using Microsoft.EntityFrameworkCore;
using Enterprise.Documentation.Core.Application.Interfaces;
using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.ValueObjects;
using Enterprise.Documentation.Core.Domain.Specifications;

namespace Enterprise.Documentation.Core.Infrastructure.Persistence.Repositories;

/// <summary>
/// Document repository implementation using Entity Framework Core
/// </summary>
public class DocumentRepository : Repository<Document, DocumentId>, IDocumentRepository
{
    public DocumentRepository(DocumentationDbContext context) : base(context)
    {
    }

    public new async Task<Document?> GetByIdAsync(DocumentId id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Document>> GetBySpecificationAsync(
        ISpecification<Document> specification,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet
            .AsNoTracking()
            .AsQueryable();

        if (specification != null)
        {
            query = query.Where(specification.ToExpression());
        }

        var result = await query.ToListAsync(cancellationToken);
        return result.AsReadOnly();
    }

    public async Task<PagedResult<Document>> GetPagedAsync(
        ISpecification<Document>? specification = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = ApplySpecification(DbSet.AsNoTracking().AsQueryable(), specification);
        
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        
        return new PagedResult<Document>(items.AsReadOnly(), totalCount, pageNumber, pageSize);
    }

    private static IQueryable<Document> ApplySpecification(IQueryable<Document> query, ISpecification<Document>? specification)
    {
        return specification != null ? query.Where(specification.ToExpression()) : query;
    }

    public async Task<Document> AddAsync(Document document, CancellationToken cancellationToken = default)
    {
        var entry = await DbSet.AddAsync(document, cancellationToken);
        return entry.Entity;
    }

    public async Task<Document> UpdateAsync(Document document, CancellationToken cancellationToken = default)
    {
        DbSet.Update(document);
        await Task.CompletedTask;
        return document;
    }

    public async Task DeleteAsync(Document document, CancellationToken cancellationToken = default)
    {
        DbSet.Remove(document);
        await Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Document>> FindAsync(
        ISpecification<Document> specification,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet
            .AsNoTracking()
            .AsQueryable();

        if (specification != null)
        {
            query = query.Where(specification.ToExpression());
        }

        var result = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return result.AsReadOnly();
    }

    public async Task<int> CountAsync(
        ISpecification<Document> specification,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet
            .AsNoTracking()
            .AsQueryable();

        if (specification != null)
        {
            query = query.Where(specification.ToExpression());
        }

        return await query.CountAsync(cancellationToken);
    }
}
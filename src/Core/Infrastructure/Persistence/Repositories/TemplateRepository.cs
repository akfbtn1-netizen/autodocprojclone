using Microsoft.EntityFrameworkCore;
using Enterprise.Documentation.Core.Application.Interfaces;
using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.ValueObjects;
using Enterprise.Documentation.Core.Domain.Specifications;

namespace Enterprise.Documentation.Core.Infrastructure.Persistence.Repositories;

/// <summary>
/// Template repository implementation using Entity Framework Core
/// </summary>
public class TemplateRepository : Repository<Template, TemplateId>, ITemplateRepository
{
    public TemplateRepository(DocumentationDbContext context) : base(context)
    {
    }

    public new async Task<Template?> GetByIdAsync(TemplateId id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<Template?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Name == name, cancellationToken);
    }

    public async Task<IReadOnlyList<Template>> GetActiveTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var result = await DbSet
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
        return result.AsReadOnly();
    }

    public async Task<IReadOnlyList<Template>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        var result = await DbSet
            .AsNoTracking()
            .Where(t => t.Category == category && t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
        return result.AsReadOnly();
    }

    public async Task<IEnumerable<Template>> FindAsync<T>(T specification, CancellationToken cancellationToken = default)
        where T : ISpecification<Template>
    {
        var query = DbSet
            .AsNoTracking()
            .AsQueryable();

        if (specification != null)
        {
            query = query.Where(specification.ToExpression());
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync<T>(T specification, CancellationToken cancellationToken = default)
        where T : ISpecification<Template>
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

    public async Task<bool> ExistsAsync(TemplateId id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .AnyAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .AnyAsync(t => t.Name == name, cancellationToken);
    }

    public async Task<Template> AddAsync(Template template, CancellationToken cancellationToken = default)
    {
        var entry = await DbSet.AddAsync(template, cancellationToken);
        return entry.Entity;
    }

    public async Task<Template> UpdateAsync(Template template, CancellationToken cancellationToken = default)
    {
        DbSet.Update(template);
        await Task.CompletedTask;
        return template;
    }
}
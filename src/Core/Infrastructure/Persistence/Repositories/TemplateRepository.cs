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

    // ITemplateRepository interface implementations
    public async Task<Template?> GetByIdAsync(TemplateId id)
    {
        return await DbSet.FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<List<Template>> GetByTypeAsync(string documentType)
    {
        return await DbSet
            .Where(t => t.DocumentType == documentType && t.IsActive)
            .ToListAsync();
    }

    public async Task<Template> AddAsync(Template template)
    {
        var result = await DbSet.AddAsync(template);
        await Context.SaveChangesAsync();
        return result.Entity;
    }

    public async Task UpdateAsync(Template template)
    {
        DbSet.Update(template);
        await Context.SaveChangesAsync();
    }

    public async Task DeleteAsync(TemplateId id)
    {
        var template = await GetByIdAsync(id);
        if (template != null)
        {
            DbSet.Remove(template);
            await Context.SaveChangesAsync();
        }
    }

    // Original methods for compatibility
    public new async Task<Template?> GetByIdAsync(TemplateId id, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<Template?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(t => t.Name == name, cancellationToken);
    }

    public async Task<IReadOnlyList<Template>> GetActiveTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var result = await DbSet
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
        return result.AsReadOnly();
    }

    public async Task<IReadOnlyList<Template>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        var result = await DbSet
            .Where(t => t.Category == category && t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
        return result.AsReadOnly();
    }

    public async Task<IEnumerable<Template>> FindAsync<T>(T specification, CancellationToken cancellationToken = default) 
        where T : ISpecification<Template>
    {
        var query = DbSet.AsQueryable();
        
        if (specification != null)
        {
            query = query.Where(specification.ToExpression());
        }
        
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync<T>(T specification, CancellationToken cancellationToken = default) 
        where T : ISpecification<Template>
    {
        var query = DbSet.AsQueryable();
        
        if (specification != null)
        {
            query = query.Where(specification.ToExpression());
        }
        
        return await query.CountAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(TemplateId id, CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(t => t.Name == name, cancellationToken);
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
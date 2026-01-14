using Microsoft.EntityFrameworkCore;
using Enterprise.Documentation.Core.Application.Interfaces;
using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.ValueObjects;
using Enterprise.Documentation.Core.Domain.Specifications;

namespace Enterprise.Documentation.Core.Infrastructure.Persistence.Repositories;

/// <summary>
/// User repository implementation using Entity Framework Core
/// </summary>
public class UserRepository : Repository<User, UserId>, IUserRepository
{
    public UserRepository(DocumentationDbContext context) : base(context)
    {
    }

    // IUserRepository interface implementations
    public async Task<User?> GetByIdAsync(UserId userId)
    {
        return await DbSet.FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await DbSet.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<List<User>> GetAllAsync()
    {
        return await DbSet.ToListAsync();
    }

    public async Task<User> AddAsync(User user)
    {
        var result = await DbSet.AddAsync(user);
        await Context.SaveChangesAsync();
        return result.Entity;
    }

    public async Task UpdateAsync(User user)
    {
        DbSet.Update(user);
        await Context.SaveChangesAsync();
    }

    public async Task DeleteAsync(UserId userId)
    {
        var user = await GetByIdAsync(userId);
        if (user != null)
        {
            DbSet.Remove(user);
            await Context.SaveChangesAsync();
        }
    }

    // Original methods for compatibility
    public new async Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }



    public async Task<IReadOnlyList<User>> GetBySpecificationAsync(
        ISpecification<User> specification,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsQueryable();
        
        if (specification != null)
        {
            query = query.Where(specification.ToExpression());
        }
        
        var result = await query.ToListAsync(cancellationToken);
        return result.AsReadOnly();
    }

    public async Task<bool> ExistsAsync(UserId id, CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(u => u.Email == email, cancellationToken);
    }



    public async Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
    {
        var entry = await DbSet.AddAsync(user, cancellationToken);
        return entry.Entity;
    }

    public async Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        DbSet.Update(user);
        await Task.CompletedTask;
        return user;
    }
}
using System.Linq.Expressions;

namespace Shared.Contracts.Interfaces;

/// <summary>
/// Generic repository interface for enterprise data access patterns.
/// Provides comprehensive CRUD operations with async support and flexible querying.
/// Follows repository pattern with Unit of Work support for transaction management.
/// </summary>
/// <typeparam name="TEntity">Entity type that implements IEntity</typeparam>
/// <typeparam name="TKey">Primary key type (typically Guid or int)</typeparam>
public interface IRepository<TEntity, TKey> where TEntity : class, IEntity<TKey>
{
    /// <summary>
    /// Gets an entity by its primary key asynchronously.
    /// </summary>
    /// <param name="id">Primary key of the entity</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Entity if found, null otherwise</returns>
    Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all entities asynchronously with optional filtering and pagination.
    /// </summary>
    /// <param name="predicate">Optional filter expression</param>
    /// <param name="orderBy">Optional ordering function</param>
    /// <param name="skip">Number of records to skip for pagination</param>
    /// <param name="take">Number of records to take for pagination</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of entities</returns>
    Task<IEnumerable<TEntity>> GetAllAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        int? skip = null,
        int? take = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single entity matching the predicate asynchronously.
    /// </summary>
    /// <param name="predicate">Filter expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Entity if found, null otherwise</returns>
    Task<TEntity?> GetSingleAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if any entity matches the predicate asynchronously.
    /// </summary>
    /// <param name="predicate">Filter expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if any entity matches, false otherwise</returns>
    Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts entities matching the predicate asynchronously.
    /// </summary>
    /// <param name="predicate">Optional filter expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of entities matching the predicate</returns>
    Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new entity to the repository.
    /// Changes are not persisted until SaveChangesAsync is called.
    /// </summary>
    /// <param name="entity">Entity to add</param>
    /// <returns>The added entity</returns>
    TEntity Add(TEntity entity);

    /// <summary>
    /// Adds multiple entities to the repository.
    /// Changes are not persisted until SaveChangesAsync is called.
    /// </summary>
    /// <param name="entities">Entities to add</param>
    void AddRange(IEnumerable<TEntity> entities);

    /// <summary>
    /// Updates an existing entity in the repository.
    /// Changes are not persisted until SaveChangesAsync is called.
    /// </summary>
    /// <param name="entity">Entity to update</param>
    /// <returns>The updated entity</returns>
    TEntity Update(TEntity entity);

    /// <summary>
    /// Updates multiple entities in the repository.
    /// Changes are not persisted until SaveChangesAsync is called.
    /// </summary>
    /// <param name="entities">Entities to update</param>
    void UpdateRange(IEnumerable<TEntity> entities);

    /// <summary>
    /// Removes an entity from the repository.
    /// Changes are not persisted until SaveChangesAsync is called.
    /// </summary>
    /// <param name="entity">Entity to remove</param>
    void Remove(TEntity entity);

    /// <summary>
    /// Removes an entity by its primary key.
    /// Changes are not persisted until SaveChangesAsync is called.
    /// </summary>
    /// <param name="id">Primary key of the entity to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if entity was found and removed, false otherwise</returns>
    Task<bool> RemoveByIdAsync(TKey id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes multiple entities from the repository.
    /// Changes are not persisted until SaveChangesAsync is called.
    /// </summary>
    /// <param name="entities">Entities to remove</param>
    void RemoveRange(IEnumerable<TEntity> entities);

    /// <summary>
    /// Persists all changes made to the repository asynchronously.
    /// This is typically implemented by the Unit of Work pattern.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of affected records</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Base entity interface that all domain entities must implement.
/// Provides standard properties for tracking and identification.
/// </summary>
/// <typeparam name="TKey">Primary key type</typeparam>
public interface IEntity<TKey>
{
    /// <summary>Primary key of the entity</summary>
    TKey Id { get; set; }

    /// <summary>Timestamp when the entity was created</summary>
    DateTime CreatedAt { get; set; }

    /// <summary>Timestamp when the entity was last updated</summary>
    DateTime UpdatedAt { get; set; }

    /// <summary>Identifier of the user who created the entity</summary>
    string CreatedBy { get; set; }

    /// <summary>Identifier of the user who last updated the entity</summary>
    string UpdatedBy { get; set; }

    /// <summary>Version/timestamp for optimistic concurrency control</summary>
    byte[]? RowVersion { get; set; }
}

/// <summary>
/// Convenience interface for entities with Guid primary keys.
/// Most domain entities should implement this interface.
/// </summary>
public interface IEntity : IEntity<Guid>
{
}
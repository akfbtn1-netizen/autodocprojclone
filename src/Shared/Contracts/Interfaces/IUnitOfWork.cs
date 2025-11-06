namespace Shared.Contracts.Interfaces;

/// <summary>
/// Unit of Work interface for managing database transactions and repository coordination.
/// Implements the Unit of Work pattern to ensure atomicity of operations across multiple repositories.
/// Provides transaction management and change tracking capabilities.
/// </summary>
public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets a repository for the specified entity type.
    /// Repositories are cached and reused within the same unit of work.
    /// </summary>
    /// <typeparam name="TEntity">Entity type that implements IEntity</typeparam>
    /// <typeparam name="TKey">Primary key type</typeparam>
    /// <returns>Repository instance for the entity type</returns>
    IRepository<TEntity, TKey> GetRepository<TEntity, TKey>() 
        where TEntity : class, IEntity<TKey>;

    /// <summary>
    /// Gets a repository for entities with Guid primary keys.
    /// Convenience method for the most common scenario.
    /// </summary>
    /// <typeparam name="TEntity">Entity type that implements IEntity</typeparam>
    /// <returns>Repository instance for the entity type</returns>
    IRepository<TEntity, Guid> GetRepository<TEntity>() 
        where TEntity : class, IEntity;

    /// <summary>
    /// Begins a new database transaction asynchronously.
    /// Subsequent operations will be part of this transaction until committed or rolled back.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Transaction scope that can be committed or rolled back</returns>
    Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all changes made within this unit of work to the database asynchronously.
    /// This will persist changes from all repositories managed by this unit of work.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of affected records</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all changes and publishes domain events asynchronously.
    /// Ensures that domain events are only published after successful persistence.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of affected records</returns>
    Task<int> SaveChangesAndPublishEventsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all tracked changes without saving them.
    /// Useful for discarding changes or implementing rollback scenarios.
    /// </summary>
    void ClearChangeTracking();

    /// <summary>
    /// Gets the number of entities currently being tracked for changes.
    /// Useful for monitoring and debugging purposes.
    /// </summary>
    int GetTrackedEntitiesCount();

    /// <summary>
    /// Checks if there are any unsaved changes in the unit of work.
    /// </summary>
    bool HasUnsavedChanges { get; }
}

/// <summary>
/// Transaction scope interface for managing database transactions.
/// Provides commit and rollback capabilities with proper resource management.
/// </summary>
public interface ITransactionScope : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Transaction identifier for tracking and logging purposes.
    /// </summary>
    string TransactionId { get; }

    /// <summary>
    /// Current state of the transaction.
    /// </summary>
    TransactionState State { get; }

    /// <summary>
    /// Commits the transaction asynchronously, making all changes permanent.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the transaction asynchronously, discarding all changes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Transaction state enumeration for tracking transaction lifecycle.
/// </summary>
public enum TransactionState
{
    /// <summary>Transaction is active and accepting operations</summary>
    Active,
    /// <summary>Transaction has been committed successfully</summary>
    Committed,
    /// <summary>Transaction has been rolled back</summary>
    RolledBack,
    /// <summary>Transaction has been disposed</summary>
    Disposed
}
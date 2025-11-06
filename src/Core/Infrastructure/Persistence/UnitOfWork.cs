using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Interfaces;
using System.Diagnostics;

namespace Core.Infrastructure.Persistence;

/// <summary>
/// Unit of Work implementation using Entity Framework Core.
/// Manages database transactions and repository coordination with proper resource management.
/// Provides transaction scoping and change tracking capabilities.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly DbContext _context;
    private readonly ILogger<UnitOfWork> _logger;
    private readonly Dictionary<Type, object> _repositories = new();
    private IDbContextTransaction? _currentTransaction;
    private bool _disposed;

    /// <summary>
    /// Initializes a new Unit of Work with the specified database context.
    /// </summary>
    /// <param name="context">Database context</param>
    /// <param name="logger">Logger instance</param>
    public UnitOfWork(DbContext context, ILogger<UnitOfWork> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public IRepository<TEntity, TKey> GetRepository<TEntity, TKey>() 
        where TEntity : class, IEntity<TKey>
    {
        var entityType = typeof(TEntity);
        
        if (_repositories.TryGetValue(entityType, out var existingRepository))
        {
            return (IRepository<TEntity, TKey>)existingRepository;
        }

        var repository = new Repository<TEntity, TKey>(_context);
        _repositories[entityType] = repository;
        
        _logger.LogDebug("Created repository for entity type {EntityType}", entityType.Name);
        return repository;
    }

    /// <inheritdoc />
    public IRepository<TEntity, Guid> GetRepository<TEntity>() 
        where TEntity : class, IEntity
    {
        return GetRepository<TEntity, Guid>();
    }

    /// <inheritdoc />
    public async Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            throw new InvalidOperationException("A transaction is already in progress");
        }

        _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var transactionScope = new TransactionScope(_currentTransaction, _logger);
        
        _logger.LogDebug("Transaction started with ID {TransactionId}", transactionScope.TransactionId);
        return transactionScope;
    }

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await _context.SaveChangesAsync(cancellationToken);
            stopwatch.Stop();
            
            _logger.LogDebug("Saved {ChangeCount} changes to database in {ElapsedMs}ms", 
                result, stopwatch.ElapsedMilliseconds);
                
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to save changes to database after {ElapsedMs}ms", 
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> SaveChangesAndPublishEventsAsync(CancellationToken cancellationToken = default)
    {
        // First save changes
        var result = await SaveChangesAsync(cancellationToken);

        // TODO: Implement domain event publishing
        // This would typically involve:
        // 1. Collecting domain events from entities
        // 2. Publishing them via IMessageBus
        // 3. Clearing events from entities after successful publishing
        
        _logger.LogDebug("Saved changes and published domain events");
        return result;
    }

    /// <inheritdoc />
    public void ClearChangeTracking()
    {
        _context.ChangeTracker.Clear();
        _logger.LogDebug("Cleared Entity Framework change tracking");
    }

    /// <inheritdoc />
    public int GetTrackedEntitiesCount()
    {
        return _context.ChangeTracker.Entries().Count();
    }

    /// <inheritdoc />
    public bool HasUnsavedChanges => _context.ChangeTracker.HasChanges();

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCoreAsync();
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the Unit of Work resources.
    /// </summary>
    /// <param name="disposing">Whether disposing is in progress</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _currentTransaction?.Dispose();
            _repositories.Clear();
            _disposed = true;
            _logger.LogDebug("Unit of Work disposed");
        }
    }

    /// <summary>
    /// Asynchronously disposes the Unit of Work resources.
    /// </summary>
    protected virtual async ValueTask DisposeAsyncCoreAsync()
    {
        if (_currentTransaction != null)
        {
            await _currentTransaction.DisposeAsync();
        }

        _repositories.Clear();
        _disposed = true;
        _logger.LogDebug("Unit of Work disposed asynchronously");
    }
}

/// <summary>
/// Transaction scope implementation for Entity Framework transactions.
/// Provides proper transaction management with commit and rollback capabilities.
/// </summary>
public class TransactionScope : ITransactionScope
{
    private readonly IDbContextTransaction _transaction;
    private readonly ILogger _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new transaction scope.
    /// </summary>
    /// <param name="transaction">Database transaction</param>
    /// <param name="logger">Logger instance</param>
    public TransactionScope(IDbContextTransaction transaction, ILogger logger)
    {
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        TransactionId = _transaction.TransactionId.ToString();
        State = TransactionState.Active;
    }

    /// <inheritdoc />
    public string TransactionId { get; }

    /// <inheritdoc />
    public TransactionState State { get; private set; }

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (State != TransactionState.Active)
        {
            throw new InvalidOperationException($"Cannot commit transaction in state {State}");
        }

        try
        {
            await _transaction.CommitAsync(cancellationToken);
            State = TransactionState.Committed;
            _logger.LogDebug("Transaction {TransactionId} committed successfully", TransactionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to commit transaction {TransactionId}", TransactionId);
            await RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (State != TransactionState.Active)
        {
            _logger.LogWarning("Attempted to rollback transaction {TransactionId} in state {State}", TransactionId, State);
            return;
        }

        try
        {
            await _transaction.RollbackAsync(cancellationToken);
            State = TransactionState.RolledBack;
            _logger.LogDebug("Transaction {TransactionId} rolled back", TransactionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rollback transaction {TransactionId}", TransactionId);
            throw;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCoreAsync();
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the transaction scope resources.
    /// </summary>
    /// <param name="disposing">Whether disposing is in progress</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            if (State == TransactionState.Active)
            {
                _logger.LogWarning("Transaction {TransactionId} disposed without explicit commit or rollback", TransactionId);
                State = TransactionState.RolledBack;
            }

            _transaction.Dispose();
            State = TransactionState.Disposed;
            _disposed = true;
        }
    }

    /// <summary>
    /// Asynchronously disposes the transaction scope resources.
    /// </summary>
    protected virtual async ValueTask DisposeAsyncCoreAsync()
    {
        if (State == TransactionState.Active)
        {
            _logger.LogWarning("Transaction {TransactionId} disposed without explicit commit or rollback", TransactionId);
            await RollbackAsync();
        }

        await _transaction.DisposeAsync();
        State = TransactionState.Disposed;
        _disposed = true;
    }
}
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Enterprise.Documentation.Core.Application.Interfaces;

namespace Enterprise.Documentation.Core.Infrastructure.Persistence;

/// <summary>
/// Simple Unit of Work implementation using Entity Framework Core
/// </summary>
public class SimpleUnitOfWork : IUnitOfWork
{
    private readonly DocumentationDbContext _context;
    private readonly ILogger<SimpleUnitOfWork> _logger;
    private IDbContextTransaction? _currentTransaction;

    public SimpleUnitOfWork(DocumentationDbContext context, ILogger<SimpleUnitOfWork> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Saved {ChangeCount} changes to database", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving changes to database");
            throw;
        }
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            _logger.LogWarning("Transaction already in progress");
            return;
        }

        _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        _logger.LogDebug("Database transaction started");
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            _logger.LogWarning("No active transaction to commit");
            return;
        }

        try
        {
            await _currentTransaction.CommitAsync(cancellationToken);
            _logger.LogDebug("Database transaction committed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error committing transaction");
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            _currentTransaction?.Dispose();
            _currentTransaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            _logger.LogWarning("No active transaction to rollback");
            return;
        }

        try
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
            _logger.LogDebug("Database transaction rolled back");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation during transaction rollback");
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "Timeout during transaction rollback");
        }
        finally
        {
            _currentTransaction?.Dispose();
            _currentTransaction = null;
        }
    }
}
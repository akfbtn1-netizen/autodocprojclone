using Enterprise.Documentation.Core.Application.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Enterprise.Documentation.Core.Infrastructure.Data;

/// <summary>
/// Creates secure SQL Server connections using credentials from Azure Key Vault.
/// Implements connection string caching for performance.
/// </summary>
public sealed class SecureConnectionFactory : ISecureConnectionFactory
{
    private readonly ISecretManager _secretManager;
    private readonly ILogger<SecureConnectionFactory> _logger;
    private string? _cachedConnectionString;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private const string ConnectionStringSecretName = "SqlServer-ConnectionString";

    public SecureConnectionFactory(
        ISecretManager secretManager,
        ILogger<SecureConnectionFactory> logger)
    {
        _secretManager = secretManager ?? throw new ArgumentNullException(nameof(secretManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SqlConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = await GetConnectionStringAsync(cancellationToken);
        var connection = new SqlConnection(connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);
            _logger.LogDebug("Database connection opened successfully");
            return connection;
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Failed to open database connection");
            await connection.DisposeAsync();
            throw;
        }
    }

    public async Task<string> GetConnectionStringAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedConnectionString != null)
        {
            return _cachedConnectionString;
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_cachedConnectionString != null)
            {
                return _cachedConnectionString;
            }

            _cachedConnectionString = await _secretManager.GetSecretAsync(
                ConnectionStringSecretName,
                cancellationToken);

            _logger.LogInformation("Connection string retrieved and cached");
            return _cachedConnectionString;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}

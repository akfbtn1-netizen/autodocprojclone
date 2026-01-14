using Microsoft.Data.SqlClient;

namespace Enterprise.Documentation.Core.Application.Interfaces;

/// <summary>
/// Factory interface for creating secure SQL Server connections.
/// Retrieves connection strings from Azure Key Vault or configuration fallback.
/// </summary>
public interface ISecureConnectionFactory
{
    /// <summary>
    /// Creates and opens a new SQL Server connection using secure credentials.
    /// Connection strings are retrieved from Key Vault when available.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>An open SqlConnection ready for use.</returns>
    /// <exception cref="InvalidOperationException">Thrown when connection string cannot be retrieved.</exception>
    /// <exception cref="SqlException">Thrown when database connection fails.</exception>
    Task<SqlConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the connection string without creating a connection.
    /// Useful for scenarios where the connection string is needed but not an active connection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>The connection string.</returns>
    Task<string> GetConnectionStringAsync(CancellationToken cancellationToken = default);
}

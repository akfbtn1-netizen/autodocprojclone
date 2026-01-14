namespace Enterprise.Documentation.Core.Application.Interfaces;

/// <summary>
/// Interface for secure secret management with Azure Key Vault integration.
/// Provides centralized access to application secrets with caching and fallback support.
/// </summary>
public interface ISecretManager
{
    /// <summary>
    /// Retrieves a secret by name from the configured secret store.
    /// Uses caching to minimize external calls.
    /// </summary>
    /// <param name="secretName">The name of the secret to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>The secret value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the secret is not found.</exception>
    Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves multiple secrets by name from the configured secret store.
    /// </summary>
    /// <param name="secretNames">Array of secret names to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Dictionary mapping secret names to their values.</returns>
    Task<Dictionary<string, string>> GetSecretsAsync(string[] secretNames, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a secret exists in the secret store.
    /// </summary>
    /// <param name="secretName">The name of the secret to check.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>True if the secret exists, false otherwise.</returns>
    Task<bool> SecretExistsAsync(string secretName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the internal cache, forcing fresh retrieval on next request.
    /// Useful when secrets have been rotated.
    /// </summary>
    void ClearCache();
}

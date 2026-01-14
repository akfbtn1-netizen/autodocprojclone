using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Enterprise.Documentation.Core.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Enterprise.Documentation.Core.Infrastructure.Security;

/// <summary>
/// Manages secret retrieval from Azure Key Vault with caching and configuration fallback.
/// Uses DefaultAzureCredential for authentication (Azure CLI locally, Managed Identity in Azure).
/// </summary>
public sealed class SecretManager : ISecretManager
{
    private readonly SecretClient? _secretClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SecretManager> _logger;
    private readonly Dictionary<string, string> _cache = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly bool _useKeyVault;

    public SecretManager(IConfiguration configuration, ILogger<SecretManager> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var keyVaultUri = configuration["KeyVault:VaultUri"];
        _useKeyVault = !string.IsNullOrEmpty(keyVaultUri);

        if (_useKeyVault)
        {
            try
            {
                _secretClient = new SecretClient(
                    new Uri(keyVaultUri!),
                    new DefaultAzureCredential());
                _logger.LogInformation("SecretManager initialized with Key Vault: {KeyVaultUri}", keyVaultUri);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize Key Vault client, will use configuration fallback");
                _secretClient = null;
                _useKeyVault = false;
            }
        }
        else
        {
            _logger.LogInformation("Key Vault not configured, using configuration fallback for secrets");
        }
    }

    public async Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

        // Check cache first
        if (_cache.TryGetValue(secretName, out var cachedValue))
        {
            _logger.LogDebug("Retrieved cached secret: {SecretName}", secretName);
            return cachedValue;
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_cache.TryGetValue(secretName, out cachedValue))
            {
                return cachedValue;
            }

            string secretValue;

            // Try Key Vault first if configured
            if (_useKeyVault && _secretClient != null)
            {
                secretValue = await GetFromKeyVaultAsync(secretName, cancellationToken);
            }
            else
            {
                // Fallback to configuration
                secretValue = GetFromConfiguration(secretName);
            }

            _cache[secretName] = secretValue;
            return secretValue;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<Dictionary<string, string>> GetSecretsAsync(
        string[] secretNames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(secretNames);

        var secrets = new Dictionary<string, string>(secretNames.Length);
        foreach (var secretName in secretNames)
        {
            secrets[secretName] = await GetSecretAsync(secretName, cancellationToken);
        }
        return secrets;
    }

    public async Task<bool> SecretExistsAsync(string secretName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

        try
        {
            await GetSecretAsync(secretName, cancellationToken);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public void ClearCache()
    {
        _semaphore.Wait();
        try
        {
            _cache.Clear();
            _logger.LogInformation("Secret cache cleared");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<string> GetFromKeyVaultAsync(string secretName, CancellationToken cancellationToken)
    {
        try
        {
            var secret = await _secretClient!.GetSecretAsync(secretName, cancellationToken: cancellationToken);
            _logger.LogInformation("Retrieved secret from Key Vault: {SecretName}", secretName);
            return secret.Value.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Secret not found in Key Vault: {SecretName}, attempting configuration fallback", secretName);
            return GetFromConfiguration(secretName);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogWarning(ex, "Key Vault request failed for {SecretName}, attempting configuration fallback", secretName);
            return GetFromConfiguration(secretName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving secret from Key Vault: {SecretName}, attempting configuration fallback", secretName);
            return GetFromConfiguration(secretName);
        }
    }

    private string GetFromConfiguration(string secretName)
    {
        // Map common secret names to configuration paths
        var configPath = MapSecretNameToConfigPath(secretName);
        var value = _configuration[configPath];

        if (string.IsNullOrEmpty(value))
        {
            _logger.LogError("Secret '{SecretName}' not found in Key Vault or configuration (path: {ConfigPath})", secretName, configPath);
            throw new InvalidOperationException($"Secret '{secretName}' not found in Key Vault or configuration");
        }

        _logger.LogDebug("Retrieved secret from configuration fallback: {SecretName}", secretName);
        return value;
    }

    private static string MapSecretNameToConfigPath(string secretName)
    {
        // Convert Key Vault naming convention (hyphens) to configuration paths (colons)
        // e.g., "SqlServer-ConnectionString" -> "ConnectionStrings:DefaultConnection"
        return secretName switch
        {
            "SqlServer-ConnectionString" => "ConnectionStrings:DefaultConnection",
            "Jwt-Secret" => "JwtSettings:SecretKey",
            "AzureOpenAI-ApiKey" => "AzureOpenAI:ApiKey",
            "AzureOpenAI-Endpoint" => "AzureOpenAI:Endpoint",
            _ => secretName.Replace("-", ":")
        };
    }
}

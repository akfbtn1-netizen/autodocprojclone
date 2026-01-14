
using Enterprise.Documentation.Shared.Contracts.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Enterprise.Documentation.Shared.Configuration;

/// <summary>
/// Standard implementation of IAgentConfiguration for Enterprise Documentation Platform agents.
/// Provides secure, validated configuration access with governance integration.
/// </summary>
public class AgentConfiguration : IAgentConfiguration
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AgentConfiguration> _logger;
    private readonly Dictionary<string, object> _cache;
    private readonly object _cacheLock = new();

    public AgentConfiguration(
        IConfiguration configuration,
        ILogger<AgentConfiguration> logger,
        string agentId,
        string environment)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        AgentId = agentId ?? throw new ArgumentNullException(nameof(agentId));
        Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _cache = new Dictionary<string, object>();
    }

    /// <inheritdoc />
    public string AgentId { get; }

    /// <inheritdoc />
    public string Environment { get; }

    /// <inheritdoc />
    public T? GetValue<T>(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key));

        try
        {
            // Check cache first
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(key, out var cachedValue) && cachedValue is T)
                {
                    return (T)cachedValue;
                }
            }

            // Try agent-specific configuration first
            var agentSpecificKey = $"Agents:{AgentId}:{key}";
            var value = _configuration.GetValue<T>(agentSpecificKey);
            
            // Fall back to global configuration
            if (value == null || value.Equals(default(T)))
            {
                value = _configuration.GetValue<T>(key);
            }

            // Cache the result
            if (value != null)
            {
                lock (_cacheLock)
                {
                    _cache[key] = value;
                }
            }

            _logger.LogDebug("Retrieved configuration value for key '{Key}' (Agent: {AgentId})", key, AgentId);
            return value;
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve configuration value for key '{Key}' (Agent: {AgentId})", key, AgentId);
            return default(T);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve configuration value for key '{Key}' (Agent: {AgentId})", key, AgentId);
            return default(T);
        }
    }

    /// <inheritdoc />
    public T GetValue<T>(string key, T defaultValue)
    {
        var value = GetValue<T>(key);
        return value ?? defaultValue;
    }

    /// <inheritdoc />
    public async Task<string?> GetSecureValueAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key));

        try
        {
            // Audit log the secure value access
            _logger.LogInformation("Secure configuration access requested for key '{Key}' by agent '{AgentId}'", key, AgentId);

            // Try Azure Key Vault configuration first
            var keyVaultKey = $"KeyVault:{key}";
            var value = _configuration[keyVaultKey];
            
            if (string.IsNullOrEmpty(value))
            {
                // Fall back to agent-specific secure configuration
                var agentSecureKey = $"Agents:{AgentId}:Secure:{key}";
                value = _configuration[agentSecureKey];
            }

            if (string.IsNullOrEmpty(value))
            {
                // Fall back to global secure configuration
                var globalSecureKey = $"Secure:{key}";
                value = _configuration[globalSecureKey];
            }

            if (!string.IsNullOrEmpty(value))
            {
                _logger.LogInformation("Secure configuration value retrieved successfully for key '{Key}' (Agent: {AgentId})", key, AgentId);
            }
            else
            {
                _logger.LogWarning("Secure configuration value not found for key '{Key}' (Agent: {AgentId})", key, AgentId);
            }

            return await Task.FromResult(value);
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Configuration error while retrieving secure value for key '{Key}' (Agent: {AgentId})", key, AgentId);
            return null;
        }
        catch (Exception ex) when (!(ex is ArgumentException || ex is InvalidOperationException))
        {
            _logger.LogError(ex, "Unexpected error retrieving secure configuration value for key '{Key}' (Agent: {AgentId})", key, AgentId);
            return null;
        }
    }

    /// <inheritdoc />
    public ConfigurationValidationResult ValidateConfiguration(params string[] requiredKeys)
    {
        var result = new ConfigurationValidationResult
        {
            Timestamp = DateTimeOffset.UtcNow
        };

        _logger.LogInformation("Validating configuration for agent '{AgentId}' with {RequiredKeyCount} required keys", 
            AgentId, requiredKeys.Length);

        foreach (var key in requiredKeys)
        {
            try
            {
                var value = GetValue<string>(key);
                if (string.IsNullOrWhiteSpace(value))
                {
                    result.MissingKeys.Add(key);
                    result.ValidationMessages.Add($"Required configuration key '{key}' is missing or empty");
                }
            }
            catch (ArgumentException ex)
            {
                result.InvalidValues.Add(key);
                result.ValidationMessages.Add($"Configuration key '{key}' failed validation: {ex.Message}");
                _logger.LogWarning(ex, "Configuration validation failed for key '{Key}' (Agent: {AgentId})", key, AgentId);
            }
            catch (InvalidOperationException ex)
            {
                result.InvalidValues.Add(key);
                result.ValidationMessages.Add($"Configuration key '{key}' failed validation: {ex.Message}");
                _logger.LogWarning(ex, "Configuration validation failed for key '{Key}' (Agent: {AgentId})", key, AgentId);
            }
        }

        result.IsValid = result.MissingKeys.Count == 0 && result.InvalidValues.Count == 0;

        if (result.IsValid)
        {
            _logger.LogInformation("Configuration validation passed for agent '{AgentId}'", AgentId);
        }
        else
        {
            _logger.LogWarning("Configuration validation failed for agent '{AgentId}': {MissingCount} missing, {InvalidCount} invalid", 
                AgentId, result.MissingKeys.Count, result.InvalidValues.Count);
        }

        return result;
    }

    /// <inheritdoc />
    public IEnumerable<string> GetAvailableKeys()
    {
        var keys = new HashSet<string>();

        try
        {
            // Get all configuration keys (this is implementation-specific)
            var allKeys = GetAllConfigurationKeys(_configuration);
            
            // Filter for this agent's keys
            var agentPrefix = $"Agents:{AgentId}:";
            
            foreach (var key in allKeys)
            {
                // Include agent-specific keys (without the prefix)
                if (key.StartsWith(agentPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    keys.Add(key.Substring(agentPrefix.Length));
                }
                // Include global keys that don't conflict with agent-specific ones
                else if (!key.StartsWith("Agents:", StringComparison.OrdinalIgnoreCase) && 
                         !key.StartsWith("Secure:", StringComparison.OrdinalIgnoreCase) &&
                         !key.StartsWith("KeyVault:", StringComparison.OrdinalIgnoreCase))
                {
                    keys.Add(key);
                }
            }
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate available configuration keys for agent '{AgentId}'", AgentId);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate available configuration keys for agent '{AgentId}'", AgentId);
        }

        return keys.OrderBy(k => k);
    }

    /// <summary>
    /// Gets all configuration keys from the configuration provider.
    /// This is a helper method that works with most IConfiguration implementations.
    /// </summary>
    private static IEnumerable<string> GetAllConfigurationKeys(IConfiguration configuration)
    {
        var keys = new HashSet<string>();
        
        void AddKeys(IConfiguration config, string prefix = "")
        {
            foreach (var child in config.GetChildren())
            {
                var key = string.IsNullOrEmpty(prefix) ? child.Key : $"{prefix}:{child.Key}";
                keys.Add(key);
                
                if (child.GetChildren().Any())
                {
                    AddKeys(child, key);
                }
            }
        }
        
        AddKeys(configuration);
        return keys;
    }

    /// <summary>
    /// Clears the configuration cache.
    /// Useful for testing or when configuration values may have changed.
    /// </summary>
    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _cache.Clear();
        }
        _logger.LogDebug("Configuration cache cleared for agent '{AgentId}'", AgentId);
    }
}
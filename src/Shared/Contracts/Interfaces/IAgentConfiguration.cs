
namespace Enterprise.Documentation.Shared.Contracts.Interfaces;

/// <summary>
/// Base interface for agent configuration in the Enterprise Documentation Platform.
/// Provides standardized configuration access with security and governance integration.
/// </summary>
public interface IAgentConfiguration
{
    /// <summary>
    /// Unique identifier for the agent this configuration belongs to.
    /// Used for configuration validation and security enforcement.
    /// </summary>
    string AgentId { get; }
    
    /// <summary>
    /// Environment the agent is running in (Development, Staging, Production).
    /// Used for environment-specific configuration and security policies.
    /// </summary>
    string Environment { get; }
    
    /// <summary>
    /// Gets a configuration value by key with type safety.
    /// Returns null if the key doesn't exist or type conversion fails.
    /// </summary>
    /// <typeparam name="T">Type to convert the configuration value to</typeparam>
    /// <param name="key">Configuration key</param>
    /// <returns>Configuration value or null if not found/convertible</returns>
    T? GetValue<T>(string key);
    
    /// <summary>
    /// Gets a configuration value by key with a default fallback.
    /// Ensures the agent always has a usable configuration value.
    /// </summary>
    /// <typeparam name="T">Type to convert the configuration value to</typeparam>
    /// <param name="key">Configuration key</param>
    /// <param name="defaultValue">Default value if key not found or conversion fails</param>
    /// <returns>Configuration value or default value</returns>
    T GetValue<T>(string key, T defaultValue);
    
    /// <summary>
    /// Gets a secure configuration value (password, API key, connection string).
    /// Values are automatically decrypted and audit logged for governance.
    /// </summary>
    /// <param name="key">Secure configuration key</param>
    /// <returns>Decrypted secure value or null if not found</returns>
    Task<string?> GetSecureValueAsync(string key);
    
    /// <summary>
    /// Validates that all required configuration values are present and valid.
    /// Should be called during agent initialization to fail fast on configuration issues.
    /// </summary>
    /// <param name="requiredKeys">List of configuration keys that must be present</param>
    /// <returns>Validation result with details of any missing or invalid configuration</returns>
    ConfigurationValidationResult ValidateConfiguration(params string[] requiredKeys);
    
    /// <summary>
    /// Gets all configuration keys available to this agent.
    /// Useful for diagnostics and configuration auditing (excludes secure keys).
    /// </summary>
    /// <returns>List of available configuration keys</returns>
    IEnumerable<string> GetAvailableKeys();
}

/// <summary>
/// Result of configuration validation operation.
/// Provides detailed information about configuration completeness and validity.
/// </summary>
public class ConfigurationValidationResult
{
    /// <summary>Whether the configuration validation passed</summary>
    public bool IsValid { get; set; }
    
    /// <summary>List of missing required configuration keys</summary>
    public List<string> MissingKeys { get; set; } = new();
    
    /// <summary>List of invalid configuration values with details</summary>
    public List<string> InvalidValues { get; set; } = new();
    
    /// <summary>Detailed validation messages</summary>
    public List<string> ValidationMessages { get; set; } = new();
    
    /// <summary>Timestamp when validation was performed</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
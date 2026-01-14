using Enterprise.Documentation.Shared.Contracts.Interfaces;
using Enterprise.Documentation.Core.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Core.Infrastructure.Services;

/// <summary>
/// Memory-based cache service implementation
/// </summary>
public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<MemoryCacheService> _logger;

    public MemoryCacheService(IMemoryCache memoryCache, ILogger<MemoryCacheService> logger)
    {
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            if (_memoryCache.TryGetValue(key, out var value) && value is string jsonValue)
            {
                return JsonSerializer.Deserialize<T>(jsonValue);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache value for key {Key}", key);
        }
        
        return null;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var jsonValue = JsonSerializer.Serialize(value);
            var options = new MemoryCacheEntryOptions();
            
            if (expiry.HasValue)
            {
                options.SetAbsoluteExpiration(expiry.Value);
            }
            else
            {
                options.SetAbsoluteExpiration(TimeSpan.FromHours(1)); // Default 1 hour
            }
            
            _memoryCache.Set(key, jsonValue, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache value for key {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            _memoryCache.Remove(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache value for key {Key}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return _memoryCache.TryGetValue(key, out _);
    }

    // ICacheService interface implementations (without CancellationToken)
    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        return await GetAsync<T>(key, CancellationToken.None);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
    {
        await SetAsync(key, value, expiration, CancellationToken.None);
    }

    public async Task RemoveAsync(string key)
    {
        await RemoveAsync(key, CancellationToken.None);
    }

    public async Task<bool> ExistsAsync(string key)
    {
        return await ExistsAsync(key, CancellationToken.None);
    }
}
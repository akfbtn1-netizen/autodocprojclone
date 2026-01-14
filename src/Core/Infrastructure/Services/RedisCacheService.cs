using Enterprise.Documentation.Shared.Contracts.Interfaces;
using Enterprise.Documentation.Core.Application.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Core.Infrastructure.Services;

/// <summary>
/// Redis-based distributed cache service implementation
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IDistributedCache distributedCache, ILogger<RedisCacheService> logger)
    {
        _distributedCache = distributedCache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var value = await _distributedCache.GetStringAsync(key, cancellationToken);
            if (!string.IsNullOrEmpty(value))
            {
                return JsonSerializer.Deserialize<T>(value);
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
            var options = new DistributedCacheEntryOptions();
            
            if (expiry.HasValue)
            {
                options.SetAbsoluteExpiration(expiry.Value);
            }
            else
            {
                options.SetAbsoluteExpiration(TimeSpan.FromHours(1)); // Default 1 hour
            }
            
            await _distributedCache.SetStringAsync(key, jsonValue, options, cancellationToken);
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
            await _distributedCache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache value for key {Key}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await _distributedCache.GetStringAsync(key, cancellationToken);
            return !string.IsNullOrEmpty(value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking cache existence for key {Key}", key);
            return false;
        }
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
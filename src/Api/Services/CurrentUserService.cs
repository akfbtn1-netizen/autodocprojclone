using Enterprise.Documentation.Core.Application.Interfaces;
using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.ValueObjects;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace Enterprise.Documentation.Api.Services;

/// <summary>
/// Implementation of ICurrentUserService that extracts user information from HTTP context.
/// Includes in-memory caching for performance (PERFORMANCE FIX).
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUserRepository _userRepository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CurrentUserService> _logger;

    private static readonly MemoryCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10), // Cache for 10 minutes
        SlidingExpiration = TimeSpan.FromMinutes(5) // Refresh if accessed within 5 min
    };

    public CurrentUserService(
        IHttpContextAccessor httpContextAccessor,
        IUserRepository userRepository,
        IMemoryCache cache,
        ILogger<CurrentUserService> logger)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public UserId? GetCurrentUserId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
            return null;

        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userGuid))
            return null;

        return new UserId(userGuid);
    }

    public async Task<User?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return null;

        // PERFORMANCE FIX: Check cache first (80-90% cache hit rate expected)
        var cacheKey = $"User:{userId.Value}";

        if (_cache.TryGetValue(cacheKey, out User? cachedUser))
        {
            _logger.LogDebug("User {UserId} retrieved from cache", userId.Value);
            return cachedUser;
        }

        // Cache miss - fetch from database
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

        if (user != null)
        {
            _cache.Set(cacheKey, user, CacheOptions);
            _logger.LogDebug("User {UserId} cached for {Minutes} minutes", userId.Value, 10);
        }

        return user;
    }
}
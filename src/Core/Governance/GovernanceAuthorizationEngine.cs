using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace Enterprise.Documentation.Core.Governance;

/// <summary>
/// Enterprise V2 Authorization Engine for agent access control and permissions management.
/// Implements Role-Based Access Control (RBAC) with agent clearance levels and rate limiting.
/// Enhanced from V1 patterns with async support and comprehensive permission validation.
/// </summary>
public class GovernanceAuthorizationEngine
{
    private readonly ILogger<GovernanceAuthorizationEngine> _logger;
    private readonly IConfiguration _configuration;
    private readonly ActivitySource _activitySource;
    
    // Agent permission cache to avoid repeated lookups
    private readonly Dictionary<string, CachedAgentPermissions> _permissionCache = new();
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(15);
    
    // Rate limiting tracking
    private readonly Dictionary<string, RateLimitTracker> _rateLimitTrackers = new();

    // Enterprise security configurations
    private readonly Dictionary<AgentClearanceLevel, TableAccessPolicy> _accessPolicies = new()
    {
        [AgentClearanceLevel.Restricted] = new()
        {
            AllowedTables = new[] { "Documents", "Templates", "PublicData" },
            ForbiddenTables = new[] { "Users", "Security", "Admin", "Audit" },
            MaxQueriesPerHour = 100,
            RequiresApproval = false
        },
        [AgentClearanceLevel.Standard] = new()
        {
            AllowedTables = new[] { "Documents", "Templates", "Users", "Metadata", "Reports" },
            ForbiddenTables = new[] { "Security", "Admin", "Audit", "Financial" },
            MaxQueriesPerHour = 500,
            RequiresApproval = false
        },
        [AgentClearanceLevel.Elevated] = new()
        {
            AllowedTables = new[] { "Documents", "Templates", "Users", "Metadata", "Reports", "Analytics", "Logs" },
            ForbiddenTables = new[] { "Admin", "Security" },
            MaxQueriesPerHour = 1000,
            RequiresApproval = true
        },
        [AgentClearanceLevel.Administrator] = new()
        {
            AllowedTables = Array.Empty<string>(), // All tables allowed
            ForbiddenTables = Array.Empty<string>(), // No restrictions
            MaxQueriesPerHour = 10000,
            RequiresApproval = true
        }
    };

    public GovernanceAuthorizationEngine(
        ILogger<GovernanceAuthorizationEngine> logger,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _activitySource = new ActivitySource($"{nameof(GovernanceAuthorizationEngine)}-v2");
    }

    /// <summary>
    /// Authorizes agent access to requested resources based on clearance level and policies.
    /// Implements comprehensive RBAC with rate limiting and permission validation.
    /// </summary>
    public async Task<GovernanceAuthorizationResult> AuthorizeAgentAccessAsync(
        string agentId, 
        IEnumerable<string> requestedTables, 
        AgentClearanceLevel clearanceLevel, 
        CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("AuthorizeAgentAccess");
        activity?.SetTag("agent.id", agentId);
        activity?.SetTag("clearance.level", clearanceLevel.ToString());
        activity?.SetTag("tables.requested", requestedTables.Count());

        try
        {
            _logger.LogDebug("Authorizing access for agent {AgentId} with clearance {ClearanceLevel} to tables: {Tables}", 
                agentId, clearanceLevel, string.Join(", ", requestedTables));

            // Step 1: Validate agent existence and status
            var agentValidation = await ValidateAgentAsync(agentId, cancellationToken);
            if (!agentValidation.IsValid)
            {
                _logger.LogWarning("Agent validation failed for {AgentId}: {Reason}", agentId, agentValidation.Reason);
                activity?.SetTag("authorization.validation_failed", true);
                return GovernanceAuthorizationResult.Deny(agentValidation.Reason);
            }

            // Step 2: Check rate limiting
            var rateLimitCheck = CheckRateLimit(agentId, clearanceLevel);
            if (rateLimitCheck.RequestsRemaining <= 0)
            {
                _logger.LogWarning("Rate limit exceeded for agent {AgentId}: {Remaining} requests remaining", 
                    agentId, rateLimitCheck.RequestsRemaining);
                activity?.SetTag("authorization.rate_limited", true);
                return GovernanceAuthorizationResult.Deny("Rate limit exceeded") with 
                { 
                    RateLimit = rateLimitCheck 
                };
            }

            // Step 3: Validate table access permissions
            var tableValidation = ValidateTableAccess(requestedTables, clearanceLevel);
            if (!tableValidation.IsAuthorized)
            {
                _logger.LogWarning("Table access denied for agent {AgentId} with clearance {ClearanceLevel}: {Reason}", 
                    agentId, clearanceLevel, tableValidation.DenialReason);
                activity?.SetTag("authorization.table_access_denied", true);
                return GovernanceAuthorizationResult.Deny(tableValidation.DenialReason ?? "Table access denied");
            }

            // Step 4: Check for special approval requirements
            var policy = _accessPolicies[clearanceLevel];
            if (policy.RequiresApproval && IsHighRiskOperation(requestedTables))
            {
                _logger.LogInformation("High-risk operation detected for agent {AgentId}, approval may be required", agentId);
                activity?.SetTag("authorization.high_risk", true);
                // In production, this might queue the request for human approval
            }

            // Step 6: Update rate limiting tracker
            UpdateRateLimit(agentId);

            // Step 6: Cache permissions for future requests
            CacheAgentPermissions(agentId, clearanceLevel, tableValidation.AuthorizedTables);

            var result = GovernanceAuthorizationResult.Allow(clearanceLevel, tableValidation.AuthorizedTables) with
            {
                RateLimit = rateLimitCheck,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            };

            _logger.LogInformation("Access authorized for agent {AgentId} to {TableCount} tables with {RequestsRemaining} requests remaining", 
                agentId, tableValidation.AuthorizedTables.Count, rateLimitCheck.RequestsRemaining);

            activity?.SetTag("authorization.success", true);
            activity?.SetTag("authorization.tables_granted", tableValidation.AuthorizedTables.Count);
            activity?.SetTag("authorization.requests_remaining", rateLimitCheck.RequestsRemaining);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authorization failed for agent {AgentId}", agentId);
            activity?.SetTag("authorization.error", ex.Message);
            return GovernanceAuthorizationResult.Deny($"Authorization error: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates agent existence and active status.
    /// </summary>
    private async Task<AgentValidationResult> ValidateAgentAsync(string agentId, CancellationToken cancellationToken)
    {
        // Check cache first
        if (_permissionCache.TryGetValue(agentId, out var cached) && 
            cached.ExpiresAt > DateTime.UtcNow)
        {
            return new AgentValidationResult { IsValid = cached.IsActive };
        }

        // In production, this would query the agent registry database
        // For now, we'll simulate validation logic
        await Task.Delay(10, cancellationToken);

        // Basic validation rules
        if (string.IsNullOrWhiteSpace(agentId))
        {
            return new AgentValidationResult 
            { 
                IsValid = false, 
                Reason = "Agent ID cannot be empty" 
            };
        }

        if (agentId.Length < 3 || agentId.Length > 100)
        {
            return new AgentValidationResult 
            { 
                IsValid = false, 
                Reason = "Agent ID must be between 3 and 100 characters" 
            };
        }

        // Check against blacklisted agents
        var blacklistedAgents = _configuration.GetSection("Governance:BlacklistedAgents").GetChildren()
            .Select(x => x.Value ?? string.Empty)
            .Where(x => !string.IsNullOrEmpty(x))
            .ToArray();
        if (blacklistedAgents.Contains(agentId, StringComparer.OrdinalIgnoreCase))
        {
            return new AgentValidationResult 
            { 
                IsValid = false, 
                Reason = "Agent access has been revoked" 
            };
        }

        // Assume agent is valid if it passes basic checks
        return new AgentValidationResult { IsValid = true };
    }

    /// <summary>
    /// Checks rate limits for the agent based on clearance level.
    /// </summary>
    private RateLimitInfo CheckRateLimit(string agentId, AgentClearanceLevel clearanceLevel)
    {
        var policy = _accessPolicies[clearanceLevel];
        var now = DateTime.UtcNow;
        var windowStart = now.Subtract(TimeSpan.FromHours(1)); // 1-hour window

        if (!_rateLimitTrackers.TryGetValue(agentId, out var tracker))
        {
            tracker = new RateLimitTracker
            {
                AgentId = agentId,
                WindowStart = windowStart,
                RequestCount = 0
            };
            _rateLimitTrackers[agentId] = tracker;
        }

        // Reset window if expired
        if (tracker.WindowStart < windowStart)
        {
            tracker.WindowStart = windowStart;
            tracker.RequestCount = 0;
        }

        var remaining = Math.Max(0, policy.MaxQueriesPerHour - tracker.RequestCount);
        
        return new RateLimitInfo
        {
            RequestsRemaining = remaining,
            WindowResetTime = tracker.WindowStart.AddHours(1),
            MaxRequestsPerWindow = policy.MaxQueriesPerHour
        };
    }

    /// <summary>
    /// Validates table access based on clearance level policies.
    /// </summary>
    private TableAccessResult ValidateTableAccess(IEnumerable<string> requestedTables, AgentClearanceLevel clearanceLevel)
    {
        var policy = _accessPolicies[clearanceLevel];
        var tables = requestedTables.ToList();
        var authorizedTables = new List<string>();

        foreach (var table in tables)
        {
            var normalizedTable = table.ToLowerInvariant();

            // Check forbidden tables first
            if (policy.ForbiddenTables.Any(ft => normalizedTable.Contains(ft.ToLowerInvariant())))
            {
                return new TableAccessResult
                {
                    IsAuthorized = false,
                    DenialReason = $"Access to table '{table}' is forbidden for clearance level {clearanceLevel}"
                };
            }

            // For Administrator level, all non-forbidden tables are allowed
            if (clearanceLevel == AgentClearanceLevel.Administrator)
            {
                authorizedTables.Add(table);
                continue;
            }

            // Check allowed tables
            if (policy.AllowedTables.Any(at => normalizedTable.Contains(at.ToLowerInvariant())))
            {
                authorizedTables.Add(table);
            }
            else
            {
                return new TableAccessResult
                {
                    IsAuthorized = false,
                    DenialReason = $"Access to table '{table}' is not permitted for clearance level {clearanceLevel}"
                };
            }
        }

        return new TableAccessResult
        {
            IsAuthorized = true,
            AuthorizedTables = authorizedTables
        };
    }

    /// <summary>
    /// Determines if the operation involves high-risk tables or patterns.
    /// </summary>
    private bool IsHighRiskOperation(IEnumerable<string> tables)
    {
        var highRiskTables = new[] { "users", "security", "admin", "financial", "audit", "logs" };
        return tables.Any(table => highRiskTables.Any(hrt => 
            table.ToLowerInvariant().Contains(hrt)));
    }

    /// <summary>
    /// Updates rate limit tracker after successful authorization.
    /// </summary>
    private void UpdateRateLimit(string agentId)
    {
        if (_rateLimitTrackers.TryGetValue(agentId, out var tracker))
        {
            tracker.RequestCount++;
            tracker.LastRequestTime = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Caches agent permissions for performance optimization.
    /// </summary>
    private void CacheAgentPermissions(string agentId, AgentClearanceLevel clearanceLevel, IReadOnlyList<string> authorizedTables)
    {
        _permissionCache[agentId] = new CachedAgentPermissions
        {
            AgentId = agentId,
            ClearanceLevel = clearanceLevel,
            AuthorizedTables = authorizedTables,
            IsActive = true,
            CachedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(_cacheExpiration)
        };

        // Clean up expired cache entries periodically
        CleanupExpiredCache();
    }

    /// <summary>
    /// Removes expired entries from the permission cache.
    /// </summary>
    private void CleanupExpiredCache()
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _permissionCache
            .Where(kvp => kvp.Value.ExpiresAt <= now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _permissionCache.Remove(key);
        }

        // Also cleanup old rate limit trackers (older than 24 hours)
        var oldTrackers = _rateLimitTrackers
            .Where(kvp => kvp.Value.LastRequestTime < now.AddHours(-24))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in oldTrackers)
        {
            _rateLimitTrackers.Remove(key);
        }
    }

    public void Dispose()
    {
        _activitySource?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Table access policy configuration for different clearance levels.
/// </summary>
public record TableAccessPolicy
{
    public string[] AllowedTables { get; init; } = Array.Empty<string>();
    public string[] ForbiddenTables { get; init; } = Array.Empty<string>();
    public int MaxQueriesPerHour { get; init; }
    public bool RequiresApproval { get; init; }
}

/// <summary>
/// Agent validation result.
/// </summary>
public record AgentValidationResult
{
    public bool IsValid { get; init; }
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// Table access validation result.
/// </summary>
public record TableAccessResult
{
    public bool IsAuthorized { get; init; }
    public string? DenialReason { get; init; }
    public IReadOnlyList<string> AuthorizedTables { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Cached agent permissions for performance optimization.
/// </summary>
public record CachedAgentPermissions
{
    public string AgentId { get; init; } = string.Empty;
    public AgentClearanceLevel ClearanceLevel { get; init; }
    public IReadOnlyList<string> AuthorizedTables { get; init; } = Array.Empty<string>();
    public bool IsActive { get; init; }
    public DateTime CachedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
}

/// <summary>
/// Rate limiting tracker for agents.
/// </summary>
public class RateLimitTracker
{
    public string AgentId { get; init; } = string.Empty;
    public DateTime WindowStart { get; set; }
    public int RequestCount { get; set; }
    public DateTime LastRequestTime { get; set; } = DateTime.UtcNow;
}
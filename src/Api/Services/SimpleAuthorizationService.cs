using Enterprise.Documentation.Core.Application.Interfaces;
using Enterprise.Documentation.Core.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Enterprise.Documentation.Api.Services;

/// <summary>
/// Implementation of IAuthorizationService with role-based and permission-based authorization.
/// </summary>
public class SimpleAuthorizationService : IAuthorizationService
{
    private readonly ILogger<SimpleAuthorizationService> _logger;

    /// <summary>
    /// Standard permissions mapped to roles
    /// </summary>
    private static readonly Dictionary<string, UserRole[]> PermissionRoleMap = new()
    {
        ["documents:read"] = new[] { UserRole.Viewer, UserRole.Contributor, UserRole.Manager, UserRole.Administrator },
        ["documents:create"] = new[] { UserRole.Contributor, UserRole.Manager, UserRole.Administrator },
        ["documents:update"] = new[] { UserRole.Contributor, UserRole.Manager, UserRole.Administrator },
        ["documents:delete"] = new[] { UserRole.Manager, UserRole.Administrator },
        ["documents:approve"] = new[] { UserRole.Manager, UserRole.Administrator },
        ["documents:publish"] = new[] { UserRole.Manager, UserRole.Administrator },
        ["users:read"] = new[] { UserRole.Manager, UserRole.Administrator },
        ["users:create"] = new[] { UserRole.Administrator },
        ["users:update"] = new[] { UserRole.Administrator },
        ["users:delete"] = new[] { UserRole.Administrator },
        ["templates:read"] = new[] { UserRole.Viewer, UserRole.Contributor, UserRole.Manager, UserRole.Administrator },
        ["templates:create"] = new[] { UserRole.Manager, UserRole.Administrator },
        ["templates:update"] = new[] { UserRole.Manager, UserRole.Administrator },
        ["templates:delete"] = new[] { UserRole.Administrator }
    };

    public SimpleAuthorizationService(ILogger<SimpleAuthorizationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<AuthorizationResult> AuthorizeAsync(
        User user,
        string[] requiredPermissions,
        object? resource = null,
        CancellationToken cancellationToken = default)
    {
        // SECURITY FIX: Properly check user authentication
        if (user == null)
        {
            _logger.LogWarning("Authorization failed: User not authenticated");
            return Task.FromResult(new AuthorizationResult(false, "User not authenticated"));
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Authorization failed: User {UserId} is not active", user.Id.Value);
            return Task.FromResult(new AuthorizationResult(false, "User account is not active"));
        }

        // SECURITY FIX: Check actual permissions against user roles
        if (requiredPermissions == null || requiredPermissions.Length == 0)
        {
            // No specific permissions required, just authenticated
            return Task.FromResult(new AuthorizationResult(true, null));
        }

        var missingPermissions = new List<string>();

        foreach (var permission in requiredPermissions)
        {
            if (!HasPermission(user, permission))
            {
                missingPermissions.Add(permission);
            }
        }

        if (missingPermissions.Any())
        {
            var message = $"Missing permissions: {string.Join(", ", missingPermissions)}";
            _logger.LogWarning("Authorization failed for user {UserId}: {Message}", user.Id.Value, message);
            return Task.FromResult(new AuthorizationResult(false, message));
        }

        _logger.LogDebug("Authorization succeeded for user {UserId} with permissions: {Permissions}",
            user.Id.Value, string.Join(", ", requiredPermissions));
        return Task.FromResult(new AuthorizationResult(true, null));
    }

    private static bool HasPermission(User user, string permission)
    {
        // Check if permission exists in map
        if (!PermissionRoleMap.TryGetValue(permission, out var allowedRoles))
        {
            // Unknown permission - deny by default
            return false;
        }

        // Check if user has any of the allowed roles
        return user.Roles.Any(userRole => allowedRoles.Contains(userRole));
    }

    public Task<bool> CanAccessDocumentAsync(
        User user, 
        Document document, 
        CancellationToken cancellationToken = default)
    {
        // Simple implementation - check if user can access based on security clearance
        // In production, this would check detailed permissions
        if (user == null) return Task.FromResult(false);

        // For now, basic security clearance check
        return Task.FromResult(user.CanAccessSecurityLevel(document.SecurityClassification));
    }

    public Task<bool> CanApproveDocumentsAsync(
        User user, 
        CancellationToken cancellationToken = default)
    {
        // Simple implementation - check if user has approval role
        // In production, this would check specific approval permissions
        if (user == null) return Task.FromResult(false);

        // For now, check if user has Manager or Administrator role
        return Task.FromResult(user.Roles.Any(role => role == UserRole.Manager || role == UserRole.Administrator));
    }
}
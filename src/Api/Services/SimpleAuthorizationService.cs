using Enterprise.Documentation.Core.Application.Interfaces;
using Enterprise.Documentation.Core.Domain.Entities;

namespace Enterprise.Documentation.Api.Services;

/// <summary>
/// Simple implementation of IAuthorizationService for development/testing.
/// In production, this would integrate with a proper authorization system.
/// </summary>
public class SimpleAuthorizationService : IAuthorizationService
{
    public Task<AuthorizationResult> AuthorizeAsync(
        User user, 
        string[] requiredPermissions, 
        object? resource = null, 
        CancellationToken cancellationToken = default)
    {
        // Simple implementation - in production this would check actual permissions
        // For now, allow if user is authenticated
        if (user != null)
        {
            return Task.FromResult(new AuthorizationResult(true, null));
        }

        return Task.FromResult(new AuthorizationResult(false, "User not authenticated"));
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
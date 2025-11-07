
using MediatR;
using Microsoft.Extensions.Logging;
using Enterprise.Documentation.Core.Application.Interfaces;

namespace Enterprise.Documentation.Core.Application.Behaviors;

/// <summary>
/// Pipeline behavior that performs authorization checks for all secured requests.
/// Integrates with the enterprise governance layer for access control.
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<AuthorizationBehavior<TRequest, TResponse>> _logger;

    public AuthorizationBehavior(
        ICurrentUserService currentUserService,
        IAuthorizationService authorizationService,
        ILogger<AuthorizationBehavior<TRequest, TResponse>> logger)
    {
        _currentUserService = currentUserService;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Check if request requires authorization
        if (request is not IAuthorizedRequest authorizedRequest)
        {
            return await next();
        }

        var currentUser = await _currentUserService.GetCurrentUserAsync(cancellationToken);
        if (currentUser == null)
        {
            _logger.LogWarning(
                "Unauthorized access attempt to {RequestName}: No current user context",
                typeof(TRequest).Name);
            
            throw new UnauthorizedAccessException("User must be authenticated to perform this action");
        }

        var authorizationResult = await _authorizationService.AuthorizeAsync(
            currentUser,
            authorizedRequest.RequiredPermissions,
            authorizedRequest.Resource,
            cancellationToken);

        if (!authorizationResult.IsAuthorized)
        {
            _logger.LogWarning(
                "Access denied for user {UserId} to {RequestName}: {Reason}",
                currentUser.Id, typeof(TRequest).Name, authorizationResult.FailureReason);
            
            throw new ForbiddenAccessException(
                $"Access denied: {authorizationResult.FailureReason}");
        }

        _logger.LogDebug(
            "Authorization successful for user {UserId} to {RequestName}",
            currentUser.Id, typeof(TRequest).Name);

        return await next();
    }
}

/// <summary>
/// Interface for requests that require authorization.
/// </summary>
public interface IAuthorizedRequest
{
    /// <summary>
    /// Required permissions to execute this request.
    /// </summary>
    string[] RequiredPermissions { get; }

    /// <summary>
    /// Resource being accessed (optional, for resource-based authorization).
    /// </summary>
    object? Resource { get; }
}

/// <summary>
/// Exception thrown when user lacks sufficient permissions.
/// </summary>
public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string message) : base(message) { }
    public ForbiddenAccessException(string message, Exception innerException) : base(message, innerException) { }
}
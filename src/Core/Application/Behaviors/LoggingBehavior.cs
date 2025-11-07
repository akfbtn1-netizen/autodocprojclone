
using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Enterprise.Documentation.Core.Application.Behaviors;

/// <summary>
/// Pipeline behavior that logs all requests and responses with timing information.
/// Provides comprehensive operational observability for all CQRS operations.
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var requestId = Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestId"] = requestId,
            ["RequestName"] = requestName
        });

        _logger.LogInformation(
            "Starting request {RequestName} with ID {RequestId}",
            requestName, requestId);

        try
        {
            var response = await next();
            
            stopwatch.Stop();
            
            _logger.LogInformation(
                "Completed request {RequestName} with ID {RequestId} in {ElapsedMilliseconds}ms",
                requestName, requestId, stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(ex,
                "Request {RequestName} with ID {RequestId} failed after {ElapsedMilliseconds}ms: {ErrorMessage}",
                requestName, requestId, stopwatch.ElapsedMilliseconds, ex.Message);
            
            throw;
        }
    }
}
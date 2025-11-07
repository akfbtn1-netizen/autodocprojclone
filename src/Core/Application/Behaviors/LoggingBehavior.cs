
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

        using var scope = CreateLoggingScope(requestName, requestId);
        LogRequestStart(requestName, requestId);

        try
        {
            var response = await next();
            LogRequestSuccess(requestName, requestId, stopwatch);
            return response;
        }
        catch (Exception ex)
        {
            LogRequestError(requestName, requestId, stopwatch, ex);
            throw;
        }
    }

    private IDisposable CreateLoggingScope(string requestName, Guid requestId)
    {
        return _logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestId"] = requestId,
            ["RequestName"] = requestName
        })!;
    }

    private void LogRequestStart(string requestName, Guid requestId)
    {
        _logger.LogInformation(
            "Starting request {RequestName} with ID {RequestId}",
            requestName, requestId);
    }

    private void LogRequestSuccess(string requestName, Guid requestId, Stopwatch stopwatch)
    {
        stopwatch.Stop();
        _logger.LogInformation(
            "Completed request {RequestName} with ID {RequestId} in {ElapsedMilliseconds}ms",
            requestName, requestId, stopwatch.ElapsedMilliseconds);
    }

    private void LogRequestError(string requestName, Guid requestId, Stopwatch stopwatch, Exception ex)
    {
        stopwatch.Stop();
        _logger.LogError(ex,
            "Request {RequestName} with ID {RequestId} failed after {ElapsedMilliseconds}ms: {ErrorMessage}",
            requestName, requestId, stopwatch.ElapsedMilliseconds, ex.Message);
    }
}
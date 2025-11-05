using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Azure.Messaging.ServiceBus;
using Polly;
using Polly.CircuitBreaker;
using System.Text.Json;
using System.Diagnostics;

namespace Enterprise.Documentation.Shared.BaseAgent;

/// <summary>
/// Base class for all agents providing common enterprise functionality:
/// - Service Bus integration with retry policies
/// - Configuration management
/// - Health checks
/// - Metrics collection
/// - Structured logging
/// - Circuit breaker patterns
/// </summary>
public abstract class BaseAgent : IAgent
{
    private const int DEFAULT_MAX_RETRIES = 3;
    private const int DEFAULT_CIRCUIT_BREAKER_THRESHOLD = 5;
    private const int DEFAULT_CIRCUIT_BREAKER_DURATION_SECONDS = 60;

    protected IConfiguration Configuration { get; }
    protected ILogger Logger { get; }

    private readonly ServiceBusClient? _serviceBusClient;
    private readonly ResiliencePipeline _resiliencePipeline;
    private bool _disposed;

    /// <inheritdoc/>
    public string AgentName { get; }

    /// <inheritdoc/>
    public string Version { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseAgent"/> class.
    /// </summary>
    /// <param name="configuration">Configuration</param>
    /// <param name="logger">Logger</param>
    protected BaseAgent(
        IConfiguration configuration,
        ILogger logger)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Get agent name and version from derived type
        AgentName = GetType().Name;
        Version = GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0";

        // Initialize Service Bus if configured
        var serviceBusConnectionString = configuration["ServiceBus:ConnectionString"];
        if (!string.IsNullOrEmpty(serviceBusConnectionString))
        {
            _serviceBusClient = new ServiceBusClient(serviceBusConnectionString);
            Logger.LogInformation("Service Bus initialized for {AgentName}", AgentName);
        }
        else
        {
            Logger.LogWarning("Service Bus not configured for {AgentName}", AgentName);
        }

        // Setup resilience pipeline (Polly v8)
        _resiliencePipeline = CreateResiliencePipeline();
    }

    /// <inheritdoc/>
    public abstract Task ExecuteAsync(CancellationToken ct);

    /// <inheritdoc/>
    public abstract Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct);

    /// <summary>
    /// Publishes an event to Service Bus topic with resilience patterns.
    /// </summary>
    /// <typeparam name="T">Event type</typeparam>
    /// <param name="topicName">Topic name</param>
    /// <param name="payload">Event payload</param>
    /// <param name="ct">Cancellation token</param>
    protected async Task PublishEventAsync<T>(
        string topicName,
        T payload,
        CancellationToken ct) where T : class
    {
        if (_serviceBusClient == null)
        {
            throw new InvalidOperationException(
                "Service Bus not configured. Cannot publish events.");
        }

        await _resiliencePipeline.ExecuteAsync(async cancellationToken =>
        {
            var sender = _serviceBusClient.CreateSender(topicName);
            await using (sender.ConfigureAwait(false))
            {
                var messageJson = JsonSerializer.Serialize(payload);
                var message = new ServiceBusMessage(messageJson)
                {
                    ContentType = "application/json",
                    MessageId = Guid.NewGuid().ToString(),
                    CorrelationId = Activity.Current?.Id ?? Guid.NewGuid().ToString(),
                    Subject = typeof(T).Name
                };

                // Add agent metadata
                message.ApplicationProperties.Add("AgentName", AgentName);
                message.ApplicationProperties.Add("AgentVersion", Version);
                message.ApplicationProperties.Add("PublishedAt", DateTimeOffset.UtcNow);

                await sender.SendMessageAsync(message, cancellationToken);

                Logger.LogInformation(
                    "Published event to topic {Topic}: {MessageId} (Type: {EventType})",
                    topicName,
                    message.MessageId,
                    typeof(T).Name);
            }
        }, ct);
    }

    /// <summary>
    /// Creates a Service Bus processor for subscribing to topics.
    /// </summary>
    /// <param name="topicName">Topic name</param>
    /// <param name="subscriptionName">Subscription name</param>
    /// <returns>Service Bus processor</returns>
    protected ServiceBusProcessor CreateProcessor(
        string topicName,
        string subscriptionName)
    {
        if (_serviceBusClient == null)
        {
            throw new InvalidOperationException(
                "Service Bus not configured. Cannot create processors.");
        }

        var processorOptions = new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = Configuration.GetValue<int>(
                $"{AgentName}:MaxConcurrentCalls", 1),
            AutoCompleteMessages = false,
            MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5)
        };

        var processor = _serviceBusClient.CreateProcessor(
            topicName,
            subscriptionName,
            processorOptions);

        Logger.LogInformation(
            "Created processor for topic {Topic}, subscription {Subscription}",
            topicName,
            subscriptionName);

        return processor;
    }

    /// <summary>
    /// Gets a secret from configuration (supports Azure Key Vault via configuration).
    /// </summary>
    /// <param name="secretName">Secret name</param>
    /// <returns>Secret value</returns>
    protected string GetSecret(string secretName)
    {
        var secretValue = Configuration[secretName];

        if (string.IsNullOrEmpty(secretValue))
        {
            throw new InvalidOperationException(
                $"Secret '{secretName}' not found in configuration.");
        }

        return secretValue;
    }

    /// <summary>
    /// Gets a secret from configuration or returns default value.
    /// </summary>
    /// <param name="secretName">Secret name</param>
    /// <param name="defaultValue">Default value if not found</param>
    /// <returns>Secret value or default</returns>
    protected string GetSecretOrDefault(string secretName, string defaultValue)
    {
        return Configuration[secretName] ?? defaultValue;
    }

    /// <summary>
    /// Creates resilience pipeline with retry and circuit breaker.
    /// </summary>
    private ResiliencePipeline CreateResiliencePipeline()
    {
        var maxRetries = Configuration.GetValue<int>(
            $"{AgentName}:MaxRetries", DEFAULT_MAX_RETRIES);

        var circuitBreakerThreshold = Configuration.GetValue<int>(
            $"{AgentName}:CircuitBreakerThreshold", DEFAULT_CIRCUIT_BREAKER_THRESHOLD);

        var circuitBreakerDuration = TimeSpan.FromSeconds(
            Configuration.GetValue<int>(
                $"{AgentName}:CircuitBreakerDurationSeconds",
                DEFAULT_CIRCUIT_BREAKER_DURATION_SECONDS));

        return new ResiliencePipelineBuilder()
            .AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = maxRetries,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = Polly.DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    Logger.LogWarning(
                        args.Outcome.Exception,
                        "Retry attempt {Attempt} after {Delay}ms",
                        args.AttemptNumber,
                        args.RetryDelay.TotalMilliseconds);
                    return ValueTask.CompletedTask;
                },
                ShouldHandle = new Polly.PredicateBuilder().Handle<ServiceBusException>()
                    .Handle<TimeoutException>()
            })
            .AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = circuitBreakerThreshold,
                BreakDuration = circuitBreakerDuration,
                SamplingDuration = TimeSpan.FromSeconds(30),
                OnOpened = args =>
                {
                    Logger.LogError(
                        args.Outcome.Exception,
                        "Circuit breaker opened for {Duration}ms",
                        circuitBreakerDuration.TotalMilliseconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    Logger.LogInformation("Circuit breaker closed (reset)");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    Logger.LogInformation("Circuit breaker half-opened (testing)");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    public virtual void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _serviceBusClient?.DisposeAsync().AsTask().Wait();
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}
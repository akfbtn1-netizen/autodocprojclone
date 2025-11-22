// filepath: c:\Projects\EnterpriseDocumentationPlatform.V2\src\Shared\Extensions\ServiceCollectionExtensions.cs

using System.Diagnostics;
using Enterprise.Documentation.Shared.Configuration;
using Enterprise.Documentation.Shared.Contracts.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Enterprise.Documentation.Shared.Extensions;

/// <summary>
/// Extension methods for IServiceCollection to register Enterprise Documentation Platform services.
/// Provides standardized service registration for all agents.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers core Enterprise Documentation Platform infrastructure services.
    /// This should be called by all agents to ensure consistent service registration.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <param name="agentId">Unique identifier for the agent</param>
    /// <returns>Service collection for method chaining</returns>
    public static IServiceCollection AddEnterpriseDocumentationCore(
        this IServiceCollection services,
        IConfiguration configuration,
        string agentId)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));
        if (string.IsNullOrWhiteSpace(agentId)) throw new ArgumentNullException(nameof(agentId));

        // Register agent configuration
        services.AddSingleton<IAgentConfiguration>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<AgentConfiguration>>();
            var environment = configuration["Environment"] ?? "Development";
            return new AgentConfiguration(configuration, logger, agentId, environment);
        });

        // Register telemetry
        services.AddSingleton(provider => new ActivitySource($"Enterprise.Documentation.{agentId}"));

        return services;
    }

    /// <summary>
    /// Registers telemetry and observability services.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <param name="agentId">Unique identifier for the agent</param>
    /// <returns>Service collection for method chaining</returns>
    public static IServiceCollection AddEnterpriseDocumentationTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string agentId)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));
        if (string.IsNullOrWhiteSpace(agentId)) throw new ArgumentNullException(nameof(agentId));

        // Register OpenTelemetry
        services.AddSingleton(provider => new ActivitySource($"Enterprise.Documentation.{agentId}"));

        // Note: Application Insights can be configured separately if needed
        // by adding Microsoft.ApplicationInsights.AspNetCore package

        return services;
    }

    /// <summary>
    /// Event publisher implementation using the message bus.
    /// This will be expanded when the infrastructure projects are available.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Service collection for method chaining</returns>
    public static IServiceCollection AddEnterpriseDocumentationMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        // Register event publisher interface
        services.AddScoped<IEventPublisher, EventPublisher>();

        return services;
    }
}

/// <summary>
/// Basic event publisher implementation.
/// Will be enhanced when message bus infrastructure is available.
/// </summary>
internal class EventPublisher : IEventPublisher
{
    private readonly ILogger<EventPublisher> _logger;

    public EventPublisher(ILogger<EventPublisher> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishAsync<TEvent>(TEvent eventData, CancellationToken cancellationToken = default) 
        where TEvent : IBaseEvent
    {
        // TODO: Implement actual message bus publishing when infrastructure is ready
        _logger.LogInformation("Publishing event {EventType} with ID {EventId}", 
            typeof(TEvent).Name, eventData.EventId);
        
        await Task.CompletedTask;
    }

    public async Task PublishBatchAsync<TEvent>(IEnumerable<TEvent> events, CancellationToken cancellationToken = default) 
        where TEvent : IBaseEvent
    {
        var eventList = events.ToList();
        
        // TODO: Implement actual batch message bus publishing when infrastructure is ready
        _logger.LogInformation("Publishing batch of {EventCount} events of type {EventType}", 
            eventList.Count, typeof(TEvent).Name);
            
        await Task.CompletedTask;
    }
}
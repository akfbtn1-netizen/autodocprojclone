# Azure Service Bus + MassTransit - References

## Official Documentation

### Azure Service Bus
- Overview: https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-messaging-overview
- Queues, Topics, Subscriptions: https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-queues-topics-subscriptions
- Dead Letter Queues: https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-dead-letter-queues
- Topic Filters: https://learn.microsoft.com/en-us/azure/service-bus-messaging/topic-filters
- Best Practices Performance: https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-performance-improvements
- Advanced Features: https://learn.microsoft.com/en-us/azure/service-bus-messaging/advanced-features-overview

### MassTransit
- Documentation: https://masstransit.io/documentation/
- Azure Service Bus Transport: https://masstransit.io/documentation/transports/azure-service-bus
- Azure Service Bus Configuration: https://masstransit.io/documentation/configuration/transports/azure-service-bus
- Quick Start: https://masstransit.io/quick-starts/azure-service-bus
- Saga State Machine: https://masstransit.io/documentation/patterns/saga/state-machine
- Saga Persistence: https://masstransit.io/documentation/patterns/saga/persistence
- Consumer Sagas: https://masstransit.io/documentation/patterns/saga/consumer-sagas

### NuGet Packages
- MassTransit: https://www.nuget.org/packages/MassTransit/
- MassTransit.Azure.ServiceBus.Core: https://www.nuget.org/packages/MassTransit.Azure.ServiceBus.Core/
- MassTransit.EntityFrameworkCore: https://www.nuget.org/packages/MassTransit.EntityFrameworkCore/

## Community Resources

### Milan Jovanović (2024)
- Using MassTransit with RabbitMQ and Azure Service Bus: https://www.milanjovanovic.tech/blog/using-masstransit-with-rabbitmq-and-azure-service-bus
- Implementing the Saga Pattern With MassTransit: https://www.milanjovanovic.tech/blog/implementing-the-saga-pattern-with-masstransit

### Medium Articles
- Saga State Machine & MassTransit: https://medium.com/adessoturkey/saga-state-machine-masstransit-automatonymous-request-response-pattern-10f14603964
- Saga Orchestration using MassTransit in .NET: https://medium.com/@ebubekirdinc/saga-orchestration-using-masstransit-in-net-9a2fcb427c1a
- Azure Service Bus — Publish/Subscribe Pattern: https://medium.com/nerd-for-tech/azure-service-bus-publish-subscribe-pattern-178dd44baa36
- Understanding Dead Letter Queue in Azure Service Bus: https://medium.com/@rictorres.uyu/understanding-dead-letter-queue-in-azure-service-bus-with-c-6314902f2aad

### Code Maze
- Creating Resilient Microservices with Polly: https://code-maze.com/creating-resilient-microservices-in-net-with-polly/

### GitHub Repositories
- MassTransit: https://github.com/MassTransit/MassTransit
- Sample-AzureServiceBus: https://github.com/MassTransit/Sample-AzureServiceBus
- Polly: https://github.com/App-vNext/Polly

## Resilience Patterns

### Polly Documentation
- Official Docs: https://www.pollydocs.org/
- Retry Strategy: https://www.pollydocs.org/strategies/retry.html
- Circuit Breaker: https://www.pollydocs.org/strategies/circuit-breaker.html
- GitHub Retry Documentation: https://github.com/App-vNext/Polly/blob/main/docs/strategies/retry.md

### Microsoft Resilience
- Implement HTTP call retries with Polly: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/implement-http-call-retries-exponential-backoff-polly
- Implement Circuit Breaker Pattern: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/implement-circuit-breaker-pattern

## Architecture Patterns

### NServiceBus (Particular Software)
- Topology Documentation: https://docs.particular.net/transports/azure-service-bus/topology
- Topic-per-event topology patterns

### DZone
- Azure Service Bus Dead-letter Queues: https://dzone.com/articles/azure-service-bus-dead-letter-queues-1

## Pricing & Licensing

### Azure Service Bus Tiers
- Basic: Queues only, no topics/subscriptions
- Standard: Topics, subscriptions, 256 KB messages
- Premium: Dedicated resources, 100 MB messages, geo-DR

### MassTransit Licensing (Important!)
- v8: Apache 2.0 (open source) - CURRENT
- v9: Commercial license starting Q1 2026
  - Small/Medium: $400/month or $4,000/year
  - Enterprise: $1,200/month or $12,000/year
- Source: https://antondevtips.com/blog/masstransit-rabbitmq-and-azure-service-bus-is-it-worth-a-commercial-license

## Best Practices Summary

### Message Design
1. Use immutable records for messages
2. Commands = verbs (CreateOrder), Events = past tense (OrderCreated)
3. Include CorrelationId for tracing
4. Use public init properties for System.Text.Json serialization

### Dead Letter Queue
1. Monitor DLQ message count
2. Implement DLQ consumers for analysis
3. Include DeadLetterReason and DeadLetterErrorDescription
4. Consider automated reprocessing for transient failures

### Saga Design
1. Use optimistic concurrency
2. Implement compensation for failures
3. Partition by correlation ID for ordering
4. Use EF Core for reliable persistence

### Resilience
1. Configure retry with exponential backoff
2. Add jitter to prevent thundering herd
3. Use circuit breaker for failing dependencies
4. Implement outbox pattern for dual-write consistency

## Version History
- 2026-01-03: Initial compilation from 60+ sources

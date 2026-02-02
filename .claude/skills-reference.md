# Development Skills Reference

## Available Expert Skills

### Frontend Development
**Skill**: senior-frontend
**Use for**: React, Next.js, TypeScript, Tailwind, component architecture, performance optimization
**Key Patterns**:
- Functional components with hooks
- TypeScript type safety
- Component composition
- Custom hooks for reusable logic
- Performance optimization (memoization, lazy loading)

### API Integration
**Skill**: api-integration-specialist
**Use for**: REST/GraphQL clients, auth flows, error handling, retry logic, webhooks
**Key Patterns**:
- Exponential backoff retry
- Circuit breakers
- Rate limiting
- Type-safe API clients
- Webhook signature verification

### End-to-End Testing
**Skill**: e2e-testing-patterns
**Use for**: Playwright/Cypress tests, page objects, reliable selectors, network mocking
**Key Patterns**:
- Page Object Model
- data-testid selectors
- Proper wait strategies (no fixed timeouts)
- Test fixtures and cleanup
- Network interception

### Agent Development
**Skill**: mcp-builder, agentic-rag-implementation
**Use for**: MCP servers, AI agents, RAG systems, workflow orchestration
**Key Patterns**:
- Tool design for LLM consumption
- Stateless HTTP transport
- Comprehensive API coverage
- Dynamic retrieval strategies
- Multi-agent coordination

### .NET Backend
**Skill**: senior-dotnet, csharp-developer
**Use for**: .NET 8, ASP.NET Core, EF Core, Clean Architecture, CQRS
**Key Patterns**:
- Clean Architecture layers
- CQRS with MediatR
- Repository + Specification pattern
- FluentValidation pipelines
- Result pattern error handling

### Enterprise Architecture
**Skill**: enterprise-clean-architecture
**Use for**: DDD, Vertical Slice, Modular Monolith patterns
**Key Patterns**:
- Domain-driven design
- Bounded contexts
- Aggregate roots
- Domain events
- Use case handlers

### Azure Integration
**Skill**: azure-openai-integration, azure-servicebus-masstransit
**Use for**: Azure OpenAI, Service Bus, event-driven architecture
**Key Patterns**:
- GPT-5/o-series models
- Semantic Kernel integration
- Topic/subscription messaging
- Saga orchestration
- Retry policies

## Project-Specific Context

### Current Project: Enterprise Documentation Platform V2

**Stack**:
- Backend: .NET 8, ASP.NET Core, EF Core
- Frontend: React, TypeScript, Tailwind
- Database: SQL Server
- Cloud: Azure
- Messaging: Azure Service Bus

**Architecture**: Clean Architecture with CQRS

**Key Modules**:
- Api: Web API controllers and DTOs
- Application: Use cases, MediatR handlers
- Core: Domain entities, interfaces
- Shared: Cross-cutting concerns
- WebApi: API configuration and middleware

## Task Routing

When I receive a task:
1. **Frontend work** → Apply senior-frontend patterns
2. **API integration** → Apply api-integration-specialist patterns
3. **Testing** → Apply e2e-testing-patterns
4. **Agent/AI work** → Apply mcp-builder or agentic-rag-implementation
5. **.NET backend** → Apply senior-dotnet + enterprise-clean-architecture
6. **Azure services** → Apply azure-openai-integration or azure-servicebus-masstransit

## Active Instructions

- Always use proper TypeScript typing
- Follow Clean Architecture separation of concerns
- Use CQRS pattern for application layer
- Implement proper error handling with Result pattern
- Write tests following Page Object Model
- Use data-testid for UI test selectors
- Apply exponential backoff for external API calls
- Use MediatR for command/query handling
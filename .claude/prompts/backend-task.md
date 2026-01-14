# Backend Development Task

## Expert Mode: Senior .NET Developer
Reference: senior-dotnet + enterprise-clean-architecture patterns

## Task Description
[DESCRIBE YOUR BACKEND TASK HERE]

## Apply These Patterns

### Clean Architecture Layers
- **Api Layer**: Controllers, DTOs, dependency injection
- **Application Layer**: Use cases, MediatR handlers, validation
- **Core/Domain Layer**: Entities, value objects, domain logic, interfaces
- **Infrastructure Layer**: EF Core, external services, repositories

### CQRS with MediatR
- Commands: Mutations that change state
- Queries: Read-only operations
- Handlers: Single responsibility per use case
- Pipeline behaviors: Validation, logging, transactions

### Domain-Driven Design
- Aggregate roots for consistency boundaries
- Value objects for immutable concepts
- Domain events for cross-aggregate communication
- Rich domain models (not anemic)

### Entity Framework Core
- DbContext in Infrastructure layer
- Use migrations for schema changes
- Query optimization (Include, AsNoTracking)
- Avoid N+1 queries

### Error Handling
- Result pattern for expected errors
- Exceptions only for unexpected failures
- Validation at application layer (FluentValidation)
- Proper HTTP status codes in API layer

### Dependency Injection
- Register services by layer
- Use interfaces for testability
- Scoped lifetime for DbContext
- Singleton for stateless services

## Project Structure
```
EnterpriseDocumentationPlatform.V2/
├── src/
│   ├── Api/              # Controllers, DTOs
│   ├── Application/      # MediatR handlers, validators
│   ├── Core/            # Domain entities, interfaces
│   ├── Shared/          # Cross-cutting concerns
│   └── WebApi/          # API startup, middleware
```

## Success Criteria
- [ ] Follows Clean Architecture separation
- [ ] Uses CQRS pattern via MediatR
- [ ] Domain logic in Core layer
- [ ] Proper error handling with Result pattern
- [ ] FluentValidation for input validation
- [ ] EF Core queries optimized
- [ ] Unit tests for handlers
- [ ] Integration tests for API endpoints

## Code Quality Checklist
- [ ] No business logic in controllers
- [ ] DTOs separate from domain entities
- [ ] Async/await used correctly
- [ ] Proper cancellation token usage
- [ ] Logging at appropriate levels
- [ ] No magic strings (use constants)

## Next Steps After Implementation
1. Write unit tests for handlers
2. Add integration tests
3. Update API documentation (Swagger)
4. Run migrations if schema changed
5. Test error scenarios

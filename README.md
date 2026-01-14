# Enterprise Documentation Platform V2

## Overview
Enterprise-grade documentation automation platform using multi-agent architecture.

## Architecture
- **Event-Driven:** Azure Service Bus for agent communication
- **Clean Architecture:** Domain-driven design
- **Enterprise Standards:** Enforced code quality, 80%+ test coverage

## Getting Started

### Prerequisites
- .NET 8 SDK
- SQL Server 2019+
- Azure subscription

### Quick Start
```powershell
# Build solution
dotnet build

# Run tests
dotnet test

# Run API
cd src/Api
dotnet run
```

## Projects

| Project | Purpose |
|---------|---------|
| Core.Domain | Domain entities and business logic |
| Core.Application | Use cases and interfaces |
| Core.Infrastructure | External integrations |
| Shared.BaseAgent | Base agent with enterprise patterns |
| Shared.Contracts | Event contracts |
| Api | REST API for frontend |
| Tests.Unit | Unit tests |
| Tests.Integration | Integration tests |

## Status
✅ Foundation Complete - Ready for Agent Migration

## Next Steps
1. Create Enhanced BaseAgent
2. Migrate SchemaDetectorAgent
3. Migrate remaining production agents
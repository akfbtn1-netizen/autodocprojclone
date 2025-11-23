# Enterprise Documentation Platform V2 - Deployment Guide

**Version:** 2.0.0  
**Last Updated:** November 23, 2025  
**Status:** Production Ready

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Project Structure & Dependencies](#project-structure--dependencies)
3. [Database Setup](#database-setup)
4. [Configuration & Environment Variables](#configuration--environment-variables)
5. [Docker Deployment](#docker-deployment)
6. [CI/CD Pipeline](#cicd-pipeline)
7. [Health Check Endpoints](#health-check-endpoints)
8. [Monitoring & Logging](#monitoring--logging)
9. [Azure Infrastructure Requirements](#azure-infrastructure-requirements)
10. [Deployment Procedures](#deployment-procedures)
11. [Troubleshooting](#troubleshooting)

---

## Prerequisites

### Software Requirements

| Component | Version | Notes |
|-----------|---------|-------|
| **.NET SDK** | 8.0.x (LTS) | Core framework |
| **SQL Server** | 2019+ or Azure SQL | Relational database |
| **Node.js** | 20.x | Frontend build (WebApp) |
| **Docker** | 20.10+ | Container runtime |
| **Docker Compose** | 2.0+ | Orchestration |
| **Git** | 2.40+ | Version control |
| **PowerShell** | 7.0+ | Build scripts |
| **Java** | 17+ | SonarQube scanner |

### Azure Services (Production/Staging)

- **Azure SQL Database** - Primary data store
- **Azure Service Bus** - Event-driven communication
- **Azure Key Vault** - Secrets management
- **Azure Container Registry** - Docker image hosting
- **Azure Container Apps** - Serverless compute
- **Azure Application Insights** - Monitoring & observability
- **Azure OpenAI** - AI/ML services (optional)

### Development Environment

```powershell
# Verify .NET installation
dotnet --version

# Expected output: 8.0.x (LTS) - [...]

# Clone repository
git clone <repository-url>
cd autodocprojclone

# Restore dependencies
dotnet restore
```

---

## Project Structure & Dependencies

### Solution Overview

```
EnterpriseDocumentationPlatform.sln
├── src/
│   ├── Core/
│   │   ├── Domain/ (Core.Domain.csproj)
│   │   │   └── Business logic, entities, value objects, domain events
│   │   ├── Application/ (Core.Application.csproj)
│   │   │   └── Use cases, CQRS handlers, interfaces
│   │   ├── Infrastructure/ (Core.Infrastructure.csproj)
│   │   │   └── Repositories, persistence, external integrations
│   │   ├── Governance/ (Core.Governance.csproj)
│   │   │   └── Data governance, security, PII detection, audit
│   │   └── Quality/ (Core.Quality.csproj)
│   │       └── Code quality analysis
│   ├── Api/ (Api.csproj)
│   │   └── REST API, Swagger/OpenAPI, controllers
│   ├── Shared/
│   │   ├── Configuration/ (Shared.Configuration.csproj)
│   │   ├── Contracts/ (Shared.Contracts.csproj)
│   │   └── Extensions/ (Shared.Extensions.csproj)
│   └── WebApp/
│       └── Frontend application (Node.js/React)
├── tests/
│   ├── Unit/ (Tests.Unit.csproj)
│   │   └── Fast, isolated unit tests
│   └── Integration/ (Tests.Integration.csproj)
│       └── Real dependencies, database tests
├── sql/
│   ├── CREATE_MasterIndex_Table.sql
│   ├── CREATE_ApprovalTracking_Table.sql
│   ├── CREATE_DocumentChanges_Table.sql
│   ├── CREATE_DocumentCounters_Table.sql
│   └── CREATE_BatchProcessing_Tables.sql
├── .github/
│   └── workflows/
│       ├── build.yml
│       ├── ci-cd-pipeline.yml
│       ├── ci-cd.yml
│       └── quality-gates.yml
└── docs/
    ├── ARCHITECTURE.md
    ├── CODING_STANDARDS.md
    └── BATCH-PROCESSING-SETUP.md
```

### Core NuGet Dependencies

**Managed Centrally via `Directory.Packages.props`**

#### Framework & Core
- **AutoMapper** 12.0.1 - Object mapping
- **MediatR** 13.1.0 - CQRS pattern
- **FluentValidation** 11.9.2 - Input validation
- **Polly** 8.4.1 - Resilience policies

#### Azure Services
- **Azure.Identity** 1.12.0 - Authentication
- **Azure.Messaging.ServiceBus** 7.17.5 - Event messaging
- **Azure.Security.KeyVault.Secrets** 4.6.0 - Secrets management
- **Azure.AI.OpenAI** 1.0.0-beta.17 - AI services

#### Database
- **Microsoft.EntityFrameworkCore** 8.0.8 - ORM
- **Microsoft.EntityFrameworkCore.SqlServer** 8.0.8 - SQL provider
- **Dapper** 2.1.35 - Lightweight ORM
- **Microsoft.Data.SqlClient** 5.2.1 - SQL client

#### Logging & Monitoring
- **Serilog** 4.0.1 - Structured logging
- **Serilog.AspNetCore** 8.0.2 - ASP.NET integration
- **Microsoft.ApplicationInsights.AspNetCore** 2.22.0 - Telemetry

#### Health Checks
- **Microsoft.Extensions.Diagnostics.HealthChecks** 8.0.8
- **AspNetCore.HealthChecks.Redis** 8.0.1
- **AspNetCore.HealthChecks.AzureServiceBus** 8.0.1
- **AspNetCore.HealthChecks.UI** 8.0.1

#### Background Jobs
- **Hangfire.Core** 1.8.14 - Job scheduler
- **Hangfire.AspNetCore** 1.8.14
- **Hangfire.SqlServer** 1.8.14

#### API & Web
- **Swashbuckle.AspNetCore** 6.6.2 - Swagger/OpenAPI
- **Microsoft.AspNetCore.Authentication.JwtBearer** 8.0.8

#### Testing
- **xUnit** 2.9.0 - Test framework
- **Moq** 4.20.70 - Mocking library
- **FluentAssertions** 6.12.0 - Assertion library
- **Microsoft.NET.Test.Sdk** 17.11.1

#### Document Processing
- **DocumentFormat.OpenXml** 3.0.2 - Word/Office documents
- **EPPlus** 7.2.2 - Excel processing

---

## Database Setup

### Connection String Configuration

**Default (LocalDB Development):**
```
Server=(localdb)\mssqllocaldb;Database=EnterpriseDocumentationDB;Trusted_Connection=true;MultipleActiveResultSets=true
```

**Azure SQL (Production):**
```
Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<database>;Persist Security Info=False;User ID=<username>;Password=<password>;MultipleActiveResultSets=true;Encrypt=true;TrustServerCertificate=false;Connection Timeout=30;
```

### Database Schema Setup

All schema scripts are located in `/sql` directory:

#### 1. Master Index Table
```bash
# File: sql/CREATE_MasterIndex_Table.sql
# Purpose: Stores comprehensive document metadata with 115+ columns
# Schema: DaQa.MasterIndex
# Key Columns:
#   - IndexID (INT IDENTITY)
#   - SourceSystem, SourceDocumentID, SourceFilePath
#   - DocumentTitle, DocumentType, Description
#   - BusinessDomain, BusinessProcess, BusinessOwner
#   - SystemName, DatabaseName, SchemaName, TableName, ColumnName
#   - DataType, DataClassification
# Indexes: PK_MasterIndex (clustered), multiple non-clustered indexes
```

#### 2. Approval Tracking Table
```bash
# File: sql/CREATE_ApprovalTracking_Table.sql
# Purpose: Tracks document approvals and AI feedback
# Schema: DaQa.ApprovalTracking
# Key Columns:
#   - TrackingId (INT IDENTITY)
#   - DocId, Action (Approved/Edited/Rejected/Rerequested)
#   - ApproverUserId, ApproverName, ActionDate
#   - OriginalContent, EditedContent, ContentDiff
#   - RejectionReason, RerequestPrompt, ApproverFeedback
#   - QualityRating (1-5), DocumentType, ChangeType
#   - WasAIEnhanced (bit)
# Views:
#   - vw_ApprovalInsights - Quality metrics by document type
#   - vw_CommonEdits - Common editing patterns for AI training
```

#### 3. Document Changes Table
```bash
# File: sql/CREATE_DocumentChanges_Table.sql
# Purpose: Audit trail of document modifications
# Schema: DaQa.DocumentChanges
# Key Columns:
#   - ChangeId (INT IDENTITY)
#   - DocumentId, ChangeType, ChangedBy
#   - ChangeDate, OldValue, NewValue
#   - Description
```

#### 4. Document Counters Table
```bash
# File: sql/CREATE_DocumentCounters_Table.sql
# Purpose: Tracking document metrics
# Schema: DaQa.DocumentCounters
# Key Columns:
#   - CounterId (INT IDENTITY)
#   - DocumentType, TotalCount, ApprovedCount
#   - UpdatedDate
```

#### 5. Batch Processing Tables
```bash
# File: sql/CREATE_BatchProcessing_Tables.sql
# Purpose: Manages batch job execution and tracking
# Schema: DaQa.[Multiple tables]
# Tables:
#   - BatchJobs - Job metadata
#   - BatchJobItems - Individual items in batch
#   - BatchJobLogs - Execution logs
#   - BatchJobStatistics - Performance metrics
```

### Entity Framework Migrations

**Location:** `/src/Core/Infrastructure/Migrations/`

**Initial Migration:**
- `20251106182120_InitialCreate.cs` - Creates all database tables
- `20251106182120_InitialCreate.Designer.cs` - Migration metadata

**Entities Created:**
- Agents (with health tracking)
- Documents (with approval status & security classification)
- AuditLogs (comprehensive audit trail)
- Users, Templates, Versions
- Domain events for event sourcing

**Apply Migrations:**

```bash
# Apply pending migrations to database
dotnet ef database update -s src/Api/Api.csproj -p src/Core/Infrastructure/Core.Infrastructure.csproj

# OR using Package Manager Console in Visual Studio
Update-Database

# Verify schema
SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo'
```

### Database Initialization Script

```powershell
# File: PowerShell script to initialize database
# 1. Creates schema (DaQa)
# 2. Runs all SQL migration files
# 3. Applies EF Core migrations
# 4. Creates indexes for performance

$sqlScripts = @(
    "sql/CREATE_MasterIndex_Table.sql",
    "sql/CREATE_ApprovalTracking_Table.sql",
    "sql/CREATE_DocumentChanges_Table.sql",
    "sql/CREATE_DocumentCounters_Table.sql",
    "sql/CREATE_BatchProcessing_Tables.sql"
)

foreach ($script in $sqlScripts) {
    sqlcmd -S <server> -d <database> -i $script
}

dotnet ef database update
```

---

## Configuration & Environment Variables

### appsettings.json (Production Template)

**Location:** `/src/Api/appsettings.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "JwtSettings": {
    "SecretKey": "your-super-secret-key-that-is-at-least-32-characters-long-for-production",
    "Issuer": "Enterprise.Documentation.Api",
    "Audience": "Enterprise.Documentation.Client",
    "ExpirationHours": 8
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=tcp:<server>.database.windows.net;Database=<dbname>;User Id=<user>;Password=<password>;"
  }
}
```

### appsettings.Development.json (Optional)

Create in `/src/Api/` for local development:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information",
      "Enterprise.Documentation.Core.Application.Services": "Debug"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=EnterpriseDocumentationDB;Trusted_Connection=true;MultipleActiveResultSets=true"
  },
  "OpenAI": {
    "ApiKey": "sk-your-api-key-here",
    "Model": "gpt-4",
    "Temperature": 0.3,
    "MaxTokens": 1500
  },
  "DocumentGeneration": {
    "BaseOutputPath": "C:\\Temp\\Documentation-Catalog",
    "TemplatesPath": "C:\\Projects\\autodocprojclone\\Templates",
    "NodeExecutable": "node"
  },
  "ExcelSync": {
    "LocalFilePath": "C:\\Users\\YourUser\\Desktop\\Change Spreadsheet.xlsx",
    "SyncIntervalSeconds": 60
  },
  "Hangfire": {
    "DashboardEnabled": true,
    "AllowAnonymousAccess": true,
    "EnableOldBatchCleanup": true,
    "EnableVectorStatsUpdate": false,
    "EnableWeeklyReports": false
  }
}
```

### Environment Variables

**For Local Development (User Secrets):**

```bash
# Set connection string for local development
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\\mssqllocaldb;Database=EnterpriseDocumentationDB;Trusted_Connection=true;MultipleActiveResultSets=true"

# Set JWT settings
dotnet user-secrets set "JwtSettings:SecretKey" "your-32-plus-character-secret-key-for-development"

# Set OpenAI API key
dotnet user-secrets set "OpenAI:ApiKey" "sk-your-development-key"

# Set logging level
dotnet user-secrets set "Logging:LogLevel:Default" "Information"
```

**For Azure Production (Environment Variables):**

```bash
# Database
AZURE_SQL_CONNECTION_STRING=Server=tcp:<server>.database.windows.net;Database=<db>;...

# JWT
JWT_SECRET_KEY=<production-secret-key-minimum-32-chars>
JWT_ISSUER=Enterprise.Documentation.Api
JWT_AUDIENCE=Enterprise.Documentation.Client
JWT_EXPIRATION_HOURS=8

# Azure Services
AZURE_SERVICE_BUS_CONNECTION_STRING=Endpoint=sb://<namespace>.servicebus.windows.net/;...
AZURE_KEY_VAULT_URL=https://<vault-name>.vault.azure.net/
AZURE_OPENAI_ENDPOINT=https://<resource>.openai.azure.com/
AZURE_OPENAI_API_KEY=<key>
AZURE_OPENAI_DEPLOYMENT=<deployment-name>

# Application Insights
APPLICATIONINSIGHTS_CONNECTION_STRING=InstrumentationKey=<key>;...

# Environment
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=https://+:443;http://+:80

# Logging
SERILOG_MIN_LOG_LEVEL=Information
```

**For GitHub Actions CI/CD (Secrets):**

Add these to GitHub repository secrets:

```
AZURE_CREDENTIALS         - Azure service principal credentials
AZURE_SUBSCRIPTION_ID     - Subscription ID
SONAR_TOKEN               - SonarQube Cloud token
SLACK_WEBHOOK             - Slack notification webhook
ACR_REGISTRY              - Azure Container Registry URL
ACR_USERNAME              - ACR username
ACR_PASSWORD              - ACR password
```

---

## Docker Deployment

### Dockerfile

**Location:** `/src/Api/Dockerfile`

The Dockerfile uses multi-stage build pattern:

```dockerfile
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Api/Api.csproj", "src/Api/"]
COPY ["src/Core/Infrastructure/Core.Infrastructure.csproj", "src/Core/Infrastructure/"]
COPY ["src/Core/Application/Core.Application.csproj", "src/Core/Application/"]
COPY ["src/Core/Domain/Core.Domain.csproj", "src/Core/Domain/"]
COPY ["src/Core/Governance/Core.Governance.csproj", "src/Core/Governance/"]
COPY ["src/Shared/Extensions/Shared.Extensions.csproj", "src/Shared/Extensions/"]
COPY ["src/Shared/Contracts/Shared.Contracts.csproj", "src/Shared/Contracts/"]
COPY ["src/Shared/Configuration/Shared.Configuration.csproj", "src/Shared/Configuration/"]
COPY ["Directory.Packages.props", "."]
RUN dotnet restore "src/Api/Api.csproj"
COPY . .
RUN dotnet build "src/Api/Api.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "src/Api/Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 80 443
ENTRYPOINT ["dotnet", "Enterprise.Documentation.Api.dll"]
```

**Build Image:**

```bash
# Local build
docker build -f src/Api/Dockerfile -t enterprise-doc-api:latest .

# Tag for container registry
docker tag enterprise-doc-api:latest <registry>.azurecr.io/enterprise-doc-api:latest

# Push to Azure Container Registry
docker push <registry>.azurecr.io/enterprise-doc-api:latest
```

### Docker Compose

**Location:** Create `/docker-compose.yml` for local development:

```yaml
version: '3.8'

services:
  api:
    build:
      context: .
      dockerfile: src/Api/Dockerfile
    ports:
      - "5195:80"
      - "7112:443"
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ASPNETCORE_URLS: http://+:80
      ConnectionStrings__DefaultConnection: "Server=sqlserver;Database=EnterpriseDocumentationDB;User Id=sa;Password=YourStrong@Password;Encrypt=false"
      Logging__LogLevel__Default: Debug
    depends_on:
      - sqlserver
    networks:
      - enterprise-net

  sqlserver:
    image: mcr.microsoft.com/mssql/server:2019-latest
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: "YourStrong@Password"
    ports:
      - "1433:1433"
    volumes:
      - mssql_data:/var/opt/mssql
      - ./sql:/sql-scripts
    networks:
      - enterprise-net

  hangfire:
    image: enterprise-doc-api:latest
    ports:
      - "5196:80"
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__DefaultConnection: "Server=sqlserver;Database=EnterpriseDocumentationDB;User Id=sa;Password=YourStrong@Password;Encrypt=false"
      Hangfire__DashboardEnabled: "true"
      Hangfire__AllowAnonymousAccess: "true"
    depends_on:
      - sqlserver
    networks:
      - enterprise-net

networks:
  enterprise-net:
    driver: bridge

volumes:
  mssql_data:
```

**Run Locally:**

```bash
# Start all services
docker-compose up -d

# View logs
docker-compose logs -f api

# Stop all services
docker-compose down

# Rebuild images
docker-compose up -d --build

# Access API
# http://localhost:5195/swagger
# Hangfire Dashboard: http://localhost:5196/hangfire
```

---

## CI/CD Pipeline

### GitHub Actions Workflows

**Location:** `/.github/workflows/`

#### 1. Build Workflow (`build.yml`)

Triggers on every push and pull request.

**Steps:**
1. Setup .NET 8.0 and Java 17
2. Checkout code
3. Cache SonarQube scanner
4. Restore NuGet packages
5. Begin SonarQube analysis
6. Build solution (Release configuration)
7. Run unit tests with code coverage
8. Run integration tests with code coverage
9. End SonarQube analysis
10. Enforce quality gates
11. Upload coverage reports

#### 2. Main CI/CD Pipeline (`ci-cd-pipeline.yml`)

Triggers on push to `main` and `develop` branches.

**Jobs:**

**Build & Test**
- Checkout code
- Setup .NET 8.0 and Node.js 20.x
- Restore dependencies
- Build solution
- Run unit tests with coverage
- Build WebApp frontend (npm)
- Publish API

**Security Scan**
- Trivy filesystem scanner
- Dependency Check
- CodeQL analysis
- Upload SARIF reports

**Code Quality**
- SonarCloud scan
- CodeQL analysis
- Coverage report processing

**Quality Gates**
- PowerShell comprehensive audit
- Minimum score enforcement (80+)
- Warning as failures

**Docker Build** (for main/develop)
- Setup Docker Buildx
- Login to Azure Container Registry
- Extract metadata with versioning
- Build and push API image

**Deploy to Staging** (develop branch only)
- Download API artifacts
- Azure login
- Deploy to staging App Service
- Run smoke tests (curl health endpoint)

**Deploy to Production** (main branch only)
- Create backup of current deployment
- Deploy to production App Service
- Run smoke tests
- Create GitHub release

**Notifications**
- Slack notification on failure

#### 3. Quality Gates Workflow (`quality-gates.yml`)

Enforces code quality standards.

**Quality Checks:**
- Code coverage > 80%
- AI Quality Score > 85
- No critical security issues
- No failing tests

---

## Health Check Endpoints

### API Health Endpoint

**Endpoint:** `GET /health`

**Response (Healthy):**
```json
{
  "status": "Healthy",
  "timestamp": "2025-11-23T12:00:00Z",
  "totalDuration": "00:00:00.1234567",
  "entries": {
    "db": {
      "data": {},
      "duration": "00:00:00.0500000",
      "status": "Healthy"
    },
    "redis": {
      "data": {},
      "duration": "00:00:00.0200000",
      "status": "Healthy"
    },
    "servicebus": {
      "data": {},
      "duration": "00:00:00.0150000",
      "status": "Healthy"
    }
  }
}
```

### Agent Health Properties

Entity: `Agent.cs` (Core.Domain.Entities)

**Health Tracking Fields:**
- `IsAvailable` - Agent operational status
- `ActiveRequestCount` - Current concurrent requests
- `MaxConcurrentRequests` - Concurrency limit (default: 5)
- `TotalRequestsProcessed` - Lifetime request count
- `SuccessfulRequests` - Success metric
- `FailedRequests` - Failure metric
- `AverageProcessingTimeMs` - Performance metric
- `LastRequestAt` - Last activity timestamp
- `LastHealthCheckAt` - Last health check timestamp

### Health Check Types

**Database Health**
- Connection validation
- Query performance
- Transaction status

**Redis Cache Health** (if configured)
- Connection status
- Response time

**Azure Service Bus Health**
- Connection status
- Message queue depth

**Governance Engine Health**
- Security policy status
- PII detection service
- Audit logger status

### Monitoring Health Endpoint

```bash
# Test health endpoint
curl -X GET https://enterprise-doc-platform.azurewebsites.net/health

# Test with verbose output
curl -v https://enterprise-doc-platform.azurewebsites.net/health

# Continuous monitoring (every 30 seconds)
while true; do
  curl -s https://enterprise-doc-platform.azurewebsites.net/health | jq .status
  sleep 30
done
```

---

## Monitoring & Logging

### Structured Logging with Serilog

**Configuration in Program.cs:**

```csharp
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Serilog configuration (when added)
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/app-.txt", rollingInterval: RollingInterval.Day)
    .WriteTo.ApplicationInsights(
        new TelemetryClient(),
        TelemetryConverter.Traces)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentUserName()
    .CreateLogger();
```

### Log Levels

| Level | Usage | Examples |
|-------|-------|----------|
| Debug | Development, detailed flow | Variable values, method entry/exit |
| Information | General flow events | API requests, job completions |
| Warning | Potentially harmful events | Retries, recoverable errors |
| Error | Error events | Exceptions, failed operations |
| Critical | System-wide failures | Database down, service unavailable |

### Logging Pattern (Structured)

```csharp
// Good: Structured logging with context
_logger.LogInformation(
    "Document {DocumentId} processed by {AgentName} in {Duration}ms",
    documentId, agentName, duration);

// Avoid: String interpolation
_logger.LogInformation($"Document {documentId} processed");
```

### Application Insights Integration

**Metrics Tracked:**
- Request rate, response time, success rate
- Exception rate and types
- Custom events (document generated, approval)
- Performance counters (CPU, memory)
- Dependency calls (database, external APIs)

**Query Examples:**

```kusto
// Request failures in last hour
requests
| where timestamp > ago(1h) and success == false
| summarize count() by resultCode

// Slowest API endpoints
requests
| where timestamp > ago(24h)
| summarize avg(duration) by name
| top 10 by avg_duration
```

### Governance Audit Logging

**Automatic Logging:**
- All data access through DataGovernanceProxy
- PII detection events
- Authorization decisions
- Security policy violations
- Query execution and results

**Audit Log Entity:**
```csharp
public class AuditLog : BaseEntity
{
    public string EntityType { get; set; }           // Documents, Users, etc.
    public string EntityId { get; set; }             // ID of entity
    public string Action { get; set; }               // Create, Update, Delete
    public string Description { get; set; }          // Human-readable
    public DateTime OccurredAt { get; set; }         // When it happened
    public string Metadata { get; set; }             // JSON metadata
    public string IpAddress { get; set; }            // Source IP
    public string UserAgent { get; set; }            // Client info
    public string SessionId { get; set; }            // Session tracking
}
```

### Correlation IDs

All requests include correlation ID for distributed tracing:

```csharp
// Middleware adds correlation ID to all requests
if (!context.Request.Headers.ContainsKey("X-Correlation-ID"))
{
    context.Request.Headers["X-Correlation-ID"] = Guid.NewGuid().ToString();
}

// Available in logs for tracking request flow
_logger.LogInformation("Processing request {CorrelationId}", correlationId);
```

---

## Azure Infrastructure Requirements

### Azure SQL Database

**Configuration:**
- **Service Tier:** Standard S1 or Premium P1 (production)
- **Database:** 50+ GB
- **Backups:** Automatic daily backups, 35-day retention
- **Geo-Replication:** Enabled for production
- **Connection Pooling:** Enabled (max 100 connections)

**Firewall Rules:**
- Allow Azure services
- Allow CI/CD agents IP range
- Allow developer IPs (if applicable)

**Maintenance:**
- Run database consistency checks monthly
- Monitor query performance
- Archive old audit logs to Blob Storage

### Azure Service Bus

**Configuration:**
- **Namespace:** Standard tier
- **Queues:**
  - `documents-created` - Document events
  - `approvals-pending` - Approval queue
  - `batch-jobs` - Batch processing
  - `errors` - Dead-letter queue
- **Topics:**
  - `document-events` - Document state changes
  - `agent-events` - Agent status changes

**Access Policies:**
- API: Send, Listen, Manage
- Background workers: Send, Listen
- Admin: Full rights

### Azure Key Vault

**Secrets to Store:**
- `ConnectionString--DefaultConnection`
- `JwtSettings--SecretKey`
- `OpenAI--ApiKey`
- `Azure--ServiceBus--ConnectionString`
- `ApplicationInsights--InstrumentationKey`

**Access Policies:**
- API managed identity: Get, List
- CI/CD service principal: All

### Azure Container Registry

**Configuration:**
- **SKU:** Standard or Premium
- **Image Retention:** 30 days for untagged images
- **Webhooks:** Trigger deployments on image push

**Images:**
- `enterprise-doc-api:latest`
- `enterprise-doc-api:<git-commit-sha>`
- `enterprise-doc-api:<semantic-version>`

### Azure Container Apps

**Deployment:**
- **CPU:** 0.5-2 cores (production: 2)
- **Memory:** 1-4 GB (production: 4)
- **Replicas:** 2-5 (auto-scale on CPU/memory)
- **Health Probe:** HTTP GET /health (30s interval)

**Ingress:**
- HTTPS required
- Port 443 external, 80 internal
- Allow traffic from load balancer only

### Azure Application Insights

**Configuration:**
- **Sampling:** 100% for errors, 10% for normal traffic
- **Retention:** 90 days (production)
- **Log Analytics:** Integrated for queries

**Custom Metrics:**
- Documents processed (per hour)
- Approval workflow timing
- Agent health scores

---

## Deployment Procedures

### Local Development Deployment

```bash
# 1. Clone repository
git clone https://github.com/your-org/autodocprojclone.git
cd autodocprojclone

# 2. Setup user secrets
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\\mssqllocaldb;Database=EnterpriseDocumentationDB;..."

# 3. Restore and build
dotnet restore
dotnet build

# 4. Apply database migrations
dotnet ef database update -p src/Core/Infrastructure/Core.Infrastructure.csproj -s src/Api/Api.csproj

# 5. Run application
cd src/Api
dotnet run

# 6. Access API
# http://localhost:5195/swagger
```

### Docker Local Deployment

```bash
# 1. Start services with Docker Compose
docker-compose up -d

# 2. Verify services are running
docker-compose ps

# 3. Check logs
docker-compose logs -f api

# 4. Initialize database (if needed)
docker exec enterprise-doc-api-1 dotnet ef database update -p src/Core/Infrastructure/Core.Infrastructure.csproj -s src/Api/Api.csproj

# 5. Access API
# http://localhost:5195/swagger

# 6. Stop services
docker-compose down
```

### Azure Staging Deployment

**Triggered by:** Push to `develop` branch

```bash
# 1. Create resource group (first time only)
az group create --name rg-edp-staging --location eastus

# 2. Create App Service Plan
az appservice plan create --name asp-edp-staging --resource-group rg-edp-staging --sku B2 --is-linux

# 3. Create App Service
az webapp create --resource-group rg-edp-staging --plan asp-edp-staging --name edp-staging --deployment-container-image-name <registry>.azurecr.io/enterprise-doc-api:latest

# 4. Configure application settings
az webapp config appsettings set --resource-group rg-edp-staging --name edp-staging --settings \
  ASPNETCORE_ENVIRONMENT=Staging \
  ConnectionStrings__DefaultConnection="<azure-sql-connection-string>" \
  JwtSettings__SecretKey="<secret-key>" \
  WEBSITES_ENABLE_APP_SERVICE_STORAGE=false

# 5. Trigger deployment (CI/CD automatically does this)
# Check GitHub Actions workflow status
```

### Azure Production Deployment

**Triggered by:** Push to `main` branch

```bash
# 1. Pre-deployment checks
az webapp deployment slot create --resource-group rg-edp-prod --name edp-prod --slot staging

# 2. Deploy to slot (Blue-Green)
az webapp deployment slot swap --resource-group rg-edp-prod --name edp-prod --slot staging

# 3. Post-deployment verification
curl -f https://enterprise-doc-platform.azurewebsites.net/health

# 4. Monitor Application Insights
az monitor app-insights metrics show --resource-group rg-edp-prod --app edp-insights

# 5. If issues: Swap back (Rollback)
az webapp deployment slot swap --resource-group rg-edp-prod --name edp-prod --slot staging --action swap
```

### Manual Deployment (PowerShell)

```powershell
# Deploy to staging
.\deploy-staging.ps1 -ImageTag "latest" -Environment "Staging"

# Deploy to production (with approval)
if (Read-Host "Deploy to production? (yes/no)") {
    .\deploy-production.ps1 -ImageTag "latest" -Environment "Production"
}
```

---

## Troubleshooting

### Common Issues

#### 1. Database Connection Errors

**Error:** "Cannot open database 'EnterpriseDocumentationDB'"

**Solution:**
```bash
# Verify connection string
$connectionString = "Server=(localdb)\mssqllocaldb;Database=EnterpriseDocumentationDB;Trusted_Connection=true"

# List available LocalDB instances
sqllocaldb info

# Create database if missing
sqlcmd -S (localdb)\mssqllocaldb -Q "CREATE DATABASE EnterpriseDocumentationDB"

# Apply migrations
dotnet ef database update
```

#### 2. EF Core Migration Issues

**Error:** "No database provider has been configured"

**Solution:**
```bash
# Install EF Core CLI
dotnet tool install --global dotnet-ef

# Update if already installed
dotnet tool update --global dotnet-ef

# Add migration
dotnet ef migrations add <MigrationName> -p src/Core/Infrastructure/Core.Infrastructure.csproj -s src/Api/Api.csproj

# Update database
dotnet ef database update -p src/Core/Infrastructure/Core.Infrastructure.csproj -s src/Api/Api.csproj
```

#### 3. Docker Build Failures

**Error:** "System.IO.FileNotFoundException: File not found"

**Solution:**
```bash
# Verify docker-compose.yml paths
docker-compose config

# Clean and rebuild
docker-compose down
docker system prune -a
docker-compose up --build

# Check Dockerfile syntax
docker build --progress=plain .
```

#### 4. Health Check Failures

**Error:** "Health check endpoint returns Unhealthy"

**Solution:**
```bash
# Check individual service health
curl http://localhost:5195/health

# Verify database connectivity
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=..."

# Check Azure Service Bus connection
# Verify connection string in Key Vault

# View logs
docker-compose logs api | grep -i health
```

#### 5. JWT Token Errors

**Error:** "Invalid token" or "Token expired"

**Solution:**
```bash
# Verify JWT settings are configured
dotnet user-secrets list | grep Jwt

# Generate new secret key (minimum 32 characters)
$key = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes((New-Guid).ToString() + (New-Guid).ToString()))
dotnet user-secrets set "JwtSettings:SecretKey" $key

# Verify Issuer and Audience match client configuration
```

#### 6. Hangfire Dashboard Not Accessible

**Error:** "401 Unauthorized" when accessing /hangfire

**Solution:**
```bash
# Check configuration
dotnet user-secrets get "Hangfire:AllowAnonymousAccess"

# For development, enable anonymous access
dotnet user-secrets set "Hangfire:AllowAnonymousAccess" "true"

# For production, implement proper authentication
# See HangfireDashboardAuthorizationFilter in Configuration/HangfireConfiguration.cs
```

### Performance Tuning

**Connection Pooling:**
```
Add ";Connection Lifetime=300;" to connection string
```

**Query Performance:**
```sql
-- Monitor slow queries
SELECT TOP 10 
    execution_count,
    total_elapsed_time / execution_count AS avg_elapsed_time,
    plan_handle,
    query_hash
FROM sys.dm_exec_query_stats
ORDER BY avg_elapsed_time DESC
```

**Caching:**
```csharp
// Implement Redis caching
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = configuration.GetConnectionString("Redis");
});
```

### Log Aggregation

**Application Insights Queries:**

```kusto
// All errors in last 24 hours
exceptions
| where timestamp > ago(24h)
| summarize count() by outerMessage
| order by count_ desc

// API performance
requests
| where timestamp > ago(1h)
| summarize 
    count=count(),
    avg_duration=avg(duration),
    p95_duration=percentile(duration, 95)
    by name
| order by avg_duration desc
```

---

## Deployment Checklist

- [ ] Prerequisites installed (.NET 8, SQL Server, Docker)
- [ ] Repository cloned and branched
- [ ] appsettings.json configured with connection strings
- [ ] User secrets configured for development
- [ ] Database migrations applied successfully
- [ ] Health checks passing locally
- [ ] Unit tests passing (>80% coverage)
- [ ] Integration tests passing
- [ ] Docker image builds successfully
- [ ] Docker Compose starts all services
- [ ] API accessible at localhost:5195/swagger
- [ ] Hangfire Dashboard accessible (if configured)
- [ ] Azure resources provisioned
- [ ] CI/CD pipeline configured
- [ ] Staging deployment successful
- [ ] Smoke tests passing on staging
- [ ] Production deployment successful
- [ ] Post-deployment monitoring configured
- [ ] Rollback procedure tested

---

## Related Documentation

- [ARCHITECTURE.md](./docs/ARCHITECTURE.md) - System architecture decisions
- [CODING_STANDARDS.md](./docs/CODING_STANDARDS.md) - Code quality standards
- [BATCH-PROCESSING-SETUP.md](./docs/BATCH-PROCESSING-SETUP.md) - Batch job configuration
- [BATCH-SYSTEM-SUMMARY.md](./docs/BATCH-SYSTEM-SUMMARY.md) - Batch processing overview
- [SONARCLOUD-SETUP.md](./SONARCLOUD-SETUP.md) - Code quality gates

---

**Maintained By:** DevOps/Platform Team  
**Last Review:** November 23, 2025  
**Next Review:** December 23, 2025


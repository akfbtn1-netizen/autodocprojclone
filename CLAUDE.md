# Enterprise Documentation Platform V2

## Purpose
AI-powered documentation automation: 4 hours → 60 seconds

## Phase & Direction
- **Current**: Phase 1 complete - core doc pipeline working
- **Next 6mo**: Specialized agents (SchemaMapper, LineageTracer, GraphRAG)
- **12mo**: Full 20+ agent mesh ecosystem

## Architecture (Multi-Agent Ready)
- Clean Architecture + CQRS/MediatR (agent command dispatch)
- Azure Service Bus (agent-to-agent events, saga patterns)
- Governance layer with AgentId + ClearanceLevel
- MCP server at `.mcp/` (project intelligence + AI tool orchestration)
- Hangfire (background agent tasks)
- SignalR (real-time agent status)

## MCP Project Intelligence (MANDATORY)

**35 Tools + 4 Prompts = 39 Capabilities** for complete project awareness.

### Data Governance (CRITICAL)
Database tools query **METADATA ONLY** - never actual data:
- `db_describe_table` queries INFORMATION_SCHEMA, sys.* views
- NO table data ever leaves the database boundary
- Compliant with enterprise data governance policies

### Project Intelligence Tools (1-15)
| Trigger | Tool | Purpose |
|---------|------|---------|
| Session start | `project_init` | Load working memory, config, status |
| Before any task | `get_relevant_context` | Load context by task type |
| Before git commit | `validate_before_commit` | Run tests + lint checks |
| Database questions | `get_masterindex_schema` | Full 115-column schema |
| API questions | `get_api_endpoints` | List all endpoints |
| Agent questions | `get_agent_architecture` | 9 agents + interactions |
| Before PR | `run_integration_tests` | Full test suite |
| DB migrations | `check_ef_migrations` | Migration status |
| React work | `get_react_components` | Component tree |
| Cost questions | `check_openai_costs` | Azure OpenAI metrics |
| Template work | `get_document_templates` | 6 template specs |
| Vector DB | `check_qdrant_status` | Qdrant health |
| Skills lookup | `get_available_skills` | Skill inventory |
| Sprint work | `get_sprint_status` | Jira sprint data |
| Code search | `search_codebase` | Regex codebase search |

### Operational Tools (16-26)
| Category | Tool | Purpose |
|----------|------|---------|
| Codebase | `codebase_read_file` | Read file contents |
| Codebase | `codebase_search_code` | Regex pattern search |
| Codebase | `codebase_glob_files` | Find files by pattern |
| Codebase | `codebase_list_directory` | List directory contents |
| Git | `git_status` | Working directory status |
| Git | `git_diff` | Show changes |
| Git | `git_log` | Commit history |
| Git | `git_branch_info` | Branch information |
| Build | `build_dotnet` | .NET build |
| Build | `build_npm` | Frontend build |
| Quality | `quality_run_gate` | Quality gate checks |

### New Tools v2.2.0 (27-35)
| Trigger | Tool | Purpose |
|---------|------|---------|
| Before data access code | `db_check_connection` | Verify SQL Server connectivity |
| Schema questions | `db_describe_table` | Table metadata (NO DATA) |
| After decisions | `remember_decision` | Persist architectural decisions |
| Before new decisions | `recall_decisions` | Review past decisions |
| Container work | `docker_status` | Docker container health |
| New agent | `scaffold_agent` | Generate agent boilerplate |
| New feature | `scaffold_cqrs_feature` | Generate CQRS pattern files |
| Before changes | `analyze_change_impact` | Dependency analysis |
| Debugging | `view_recent_logs` | Aggregate application logs |

### Prompts (4)
- `frontend-specialist` - React 19 + TypeScript + Tailwind
- `api-integration-specialist` - REST/SignalR integration
- `e2e-testing-specialist` - End-to-end testing
- `agent-architect` - Multi-agent system design

### Before Writing Code Checklist
1. ✅ Called `project_init` this session?
2. ✅ Called `get_relevant_context` for this task type?
3. ✅ Know the current sprint priorities? (`get_sprint_status`)
4. ✅ Understand affected components? (`search_codebase`)
5. ✅ Checked past decisions? (`recall_decisions`)
6. ✅ Verified DB connectivity? (`db_check_connection`)

### Context Types for get_relevant_context
- `api` - Backend API development
- `frontend` - React component work
- `database` - Schema/migration work
- `agent` - Agent development
- `workflow` - Approval workflow
- `testing` - Test development
- `devops` - Build/deploy work

## Backend (.NET 8 / C# 12)
- ASP.NET Core Web API
- Dapper (queries) + EF Core (migrations only)
- SQL Server (IRFS1) - gwpc/DaQa schemas
- FluentValidation, AutoMapper, Polly
- Serilog + OpenTelemetry

## Frontend (React 19)
- TypeScript + Vite 6
- Zustand (state) + React Query (server state)
- Tailwind CSS (warm stone/teal theme)
- Framer Motion, React Flow
- SignalR client for real-time

## Document Processing
- C# orchestration + Node.js template execution
- 6 templates: Tier 1 (complex), Tier 2 (standard), Tier 3 (simple)

## Project Structure
```
DocumentationAgents.API/           # Program.cs, controllers, SignalR hubs
DocumentationAgents.Application/   # CQRS handlers, validators, DTOs
DocumentationAgents.Core/          # Entities, interfaces, domain logic
DocumentationAgents.Infrastructure/ # Repositories, external services
DocumentationAgents.Shared/        # Events, enums, cross-cutting
DocumentationAgents.Tests/         # xUnit tests (35+)
DocumentationDashboard.Web/        # React 19 + Vite frontend
Templates/                         # 6 Node.js .docx templates
.mcp/                              # MCP server v2.2.0 (35 tools, 4 prompts)
```

## Entry Points
- Backend: `DocumentationAgents.API/Program.cs` (DI, middleware, SignalR)
- Frontend: `DocumentationDashboard.Web/src/main.tsx`

## Naming Conventions
- Interfaces: `IServiceName`
- Agents: `*Agent` suffix
- Handlers: `*CommandHandler`, `*QueryHandler`
- DTOs: `*Request`, `*Response`, `*Dto`
- Private fields: `_camelCase`
- Async methods: `Async` suffix

## Quality Standards
- Cyclomatic complexity: ≤6/method
- Method length: ≤20 lines
- Class length: ≤200 lines
- Parameters: ≤4/method
- Test coverage: ≥80%

## Key API Routes
- Auth: `POST /api/auth/login` (JWT)
- Documents: `/api/documents` (CRUD + approve)
- Batch: `/api/batchprocessing/{schema|folder|excel}`
- Governance: `/api/governance/{validate|authorize}`

## Error Handling
- Business logic: Try-catch + Serilog
- API: ASP.NET Core problem details
- Security: `DataGovernanceException`
- Validation: FluentValidation in MediatR pipeline

## Git: Conventional Commits
`feat:` | `fix:` | `refactor:` | `test:` | `docs:` | `chore:`
Branches: `feature/description` or `fix/description`

## Status
✅ Doc pipeline, approval workflow, JWT auth, 35+ tests, React dashboard, Hangfire, PII detection
🚧 SharePoint upload (code ready)
📋 Jira webhooks, batch overnight, GraphRAG, 20+ specialized agents

## Never Do
- ❌ EF Core for queries (Dapper only; EF for migrations)
- ❌ Bypass governance layer (always use DataGovernanceProxy)
- ❌ Log PII (Presidio scans everything)
- ❌ Hardcode secrets (use appsettings / Key Vault)
- ❌ Skip async/await on I/O
- ❌ Create implementations without interfaces first

## Integrations
✅ Azure OpenAI, SQL Server (IRFS1), Hangfire, SignalR, Presidio
🚧 SharePoint (Graph API ready)
📋 Jira, Azure Service Bus, Blob WORM

## Config Keys
`ConnectionStrings:DefaultConnection` | `AzureOpenAI:Endpoint/ApiKey` | `JWT:Secret` | `Hangfire:DashboardPath`

## Tests
`DocumentationAgents.Tests/` → `Unit/` + `Integration/`
Run: `dotnet test --filter Category=Unit` (fast) | `dotnet test` (all)

## Commands
```bash
dotnet build                    # Build backend
dotnet test                     # Run all tests
cd DocumentationDashboard.Web && npm run dev   # Frontend dev
```

## Planned Agent Categories (20+)
- **Core**: SchemaDetector, DocGenerator, MetadataManager, WorkflowOrchestrator
- **Intelligence**: SchemaMapper, LineageTracer, ImpactAnalyzer, ColumnPredictor
- **Governance**: PIICompliance, DataQuality, SecurityAuditor, AccessManager
- **Search**: SemanticSearch, OntologyTagger, RelationshipMiner

## Skills
@reference .claude/skills-reference.md

## Detailed References
- `.claude/context/agent-roadmap.md` - Full 20+ agent specifications
- `.claude/context/patterns.md` - Code examples (MediatR, React patterns)

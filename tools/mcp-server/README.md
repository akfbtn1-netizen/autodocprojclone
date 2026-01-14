# Enterprise Docs MCP Server

MCP (Model Context Protocol) server for the Enterprise Documentation Platform V2. Provides comprehensive dev environment tools and specialist prompts.

## Features

### Tools (22 total across 9 categories)

#### Codebase Access
| Tool | Description |
|------|-------------|
| `codebase_read_file` | Read file contents from the project |
| `codebase_search_code` | Search code using regex patterns |
| `codebase_glob_files` | Find files matching glob patterns |
| `codebase_list_directory` | List directory contents |

#### Git Operations
| Tool | Description |
|------|-------------|
| `git_status` | Get current git status |
| `git_diff` | Show changes (staged/unstaged) |
| `git_log` | Show commit history |
| `git_branch_info` | Get branch information |

#### Testing
| Tool | Description |
|------|-------------|
| `test_dotnet` | Run .NET tests with filtering |
| `test_npm` | Run frontend tests |
| `test_coverage` | Run tests with code coverage |

#### Build
| Tool | Description |
|------|-------------|
| `build_dotnet` | Build .NET solution |
| `build_npm` | Build frontend |

#### Quality Gates
| Tool | Description |
|------|-------------|
| `quality_run_gate` | Run quality gate checks |

#### API Mapping
| Tool | Description |
|------|-------------|
| `api_list_endpoints` | List all API endpoints with routes and methods |

#### Code Indexing
| Tool | Description |
|------|-------------|
| `code_index_handlers` | Index MediatR command/query handlers |
| `code_index_entities` | Index domain entities with properties |
| `code_find_usages` | Find where symbols are used |

#### Database Schema
| Tool | Description |
|------|-------------|
| `db_get_schema` | Get EF Core DbContext schema |
| `db_check_migrations` | Check migration status |

#### Context Memory
| Tool | Description |
|------|-------------|
| `memory_update` | Update working memory (persists across sessions) |
| `memory_read` | Read working memory |
| `memory_clear` | Clear memory sections |

### Specialist Prompts (4 total)

| Prompt | Description |
|--------|-------------|
| `frontend-specialist` | Senior frontend expertise (React, Next.js, TypeScript) |
| `api-integration-specialist` | API integration expertise (REST, GraphQL, webhooks) |
| `e2e-testing-specialist` | E2E testing expertise (Playwright, Cypress) |
| `agent-architect` | AI agent architecture (Claude SDK, LangGraph, MCP) |

## Installation

```bash
cd tools/mcp-server
npm install
npm run build
```

## Usage

### stdio Transport (Claude Desktop)

```bash
npm start
# or
node dist/index.js
```

### SSE Transport (Web Integration)

```bash
set MCP_TRANSPORT=sse
npm start
# Server runs on http://localhost:3100
```

## Configuration

Environment variables:

| Variable | Default | Description |
|----------|---------|-------------|
| `MCP_PROJECT_ROOT` | `C:\Projects\EnterpriseDocumentationPlatform.V2` | Project root path |
| `MCP_TRANSPORT` | `stdio` | Transport type (stdio/sse) |
| `MCP_SSE_PORT` | `3100` | SSE server port |
| `MCP_COMMAND_TIMEOUT` | `300000` | Command timeout (ms) |
| `MCP_CHARACTER_LIMIT` | `50000` | Response character limit |

## Claude Desktop Configuration

Add to your `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "enterprise-docs": {
      "command": "node",
      "args": ["C:/Projects/EnterpriseDocumentationPlatform.V2/tools/mcp-server/dist/index.js"],
      "env": {
        "MCP_PROJECT_ROOT": "C:/Projects/EnterpriseDocumentationPlatform.V2"
      }
    }
  }
}
```

## Working Memory

The MCP server maintains persistent working memory at `.claude/working-memory.md`. Use it to:
- Track current task context
- Record decisions made
- List next steps
- Note open questions

This memory persists across sessions, helping maintain continuity in long-running projects.

## Development

```bash
# Watch mode
npm run dev

# Build
npm run build

# Clean
npm run clean
```

## Security

- All file paths are sandboxed within the project root
- No directory traversal allowed
- Command execution limited to predefined commands
- 5-minute timeout on long-running commands

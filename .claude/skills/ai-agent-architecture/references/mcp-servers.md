# MCP Server Implementation Guide

Build Model Context Protocol servers for agent tool integration.

---

## MCP Architecture

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│  AI Agent    │────▶│  MCP Client  │────▶│  MCP Server  │
│  (Claude)    │◀────│  (in host)   │◀────│  (tools)     │
└──────────────┘     └──────────────┘     └──────────────┘
                            │
                            ▼
                     ┌──────────────┐
                     │  External    │
                     │  Services    │
                     └──────────────┘
```

---

## Basic Server (Python)

```python
from mcp.server import Server
from mcp.types import Tool, TextContent
import asyncio

# Create server instance
server = Server("my-tools")

# Define tools
@server.tool()
async def search_documents(query: str, limit: int = 10) -> str:
    """
    Search internal document repository.
    
    Args:
        query: Search query string
        limit: Maximum results to return
    
    Returns:
        JSON string of matching documents
    """
    results = await document_db.search(query, limit=limit)
    return json.dumps(results)

@server.tool()
async def get_user_info(user_id: str) -> str:
    """
    Retrieve user information from CRM.
    
    Args:
        user_id: Unique user identifier
    
    Returns:
        JSON string with user details
    """
    user = await crm.get_user(user_id)
    return json.dumps(user.to_dict())

@server.tool()
async def send_notification(
    channel: str,
    message: str,
    priority: str = "normal"
) -> str:
    """
    Send notification via Slack/Teams.
    
    Args:
        channel: Channel name or ID
        message: Notification content
        priority: normal, high, or urgent
    
    Returns:
        Confirmation message
    """
    await notifier.send(channel, message, priority)
    return f"Notification sent to {channel}"

# Run server
async def main():
    async with server.run_stdio():
        await asyncio.Future()  # Run forever

if __name__ == "__main__":
    asyncio.run(main())
```

---

## Server with Resources

```python
from mcp.server import Server
from mcp.types import Resource, TextContent

server = Server("data-server")

# Define resources (read-only data)
@server.resource("config://app-settings")
async def get_app_settings() -> str:
    """Application configuration settings."""
    return json.dumps(await config.get_all())

@server.resource("schema://database")
async def get_database_schema() -> str:
    """Database schema documentation."""
    return await db.get_schema_docs()

@server.resource("template://email/{template_name}")
async def get_email_template(template_name: str) -> str:
    """Email templates by name."""
    return await templates.get(template_name)

# List available resources
@server.list_resources()
async def list_resources():
    return [
        Resource(uri="config://app-settings", name="App Settings"),
        Resource(uri="schema://database", name="DB Schema"),
        Resource(uri="template://email/welcome", name="Welcome Email"),
        Resource(uri="template://email/reset", name="Password Reset"),
    ]
```

---

## Server with Prompts

```python
from mcp.server import Server
from mcp.types import Prompt, PromptArgument

server = Server("prompt-server")

@server.prompt("analyze-code")
async def analyze_code_prompt(
    language: str,
    code: str
) -> str:
    """Generate code analysis prompt."""
    return f"""Analyze this {language} code for:
1. Potential bugs
2. Security vulnerabilities
3. Performance issues
4. Code style improvements

Code:
```{language}
{code}
```

Provide specific, actionable feedback."""

@server.prompt("summarize-meeting")
async def summarize_meeting_prompt(
    transcript: str,
    attendees: str
) -> str:
    """Generate meeting summary prompt."""
    return f"""Summarize this meeting transcript.

Attendees: {attendees}

Transcript:
{transcript}

Include:
- Key decisions made
- Action items with owners
- Follow-up items
- Next steps"""

@server.list_prompts()
async def list_prompts():
    return [
        Prompt(
            name="analyze-code",
            description="Analyze code for issues",
            arguments=[
                PromptArgument(name="language", required=True),
                PromptArgument(name="code", required=True)
            ]
        ),
        Prompt(
            name="summarize-meeting",
            description="Summarize meeting transcript",
            arguments=[
                PromptArgument(name="transcript", required=True),
                PromptArgument(name="attendees", required=False)
            ]
        )
    ]
```

---

## TypeScript Server

```typescript
import { Server } from "@modelcontextprotocol/sdk/server";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio";

const server = new Server({
  name: "my-server",
  version: "1.0.0"
}, {
  capabilities: {
    tools: {},
    resources: {},
    prompts: {}
  }
});

// Define tool
server.setRequestHandler("tools/call", async (request) => {
  const { name, arguments: args } = request.params;
  
  switch (name) {
    case "search":
      const results = await searchDatabase(args.query);
      return { content: [{ type: "text", text: JSON.stringify(results) }] };
    
    case "create_ticket":
      const ticket = await createJiraTicket(args);
      return { content: [{ type: "text", text: `Created: ${ticket.key}` }] };
    
    default:
      throw new Error(`Unknown tool: ${name}`);
  }
});

// List tools
server.setRequestHandler("tools/list", async () => {
  return {
    tools: [
      {
        name: "search",
        description: "Search database",
        inputSchema: {
          type: "object",
          properties: {
            query: { type: "string", description: "Search query" }
          },
          required: ["query"]
        }
      },
      {
        name: "create_ticket",
        description: "Create JIRA ticket",
        inputSchema: {
          type: "object",
          properties: {
            title: { type: "string" },
            description: { type: "string" },
            priority: { type: "string", enum: ["low", "medium", "high"] }
          },
          required: ["title"]
        }
      }
    ]
  };
});

// Run server
async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
}

main();
```

---

## Security Best Practices

### Input Validation

```python
from pydantic import BaseModel, validator

class SearchInput(BaseModel):
    query: str
    limit: int = 10
    
    @validator('query')
    def validate_query(cls, v):
        if len(v) > 1000:
            raise ValueError("Query too long")
        # Prevent injection
        if any(c in v for c in ['<script>', 'DROP TABLE']):
            raise ValueError("Invalid characters")
        return v
    
    @validator('limit')
    def validate_limit(cls, v):
        if v < 1 or v > 100:
            raise ValueError("Limit must be 1-100")
        return v

@server.tool()
async def search_documents(query: str, limit: int = 10) -> str:
    # Validate input
    validated = SearchInput(query=query, limit=limit)
    # Use validated values
    return await do_search(validated.query, validated.limit)
```

### Rate Limiting

```python
from asyncio import Semaphore
from time import time

class RateLimiter:
    def __init__(self, max_requests: int, window_seconds: int):
        self.max_requests = max_requests
        self.window = window_seconds
        self.requests = []
        self.semaphore = Semaphore(max_requests)
    
    async def acquire(self):
        now = time()
        self.requests = [t for t in self.requests if now - t < self.window]
        
        if len(self.requests) >= self.max_requests:
            raise Exception("Rate limit exceeded")
        
        async with self.semaphore:
            self.requests.append(now)
            return True

limiter = RateLimiter(max_requests=100, window_seconds=60)

@server.tool()
async def rate_limited_tool(query: str) -> str:
    await limiter.acquire()
    return await process(query)
```

### Authentication

```python
import os
from functools import wraps

def require_auth(func):
    @wraps(func)
    async def wrapper(*args, **kwargs):
        # Get token from environment or context
        token = os.environ.get("MCP_AUTH_TOKEN")
        if not token or not await validate_token(token):
            raise PermissionError("Invalid authentication")
        return await func(*args, **kwargs)
    return wrapper

@server.tool()
@require_auth
async def sensitive_operation(data: str) -> str:
    return await process_sensitive(data)
```

---

## Testing MCP Servers

```python
import pytest
from mcp.client import Client

@pytest.fixture
async def mcp_client():
    """Create test client connected to server."""
    client = Client()
    await client.connect_stdio(["python", "server.py"])
    yield client
    await client.close()

@pytest.mark.asyncio
async def test_search_tool(mcp_client):
    # List available tools
    tools = await mcp_client.list_tools()
    assert any(t.name == "search_documents" for t in tools)
    
    # Call tool
    result = await mcp_client.call_tool("search_documents", {
        "query": "test query",
        "limit": 5
    })
    
    assert result is not None
    data = json.loads(result.content[0].text)
    assert len(data) <= 5

@pytest.mark.asyncio
async def test_invalid_input(mcp_client):
    with pytest.raises(Exception):
        await mcp_client.call_tool("search_documents", {
            "query": "",  # Invalid empty query
            "limit": -1   # Invalid limit
        })
```

---

## Deployment

### Docker

```dockerfile
FROM python:3.11-slim

WORKDIR /app
COPY requirements.txt .
RUN pip install -r requirements.txt

COPY server.py .

# MCP runs via stdio
CMD ["python", "server.py"]
```

### Kubernetes

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: mcp-server
spec:
  replicas: 3
  selector:
    matchLabels:
      app: mcp-server
  template:
    spec:
      containers:
      - name: mcp-server
        image: my-mcp-server:latest
        env:
        - name: DATABASE_URL
          valueFrom:
            secretKeyRef:
              name: mcp-secrets
              key: database-url
```

### Claude Desktop Configuration

```json
{
  "mcpServers": {
    "my-tools": {
      "command": "python",
      "args": ["/path/to/server.py"],
      "env": {
        "DATABASE_URL": "postgresql://..."
      }
    }
  }
}
```

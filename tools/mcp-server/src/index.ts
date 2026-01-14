#!/usr/bin/env node

import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
  ListPromptsRequestSchema,
  GetPromptRequestSchema,
} from '@modelcontextprotocol/sdk/types.js';

import { config } from './config.js';
import { allTools, handleToolCall } from './tools/index.js';
import { allPrompts, handlePromptRequest } from './prompts/index.js';

// Create the MCP server
const server = new Server(
  {
    name: 'enterprise-docs-mcp',
    version: '1.0.0',
  },
  {
    capabilities: {
      tools: {},
      prompts: {},
    },
  }
);

// Handle list tools request
server.setRequestHandler(ListToolsRequestSchema, async () => {
  return {
    tools: allTools.map((tool) => ({
      name: tool.name,
      description: tool.description,
      inputSchema: tool.inputSchema,
    })),
  };
});

// Handle tool calls
server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name, arguments: args } = request.params;
  const result = await handleToolCall(name, (args ?? {}) as Record<string, unknown>);
  return result;
});

// Handle list prompts request
server.setRequestHandler(ListPromptsRequestSchema, async () => {
  return {
    prompts: allPrompts.map((prompt) => ({
      name: prompt.name,
      description: prompt.description,
      arguments: prompt.arguments,
    })),
  };
});

// Handle get prompt request
server.setRequestHandler(GetPromptRequestSchema, async (request) => {
  const { name, arguments: args } = request.params;
  const result = await handlePromptRequest(name, (args ?? {}) as Record<string, string>);
  return result;
});

// Main entry point
async function main() {
  const transport = config.transport;

  if (transport === 'stdio') {
    // Use stdio transport (default for Claude Desktop)
    const stdioTransport = new StdioServerTransport();
    await server.connect(stdioTransport);

    // Log to stderr so it doesn't interfere with MCP protocol
    console.error(`Enterprise Docs MCP Server started (stdio)`);
    console.error(`Project root: ${config.projectRoot}`);
    console.error(`Tools: ${allTools.length}, Prompts: ${allPrompts.length}`);
  } else if (transport === 'sse') {
    // SSE transport using Express
    const { default: express } = await import('express');
    const { SSEServerTransport } = await import('@modelcontextprotocol/sdk/server/sse.js');

    const app = express();
    app.use(express.json());

    // Health check endpoint
    app.get('/health', (_req, res) => {
      res.json({
        status: 'ok',
        version: '1.0.0',
        tools: allTools.length,
        prompts: allPrompts.length,
      });
    });

    // SSE endpoint - store transport reference
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    let sseTransport: any = null;

    app.get('/sse', async (_req, res) => {
      console.error('SSE connection established');
      sseTransport = new SSEServerTransport('/messages', res);
      await server.connect(sseTransport);
    });

    app.post('/messages', async (req, res) => {
      if (sseTransport && typeof sseTransport.handlePostMessage === 'function') {
        await sseTransport.handlePostMessage(req, res);
      } else {
        res.status(503).json({ error: 'No SSE connection established' });
      }
    });

    const port = config.ssePort;
    app.listen(port, () => {
      console.error(`Enterprise Docs MCP Server started (SSE)`);
      console.error(`Listening on http://localhost:${port}`);
      console.error(`Project root: ${config.projectRoot}`);
      console.error(`Tools: ${allTools.length}, Prompts: ${allPrompts.length}`);
    });
  } else {
    console.error(`Unknown transport: ${transport}`);
    process.exit(1);
  }
}

main().catch((error) => {
  console.error('Fatal error:', error);
  process.exit(1);
});

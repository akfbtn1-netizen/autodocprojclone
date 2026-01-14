import { codebaseTools, handleCodebaseTool } from './codebase.js';
import { gitTools, handleGitTool } from './git.js';
import { testingTools, handleTestingTool } from './testing.js';
import { buildTools, handleBuildTool } from './build.js';
import { qualityTools, handleQualityTool } from './quality.js';
import { apiTools, handleApiTool } from './api.js';
import { codeIndexTools, handleCodeIndexTool } from './code-index.js';
import { databaseTools, handleDatabaseTool } from './database.js';
import { memoryTools, handleMemoryTool } from './memory.js';

// All tool definitions
export const allTools = [
  ...codebaseTools,
  ...gitTools,
  ...testingTools,
  ...buildTools,
  ...qualityTools,
  ...apiTools,
  ...codeIndexTools,
  ...databaseTools,
  ...memoryTools,
];

// Tool handler router
export async function handleToolCall(
  name: string,
  args: Record<string, unknown>
): Promise<{ content: { type: 'text'; text: string }[]; isError?: boolean }> {
  // Codebase tools
  if (name.startsWith('codebase_')) {
    return handleCodebaseTool(name, args);
  }

  // Git tools
  if (name.startsWith('git_')) {
    return handleGitTool(name, args);
  }

  // Testing tools
  if (name.startsWith('test_')) {
    return handleTestingTool(name, args);
  }

  // Build tools
  if (name.startsWith('build_')) {
    return handleBuildTool(name, args);
  }

  // Quality tools
  if (name.startsWith('quality_')) {
    return handleQualityTool(name, args);
  }

  // API tools
  if (name.startsWith('api_')) {
    return handleApiTool(name, args);
  }

  // Code indexing tools
  if (name.startsWith('code_')) {
    return handleCodeIndexTool(name, args);
  }

  // Database tools
  if (name.startsWith('db_')) {
    return handleDatabaseTool(name, args);
  }

  // Memory tools
  if (name.startsWith('memory_')) {
    return handleMemoryTool(name, args);
  }

  return {
    content: [{ type: 'text', text: `Unknown tool: ${name}` }],
    isError: true,
  };
}

export {
  codebaseTools,
  gitTools,
  testingTools,
  buildTools,
  qualityTools,
  apiTools,
  codeIndexTools,
  databaseTools,
  memoryTools,
  handleCodebaseTool,
  handleGitTool,
  handleTestingTool,
  handleBuildTool,
  handleQualityTool,
  handleApiTool,
  handleCodeIndexTool,
  handleDatabaseTool,
  handleMemoryTool,
};

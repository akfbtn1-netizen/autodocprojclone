import { resolve, normalize } from 'path';

export interface MCPServerConfig {
  projectRoot: string;
  skillsPath: string;
  transport: 'stdio' | 'sse';
  ssePort: number;
  commandTimeout: number;
  characterLimit: number;
  maxFileSize: number;
}

function getEnvOrDefault(key: string, defaultValue: string): string {
  return process.env[key] ?? defaultValue;
}

function getEnvNumberOrDefault(key: string, defaultValue: number): number {
  const value = process.env[key];
  if (value) {
    const parsed = parseInt(value, 10);
    if (!isNaN(parsed)) return parsed;
  }
  return defaultValue;
}

export function loadConfig(): MCPServerConfig {
  const projectRoot = resolve(
    getEnvOrDefault('MCP_PROJECT_ROOT', 'C:\\Projects\\EnterpriseDocumentationPlatform.V2')
  );

  return {
    projectRoot: normalize(projectRoot),
    skillsPath: normalize(resolve(projectRoot, '.claude', 'skills')),
    transport: (getEnvOrDefault('MCP_TRANSPORT', 'stdio') as 'stdio' | 'sse'),
    ssePort: getEnvNumberOrDefault('MCP_SSE_PORT', 3100),
    commandTimeout: getEnvNumberOrDefault('MCP_COMMAND_TIMEOUT', 300000), // 5 minutes
    characterLimit: getEnvNumberOrDefault('MCP_CHARACTER_LIMIT', 50000),
    maxFileSize: getEnvNumberOrDefault('MCP_MAX_FILE_SIZE', 10 * 1024 * 1024), // 10MB
  };
}

export const config = loadConfig();

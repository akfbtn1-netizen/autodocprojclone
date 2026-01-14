import { z } from 'zod';
import { resolve } from 'path';
import { processService } from '../services/process.js';
import { config } from '../config.js';

// Schemas
export const DotnetBuildSchema = z.object({
  project: z.string().optional().describe('Path to specific project or solution'),
  configuration: z.enum(['Debug', 'Release']).optional().default('Debug'),
  verbosity: z.enum(['quiet', 'minimal', 'normal', 'detailed']).optional().default('minimal'),
});

export const NpmBuildSchema = z.object({
  path: z.string().optional().default('frontend').describe('Path to frontend project'),
});

// Tool definitions
export const buildTools = [
  {
    name: 'build_dotnet',
    description: 'Build the .NET solution or a specific project using dotnet build.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        project: { type: 'string', description: 'Path to specific project or solution' },
        configuration: {
          type: 'string',
          enum: ['Debug', 'Release'],
          description: 'Build configuration',
          default: 'Debug',
        },
        verbosity: {
          type: 'string',
          enum: ['quiet', 'minimal', 'normal', 'detailed'],
          description: 'Output verbosity level',
          default: 'minimal',
        },
      },
      required: [],
    },
  },
  {
    name: 'build_npm',
    description: 'Build the frontend using npm run build.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        path: { type: 'string', description: 'Path to frontend project', default: 'frontend' },
      },
      required: [],
    },
  },
];

// Tool handlers
export async function handleBuildTool(
  name: string,
  args: Record<string, unknown>
): Promise<{ content: { type: 'text'; text: string }[]; isError?: boolean }> {
  try {
    switch (name) {
      case 'build_dotnet': {
        const parsed = DotnetBuildSchema.parse(args);
        const buildArgs: string[] = [];

        if (parsed.project) {
          buildArgs.push(parsed.project);
        }

        buildArgs.push('--configuration', parsed.configuration ?? 'Debug');
        buildArgs.push('--verbosity', parsed.verbosity ?? 'minimal');

        const result = await processService.executeDotnet('build', buildArgs);

        // Parse build output for summary
        const warningMatch = result.stdout.match(/(\d+) Warning\(s\)/);
        const errorMatch = result.stdout.match(/(\d+) Error\(s\)/);
        const warnings = warningMatch ? parseInt(warningMatch[1]) : 0;
        const errors = errorMatch ? parseInt(errorMatch[1]) : 0;

        const output = [
          result.success ? '✓ Build succeeded' : '✗ Build failed',
          `  Warnings: ${warnings}`,
          `  Errors: ${errors}`,
          '',
          '--- Output ---',
          result.stdout,
        ];

        if (result.stderr && !result.success) {
          output.push('', '--- Errors ---', result.stderr);
        }

        return {
          content: [{ type: 'text', text: output.join('\n') }],
          isError: !result.success,
        };
      }

      case 'build_npm': {
        const parsed = NpmBuildSchema.parse(args);
        const frontendPath = resolve(config.projectRoot, parsed.path);

        const result = await processService.executeNpm('run', ['build'], {
          cwd: frontendPath,
        });

        const output = [
          result.success ? '✓ Frontend build succeeded' : '✗ Frontend build failed',
          '',
          '--- Output ---',
          result.stdout,
        ];

        if (result.stderr) {
          output.push('', '--- Stderr ---', result.stderr);
        }

        return {
          content: [{ type: 'text', text: output.join('\n') }],
          isError: !result.success,
        };
      }

      default:
        return {
          content: [{ type: 'text', text: `Unknown build tool: ${name}` }],
          isError: true,
        };
    }
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    return {
      content: [{ type: 'text', text: `Error: ${message}` }],
      isError: true,
    };
  }
}

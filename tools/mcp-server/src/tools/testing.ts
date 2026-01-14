import { z } from 'zod';
import { resolve } from 'path';
import { processService } from '../services/process.js';
import { config } from '../config.js';

// Schemas
export const DotnetTestSchema = z.object({
  project: z.string().optional().describe('Path to specific test project (.csproj)'),
  filter: z.string().optional().describe('Filter expression for test selection'),
  verbosity: z.enum(['quiet', 'minimal', 'normal', 'detailed']).optional().default('minimal'),
});

export const NpmTestSchema = z.object({
  path: z.string().optional().default('frontend').describe('Path to frontend project'),
  coverage: z.boolean().optional().default(false).describe('Run with coverage reporting'),
});

// Tool definitions
export const testingTools = [
  {
    name: 'test_dotnet',
    description: 'Run .NET tests using dotnet test. Can run all tests or filter to specific tests.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        project: { type: 'string', description: 'Path to specific test project (.csproj)' },
        filter: { type: 'string', description: 'Filter expression for test selection (e.g., "FullyQualifiedName~MyTest")' },
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
    name: 'test_npm',
    description: 'Run frontend tests using npm test.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        path: { type: 'string', description: 'Path to frontend project', default: 'frontend' },
        coverage: { type: 'boolean', description: 'Run with coverage reporting', default: false },
      },
      required: [],
    },
  },
  {
    name: 'test_coverage',
    description: 'Run .NET tests with code coverage collection using Coverlet. Generates coverage report.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        project: { type: 'string', description: 'Path to specific test project (.csproj)' },
        format: {
          type: 'string',
          enum: ['cobertura', 'opencover', 'json', 'lcov'],
          description: 'Coverage output format',
          default: 'cobertura',
        },
      },
      required: [],
    },
  },
];

// Tool handlers
export async function handleTestingTool(
  name: string,
  args: Record<string, unknown>
): Promise<{ content: { type: 'text'; text: string }[]; isError?: boolean }> {
  try {
    switch (name) {
      case 'test_dotnet': {
        const parsed = DotnetTestSchema.parse(args);
        const testArgs: string[] = [];

        if (parsed.project) {
          testArgs.push(parsed.project);
        }

        testArgs.push(`--verbosity`, parsed.verbosity ?? 'minimal');
        testArgs.push('--no-restore'); // Faster if already restored

        if (parsed.filter) {
          testArgs.push('--filter', parsed.filter);
        }

        const result = await processService.executeDotnet('test', testArgs);

        const output = [
          result.success ? '✓ Tests completed successfully' : '✗ Tests failed',
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

      case 'test_npm': {
        const parsed = NpmTestSchema.parse(args);
        const frontendPath = resolve(config.projectRoot, parsed.path);

        const testArgs = ['run', 'test'];

        if (parsed.coverage) {
          testArgs.push('--', '--coverage');
        }

        const result = await processService.executeNpm(testArgs[0], testArgs.slice(1), {
          cwd: frontendPath,
        });

        const output = [
          result.success ? '✓ Frontend tests completed successfully' : '✗ Frontend tests failed',
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

      case 'test_coverage': {
        const project = args?.project as string | undefined;
        const format = (args?.format as string) || 'cobertura';

        const testArgs: string[] = [];

        if (project) {
          testArgs.push(project);
        }

        // Add Coverlet collection properties
        testArgs.push('/p:CollectCoverage=true');
        testArgs.push(`/p:CoverletOutputFormat=${format}`);
        testArgs.push('/p:CoverletOutput=./coverage/');

        const result = await processService.executeDotnet('test', testArgs);

        const output = [
          result.success ? '✓ Tests with coverage completed' : '✗ Tests failed',
          '',
          '--- Coverage Report ---',
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

      default:
        return {
          content: [{ type: 'text', text: `Unknown testing tool: ${name}` }],
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

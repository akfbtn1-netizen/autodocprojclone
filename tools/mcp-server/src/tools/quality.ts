import { z } from 'zod';
import { resolve } from 'path';
import { processService } from '../services/process.js';
import { config } from '../config.js';

// Schemas
export const QualityGateSchema = z.object({
  projectPath: z.string().optional().describe('Path to validate (relative to project root)'),
  failOnViolations: z.boolean().optional().default(true).describe('Return error status on quality violations'),
});

// Tool definitions
export const qualityTools = [
  {
    name: 'quality_run_gate',
    description:
      'Run the project quality gate checks using the PowerShell quality-gate.ps1 script. ' +
      'Validates code complexity, method length, class size, and other quality metrics.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        projectPath: { type: 'string', description: 'Path to validate (relative to project root)' },
        failOnViolations: { type: 'boolean', description: 'Return error status on quality violations', default: true },
      },
      required: [],
    },
  },
];

// Tool handlers
export async function handleQualityTool(
  name: string,
  args: Record<string, unknown>
): Promise<{ content: { type: 'text'; text: string }[]; isError?: boolean }> {
  try {
    switch (name) {
      case 'quality_run_gate': {
        const parsed = QualityGateSchema.parse(args);
        const scriptPath = resolve(config.projectRoot, 'tools', 'quality-gate.ps1');

        const scriptArgs: string[] = [];
        if (parsed.projectPath) {
          scriptArgs.push('-ProjectPath', parsed.projectPath);
        }

        const result = await processService.executePowerShell(scriptPath, scriptArgs);

        // Parse the output for key metrics
        const output = [
          result.success ? '✓ Quality gate passed' : '✗ Quality gate failed',
          '',
          '--- Quality Report ---',
          result.stdout,
        ];

        if (result.stderr && result.stderr.trim()) {
          output.push('', '--- Warnings/Errors ---', result.stderr);
        }

        const isError = parsed.failOnViolations && !result.success;

        return {
          content: [{ type: 'text', text: output.join('\n') }],
          isError,
        };
      }

      default:
        return {
          content: [{ type: 'text', text: `Unknown quality tool: ${name}` }],
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

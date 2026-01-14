import { z } from 'zod';
import { gitService } from '../services/git.js';

// Schemas
export const GitStatusSchema = z.object({
  path: z.string().optional().describe('Optional path to filter status'),
});

export const GitDiffSchema = z.object({
  staged: z.boolean().optional().default(false).describe('Show staged changes only'),
  path: z.string().optional().describe('Optional path to filter diff'),
  base: z.string().optional().describe('Base commit/branch to compare against'),
});

export const GitLogSchema = z.object({
  limit: z.number().optional().default(10).describe('Maximum number of commits to show'),
  oneline: z.boolean().optional().default(false).describe('Show commits in one-line format'),
  path: z.string().optional().describe('Optional path to filter history'),
});

export const GitBranchSchema = z.object({
  includeRemote: z.boolean().optional().default(false).describe('Include remote branches'),
});

// Tool definitions
export const gitTools = [
  {
    name: 'git_status',
    description: 'Get the current git status showing staged, unstaged, and untracked files.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        path: { type: 'string', description: 'Optional path to filter status' },
      },
      required: [],
    },
  },
  {
    name: 'git_diff',
    description: 'Show git diff of changes. Can show staged changes, unstaged changes, or compare against a base.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        staged: { type: 'boolean', description: 'Show staged changes only', default: false },
        path: { type: 'string', description: 'Optional path to filter diff' },
        base: { type: 'string', description: 'Base commit/branch to compare against' },
      },
      required: [],
    },
  },
  {
    name: 'git_log',
    description: 'Show git commit history.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        limit: { type: 'number', description: 'Maximum number of commits to show', default: 10 },
        oneline: { type: 'boolean', description: 'Show commits in one-line format', default: false },
        path: { type: 'string', description: 'Optional path to filter history' },
      },
      required: [],
    },
  },
  {
    name: 'git_branch_info',
    description: 'Get information about git branches including current branch.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        includeRemote: { type: 'boolean', description: 'Include remote branches', default: false },
      },
      required: [],
    },
  },
];

// Tool handlers
export async function handleGitTool(
  name: string,
  args: Record<string, unknown>
): Promise<{ content: { type: 'text'; text: string }[]; isError?: boolean }> {
  try {
    switch (name) {
      case 'git_status': {
        const parsed = GitStatusSchema.parse(args);
        const status = await gitService.status(parsed.path);

        const sections: string[] = [];
        sections.push(`Branch: ${status.branch}`);

        if (status.staged.length > 0) {
          sections.push('\nStaged changes:');
          status.staged.forEach((f) => sections.push(`  ${f.status}: ${f.path}`));
        }

        if (status.unstaged.length > 0) {
          sections.push('\nUnstaged changes:');
          status.unstaged.forEach((f) => sections.push(`  ${f.status}: ${f.path}`));
        }

        if (status.untracked.length > 0) {
          sections.push('\nUntracked files:');
          status.untracked.forEach((f) => sections.push(`  ${f.path}`));
        }

        if (status.staged.length === 0 && status.unstaged.length === 0 && status.untracked.length === 0) {
          sections.push('\nWorking tree clean');
        }

        return {
          content: [{ type: 'text', text: sections.join('\n') }],
        };
      }

      case 'git_diff': {
        const parsed = GitDiffSchema.parse(args);
        const diff = await gitService.diff({
          staged: parsed.staged,
          path: parsed.path,
          base: parsed.base,
        });

        return {
          content: [{ type: 'text', text: diff }],
        };
      }

      case 'git_log': {
        const parsed = GitLogSchema.parse(args);
        const log = await gitService.log({
          limit: parsed.limit,
          oneline: parsed.oneline,
          path: parsed.path,
        });

        if (typeof log === 'string') {
          return {
            content: [{ type: 'text', text: log }],
          };
        }

        const formatted = log
          .map((entry) => `${entry.hash.substring(0, 7)} | ${entry.date} | ${entry.author} | ${entry.message}`)
          .join('\n');

        return {
          content: [{ type: 'text', text: formatted }],
        };
      }

      case 'git_branch_info': {
        const parsed = GitBranchSchema.parse(args);
        const branchInfo = await gitService.branchInfo(parsed.includeRemote);

        const sections: string[] = [];
        sections.push(`Current branch: ${branchInfo.current}`);
        sections.push('\nLocal branches:');
        branchInfo.local.forEach((b) => {
          sections.push(`  ${b === branchInfo.current ? '* ' : '  '}${b}`);
        });

        if (parsed.includeRemote && branchInfo.remote.length > 0) {
          sections.push('\nRemote branches:');
          branchInfo.remote.forEach((b) => sections.push(`  ${b}`));
        }

        return {
          content: [{ type: 'text', text: sections.join('\n') }],
        };
      }

      default:
        return {
          content: [{ type: 'text', text: `Unknown git tool: ${name}` }],
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

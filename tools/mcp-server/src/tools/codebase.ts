import { z } from 'zod';
import { fileSystemService } from '../services/file-system.js';
import { config } from '../config.js';

// Schemas
export const ReadFileSchema = z.object({
  path: z.string().describe('File path relative to project root'),
  limit: z.number().optional().describe('Maximum number of lines to read'),
  offset: z.number().optional().describe('Line number to start reading from (0-indexed)'),
});

export const SearchCodeSchema = z.object({
  pattern: z.string().describe('Regex pattern to search for'),
  glob: z.string().optional().describe('Glob pattern to filter files (e.g., "**/*.ts")'),
  maxResults: z.number().optional().default(50).describe('Maximum number of results to return'),
});

export const GlobFilesSchema = z.object({
  pattern: z.string().describe('Glob pattern to match files (e.g., "**/*.cs")'),
  path: z.string().optional().describe('Base path to search from (relative to project root)'),
});

export const ListDirectorySchema = z.object({
  path: z.string().describe('Directory path relative to project root'),
});

// Tool definitions
export const codebaseTools = [
  {
    name: 'codebase_read_file',
    description: 'Read the contents of a file from the project. Returns file content with optional line limiting.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        path: { type: 'string', description: 'File path relative to project root' },
        limit: { type: 'number', description: 'Maximum number of lines to read' },
        offset: { type: 'number', description: 'Line number to start reading from (0-indexed)' },
      },
      required: ['path'],
    },
  },
  {
    name: 'codebase_search_code',
    description: 'Search for code matching a regex pattern across the project files.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        pattern: { type: 'string', description: 'Regex pattern to search for' },
        glob: { type: 'string', description: 'Glob pattern to filter files (e.g., "**/*.ts")' },
        maxResults: { type: 'number', description: 'Maximum number of results to return', default: 50 },
      },
      required: ['pattern'],
    },
  },
  {
    name: 'codebase_glob_files',
    description: 'Find files matching a glob pattern in the project.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        pattern: { type: 'string', description: 'Glob pattern to match files (e.g., "**/*.cs")' },
        path: { type: 'string', description: 'Base path to search from (relative to project root)' },
      },
      required: ['pattern'],
    },
  },
  {
    name: 'codebase_list_directory',
    description: 'List the contents of a directory in the project.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        path: { type: 'string', description: 'Directory path relative to project root' },
      },
      required: ['path'],
    },
  },
];

// Tool handlers
export async function handleCodebaseTool(
  name: string,
  args: Record<string, unknown>
): Promise<{ content: { type: 'text'; text: string }[]; isError?: boolean }> {
  try {
    switch (name) {
      case 'codebase_read_file': {
        const parsed = ReadFileSchema.parse(args);
        const content = await fileSystemService.readFile(parsed.path, {
          limit: parsed.limit,
          offset: parsed.offset,
        });

        // Truncate if too long
        const truncated =
          content.length > config.characterLimit
            ? content.substring(0, config.characterLimit) +
              `\n\n[Content truncated. Showing first ${config.characterLimit} characters. Use offset/limit to read more.]`
            : content;

        return {
          content: [{ type: 'text', text: truncated }],
        };
      }

      case 'codebase_search_code': {
        const parsed = SearchCodeSchema.parse(args);
        const results = await fileSystemService.searchCode(parsed.pattern, {
          glob: parsed.glob,
          maxResults: parsed.maxResults,
        });

        if (results.length === 0) {
          return {
            content: [{ type: 'text', text: 'No matches found.' }],
          };
        }

        const formatted = results
          .map((r) => `${r.file}:${r.line}: ${r.content}`)
          .join('\n');

        return {
          content: [
            {
              type: 'text',
              text: `Found ${results.length} matches:\n\n${formatted}`,
            },
          ],
        };
      }

      case 'codebase_glob_files': {
        const parsed = GlobFilesSchema.parse(args);
        const files = await fileSystemService.globFiles(parsed.pattern, parsed.path);

        if (files.length === 0) {
          return {
            content: [{ type: 'text', text: 'No files found matching the pattern.' }],
          };
        }

        return {
          content: [
            {
              type: 'text',
              text: `Found ${files.length} files:\n\n${files.join('\n')}`,
            },
          ],
        };
      }

      case 'codebase_list_directory': {
        const parsed = ListDirectorySchema.parse(args);
        const entries = await fileSystemService.listDirectory(parsed.path);

        const formatted = entries
          .sort((a, b) => {
            // Directories first, then files
            if (a.type !== b.type) {
              return a.type === 'directory' ? -1 : 1;
            }
            return a.name.localeCompare(b.name);
          })
          .map((e) => `${e.type === 'directory' ? '[DIR]' : '[FILE]'} ${e.name}`)
          .join('\n');

        return {
          content: [{ type: 'text', text: formatted }],
        };
      }

      default:
        return {
          content: [{ type: 'text', text: `Unknown codebase tool: ${name}` }],
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

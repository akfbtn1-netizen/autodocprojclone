import { z } from 'zod';
import { readFile, readdir } from 'fs/promises';
import { resolve, join, relative } from 'path';
import { config } from '../config.js';
import { processService } from '../services/process.js';

// Schemas
export const IndexHandlersSchema = z.object({
  pattern: z.string().optional().describe('Filter handlers by name pattern'),
});

export const IndexEntitiesSchema = z.object({
  layer: z.string().optional().describe('Layer to scan (Domain, Application, etc.)'),
});

export const FindUsagesSchema = z.object({
  symbol: z.string().describe('Class name, method name, or symbol to search for'),
  fileType: z.string().optional().default('*.cs').describe('File type filter (e.g., *.cs, *.ts)'),
});

// Tool definitions
export const codeIndexTools = [
  {
    name: 'code_index_handlers',
    description:
      'Index all MediatR command/query handlers in the Application layer. ' +
      'Shows request types, response types, and file locations.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        pattern: { type: 'string', description: 'Filter handlers by name pattern (optional)' },
      },
      required: [],
    },
  },
  {
    name: 'code_index_entities',
    description: 'Index all domain entities with their properties and relationships.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        layer: { type: 'string', description: 'Layer to scan (Domain, Application, etc.)' },
      },
      required: [],
    },
  },
  {
    name: 'code_find_usages',
    description: 'Find where a class, method, or symbol is used across the codebase.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        symbol: { type: 'string', description: 'Class name, method name, or symbol to search for' },
        fileType: { type: 'string', description: 'File type filter (e.g., *.cs, *.ts)', default: '*.cs' },
      },
      required: ['symbol'],
    },
  },
];

interface Handler {
  handler: string;
  request: string;
  response: string;
  file: string;
}

interface Entity {
  name: string;
  baseClass: string | null;
  properties: { type: string; name: string }[];
  file: string;
}

// Recursive directory scanner
async function scanDirectory(
  dir: string,
  callback: (filePath: string, content: string) => Promise<void>,
  fileFilter?: (filename: string) => boolean
): Promise<void> {
  try {
    const entries = await readdir(dir, { withFileTypes: true });

    for (const entry of entries) {
      const fullPath = join(dir, entry.name);

      if (entry.isDirectory() && !entry.name.startsWith('.') && entry.name !== 'node_modules' && entry.name !== 'bin' && entry.name !== 'obj') {
        await scanDirectory(fullPath, callback, fileFilter);
      } else if (entry.isFile() && (!fileFilter || fileFilter(entry.name))) {
        try {
          const content = await readFile(fullPath, 'utf-8');
          await callback(fullPath, content);
        } catch {
          // Skip files that can't be read
        }
      }
    }
  } catch {
    // Skip directories we can't access
  }
}

// Tool handlers
export async function handleCodeIndexTool(
  name: string,
  args: Record<string, unknown>
): Promise<{ content: { type: 'text'; text: string }[]; isError?: boolean }> {
  try {
    switch (name) {
      case 'code_index_handlers': {
        const parsed = IndexHandlersSchema.parse(args);
        const appDir = resolve(config.projectRoot, 'src', 'Core', 'Application');
        const handlers: Handler[] = [];

        await scanDirectory(
          appDir,
          async (filePath, content) => {
            // Match MediatR handler pattern
            const handlerMatch = content.match(
              /class\s+(\w+)\s*:\s*IRequestHandler<([^,>]+)(?:,\s*([^>]+))?>/
            );

            if (handlerMatch) {
              const handlerName = handlerMatch[1];

              if (!parsed.pattern || handlerName.toLowerCase().includes(parsed.pattern.toLowerCase())) {
                handlers.push({
                  handler: handlerName,
                  request: handlerMatch[2].trim(),
                  response: handlerMatch[3]?.trim() || 'Unit',
                  file: relative(config.projectRoot, filePath).replace(/\\/g, '/'),
                });
              }
            }
          },
          (filename) => filename.endsWith('Handler.cs')
        );

        if (handlers.length === 0) {
          return {
            content: [
              {
                type: 'text',
                text: parsed.pattern
                  ? `No handlers found matching: ${parsed.pattern}`
                  : 'No MediatR handlers found',
              },
            ],
          };
        }

        // Format output
        const output = [
          `Found ${handlers.length} handlers:`,
          '',
          ...handlers.map(
            (h) => `${h.handler}\n  Request: ${h.request}\n  Response: ${h.response}\n  File: ${h.file}`
          ),
        ];

        return {
          content: [{ type: 'text', text: output.join('\n') }],
        };
      }

      case 'code_index_entities': {
        const parsed = IndexEntitiesSchema.parse(args);
        const layer = parsed.layer || 'Domain';
        const coreDir = resolve(config.projectRoot, 'src', 'Core', layer);
        const entities: Entity[] = [];

        await scanDirectory(
          coreDir,
          async (filePath, content) => {
            // Match entity classes (exclude interfaces and exceptions)
            const classMatch = content.match(/public\s+(?:sealed\s+)?class\s+(\w+)(?:\s*:\s*([^{]+))?/);

            if (
              classMatch &&
              !classMatch[1].startsWith('I') &&
              !classMatch[1].includes('Exception') &&
              !classMatch[1].includes('Interface')
            ) {
              const properties: { type: string; name: string }[] = [];
              const propRegex = /public\s+(?:required\s+)?(\w+(?:<[^>]+>)?(?:\?)?)\s+(\w+)\s*{\s*get;/g;

              let propMatch;
              while ((propMatch = propRegex.exec(content)) !== null) {
                properties.push({
                  type: propMatch[1],
                  name: propMatch[2],
                });
              }

              entities.push({
                name: classMatch[1],
                baseClass: classMatch[2]?.trim() || null,
                properties,
                file: relative(config.projectRoot, filePath).replace(/\\/g, '/'),
              });
            }
          },
          (filename) => filename.endsWith('.cs')
        );

        if (entities.length === 0) {
          return {
            content: [{ type: 'text', text: `No entities found in ${layer} layer` }],
          };
        }

        // Format output
        const output = [
          `Found ${entities.length} entities in ${layer}:`,
          '',
          ...entities.map((e) => {
            const propsStr = e.properties.map((p) => `    ${p.type} ${p.name}`).join('\n');
            return `${e.name}${e.baseClass ? ` : ${e.baseClass}` : ''}\n  File: ${e.file}\n  Properties:\n${propsStr || '    (none)'}`;
          }),
        ];

        return {
          content: [{ type: 'text', text: output.join('\n\n') }],
        };
      }

      case 'code_find_usages': {
        const parsed = FindUsagesSchema.parse(args);
        const srcDir = resolve(config.projectRoot, 'src');

        // Use findstr on Windows for fast search
        const result = await processService.execute(
          'findstr',
          ['/S', '/I', '/N', `/C:${parsed.symbol}`, parsed.fileType ?? '*.cs'],
          { cwd: srcDir, timeout: 30000 }
        );

        if (!result.stdout || result.stdout.trim() === '') {
          return {
            content: [{ type: 'text', text: `No usages found for: ${parsed.symbol}` }],
          };
        }

        // Parse and format results
        const lines = result.stdout.trim().split('\n').slice(0, 50); // Limit to 50 results
        const output = [
          `Found usages of "${parsed.symbol}":`,
          '',
          ...lines.map((line) => `  ${line.trim()}`),
          lines.length === 50 ? '\n... (results truncated to 50)' : '',
        ];

        return {
          content: [{ type: 'text', text: output.filter(Boolean).join('\n') }],
        };
      }

      default:
        return {
          content: [{ type: 'text', text: `Unknown code index tool: ${name}` }],
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

import { z } from 'zod';
import { readFile, writeFile, mkdir } from 'fs/promises';
import { resolve, dirname } from 'path';
import { config } from '../config.js';

// Memory file location
const MEMORY_FILE = resolve(config.projectRoot, '.claude', 'working-memory.md');

// Schemas
export const UpdateMemorySchema = z.object({
  section: z
    .enum(['current_task', 'decisions', 'next_steps', 'questions', 'context'])
    .describe('Which section to update'),
  content: z.string().describe('Content to add/update'),
  replace: z.boolean().optional().default(false).describe('Replace section content instead of appending'),
});

export const ReadMemorySchema = z.object({
  section: z
    .enum(['current_task', 'decisions', 'next_steps', 'questions', 'context', 'all'])
    .optional()
    .default('all')
    .describe('Which section to read'),
});

// Tool definitions
export const memoryTools = [
  {
    name: 'memory_update',
    description:
      'Update working memory with current task context, decisions, or progress. ' +
      'This persists across sessions and helps maintain continuity.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        section: {
          type: 'string',
          enum: ['current_task', 'decisions', 'next_steps', 'questions', 'context'],
          description: 'Which section to update',
        },
        content: { type: 'string', description: 'Content to add/update' },
        replace: { type: 'boolean', description: 'Replace section content instead of appending', default: false },
      },
      required: ['section', 'content'],
    },
  },
  {
    name: 'memory_read',
    description: 'Read current working memory to understand session context and history.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        section: {
          type: 'string',
          enum: ['current_task', 'decisions', 'next_steps', 'questions', 'context', 'all'],
          description: 'Which section to read (default: all)',
          default: 'all',
        },
      },
      required: [],
    },
  },
  {
    name: 'memory_clear',
    description: 'Clear a specific section or all of working memory.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        section: {
          type: 'string',
          enum: ['current_task', 'decisions', 'next_steps', 'questions', 'context', 'all'],
          description: 'Which section to clear',
        },
      },
      required: ['section'],
    },
  },
];

// Section headers mapping
const SECTION_HEADERS: Record<string, string> = {
  current_task: '## Current Task',
  decisions: '## Recent Decisions',
  next_steps: '## Next Steps',
  questions: '## Open Questions',
  context: '## Context',
};

// Helper to read memory file
async function readMemoryFile(): Promise<string> {
  try {
    return await readFile(MEMORY_FILE, 'utf-8');
  } catch {
    return createEmptyMemory();
  }
}

// Helper to write memory file
async function writeMemoryFile(content: string): Promise<void> {
  await mkdir(dirname(MEMORY_FILE), { recursive: true });
  await writeFile(MEMORY_FILE, content, 'utf-8');
}

// Create empty memory template
function createEmptyMemory(): string {
  const timestamp = new Date().toISOString();
  return `# Working Memory

Last Updated: ${timestamp}

## Current Task

(No current task)

## Recent Decisions

(No decisions recorded)

## Next Steps

(No next steps defined)

## Open Questions

(No open questions)

## Context

(No context saved)
`;
}

// Extract a section from memory content
function extractSection(content: string, section: string): string {
  const header = SECTION_HEADERS[section];
  if (!header) return '';

  const headerIndex = content.indexOf(header);
  if (headerIndex === -1) return '';

  const nextHeaderIndex = content.indexOf('\n## ', headerIndex + 1);
  const sectionContent =
    nextHeaderIndex === -1 ? content.substring(headerIndex) : content.substring(headerIndex, nextHeaderIndex);

  return sectionContent.trim();
}

// Tool handlers
export async function handleMemoryTool(
  name: string,
  args: Record<string, unknown>
): Promise<{ content: { type: 'text'; text: string }[]; isError?: boolean }> {
  try {
    switch (name) {
      case 'memory_update': {
        const parsed = UpdateMemorySchema.parse(args);
        const timestamp = new Date().toISOString();
        const localTime = new Date().toLocaleString();

        let memoryContent = await readMemoryFile();
        const header = SECTION_HEADERS[parsed.section];

        // Ensure section exists
        if (!memoryContent.includes(header)) {
          memoryContent += `\n${header}\n\n`;
        }

        // Find section boundaries
        const headerIndex = memoryContent.indexOf(header);
        const nextHeaderIndex = memoryContent.indexOf('\n## ', headerIndex + 1);

        // Prepare new entry
        const entry = parsed.replace
          ? `\n${parsed.content}\n`
          : `\n[${localTime}] ${parsed.content}\n`;

        if (parsed.replace) {
          // Replace entire section content
          const beforeSection = memoryContent.substring(0, headerIndex + header.length);
          const afterSection = nextHeaderIndex === -1 ? '' : memoryContent.substring(nextHeaderIndex);
          memoryContent = beforeSection + entry + afterSection;
        } else {
          // Append to section
          if (nextHeaderIndex === -1) {
            memoryContent += entry;
          } else {
            memoryContent =
              memoryContent.substring(0, nextHeaderIndex) + entry + memoryContent.substring(nextHeaderIndex);
          }
        }

        // Update timestamp
        memoryContent = memoryContent.replace(/Last Updated: .*/, `Last Updated: ${timestamp}`);

        await writeMemoryFile(memoryContent);

        return {
          content: [
            {
              type: 'text',
              text: `Memory updated: ${parsed.section}\nEntry: ${parsed.content.substring(0, 100)}${parsed.content.length > 100 ? '...' : ''}`,
            },
          ],
        };
      }

      case 'memory_read': {
        const parsed = ReadMemorySchema.parse(args);
        const memoryContent = await readMemoryFile();

        if (parsed.section === 'all') {
          return {
            content: [{ type: 'text', text: memoryContent }],
          };
        }

        const sectionContent = extractSection(memoryContent, parsed.section);

        if (!sectionContent) {
          return {
            content: [{ type: 'text', text: `Section "${parsed.section}" is empty or not found` }],
          };
        }

        return {
          content: [{ type: 'text', text: sectionContent }],
        };
      }

      case 'memory_clear': {
        const section = args?.section as string;

        if (!section) {
          return {
            content: [{ type: 'text', text: 'Error: section is required' }],
            isError: true,
          };
        }

        if (section === 'all') {
          await writeMemoryFile(createEmptyMemory());
          return {
            content: [{ type: 'text', text: 'All memory cleared' }],
          };
        }

        const memoryContent = await readMemoryFile();
        const header = SECTION_HEADERS[section];

        if (!header) {
          return {
            content: [{ type: 'text', text: `Unknown section: ${section}` }],
            isError: true,
          };
        }

        const headerIndex = memoryContent.indexOf(header);
        if (headerIndex === -1) {
          return {
            content: [{ type: 'text', text: `Section "${section}" not found` }],
          };
        }

        const nextHeaderIndex = memoryContent.indexOf('\n## ', headerIndex + 1);
        const clearedContent =
          memoryContent.substring(0, headerIndex + header.length) +
          '\n\n(Cleared)\n' +
          (nextHeaderIndex === -1 ? '' : memoryContent.substring(nextHeaderIndex));

        await writeMemoryFile(clearedContent);

        return {
          content: [{ type: 'text', text: `Section "${section}" cleared` }],
        };
      }

      default:
        return {
          content: [{ type: 'text', text: `Unknown memory tool: ${name}` }],
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

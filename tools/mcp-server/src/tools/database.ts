import { z } from 'zod';
import { readFile, readdir } from 'fs/promises';
import { resolve, join, relative } from 'path';
import { config } from '../config.js';
import { processService } from '../services/process.js';

// Schemas
export const GetDbSchemaSchema = z.object({
  entity: z.string().optional().describe('Get schema for specific entity'),
});

export const CheckMigrationsSchema = z.object({
  project: z.string().optional().describe('Path to Infrastructure project'),
});

// Tool definitions
export const databaseTools = [
  {
    name: 'db_get_schema',
    description:
      'Get database schema from EF Core DbContext. Shows all entities, properties, and DbSet definitions.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        entity: { type: 'string', description: 'Get schema for specific entity (optional, shows all if omitted)' },
      },
      required: [],
    },
  },
  {
    name: 'db_check_migrations',
    description: 'Check EF Core migration status - shows pending and applied migrations.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        project: { type: 'string', description: 'Path to Infrastructure project (optional)' },
      },
      required: [],
    },
  },
];

interface DbSet {
  entity: string;
  dbSetName: string;
}

// Recursive search for DbContext file
async function findDbContext(dir: string): Promise<string | null> {
  try {
    const entries = await readdir(dir, { withFileTypes: true });

    for (const entry of entries) {
      const fullPath = join(dir, entry.name);

      if (entry.isDirectory() && !entry.name.startsWith('.') && entry.name !== 'bin' && entry.name !== 'obj') {
        const found = await findDbContext(fullPath);
        if (found) return found;
      } else if (entry.name.includes('DbContext.cs') || entry.name.includes('Context.cs')) {
        const content = await readFile(fullPath, 'utf-8');
        if (content.includes('DbContext') && content.includes('DbSet<')) {
          return fullPath;
        }
      }
    }

    return null;
  } catch {
    return null;
  }
}

// Tool handlers
export async function handleDatabaseTool(
  name: string,
  args: Record<string, unknown>
): Promise<{ content: { type: 'text'; text: string }[]; isError?: boolean }> {
  try {
    switch (name) {
      case 'db_get_schema': {
        const parsed = GetDbSchemaSchema.parse(args);
        const infraDir = resolve(config.projectRoot, 'src', 'Core', 'Infrastructure');

        const contextFile = await findDbContext(infraDir);

        if (!contextFile) {
          // Try alternate location
          const altInfraDir = resolve(config.projectRoot, 'src', 'Infrastructure');
          const altContextFile = await findDbContext(altInfraDir);

          if (!altContextFile) {
            return {
              content: [
                {
                  type: 'text',
                  text: 'DbContext file not found. Searched in:\n  - src/Core/Infrastructure\n  - src/Infrastructure',
                },
              ],
              isError: true,
            };
          }
        }

        const finalContextFile = contextFile || (await findDbContext(resolve(config.projectRoot, 'src', 'Infrastructure')));

        if (!finalContextFile) {
          return {
            content: [{ type: 'text', text: 'DbContext file not found' }],
            isError: true,
          };
        }

        const content = await readFile(finalContextFile, 'utf-8');

        // Extract DbSet definitions
        const dbSets: DbSet[] = [];
        const dbSetRegex = /public\s+(?:virtual\s+)?DbSet<(\w+)>\s+(\w+)/g;

        let match;
        while ((match = dbSetRegex.exec(content)) !== null) {
          if (!parsed.entity || match[1].toLowerCase() === parsed.entity.toLowerCase()) {
            dbSets.push({
              entity: match[1],
              dbSetName: match[2],
            });
          }
        }

        // Extract DbContext class name
        const contextNameMatch = content.match(/class\s+(\w+)\s*:\s*DbContext/);
        const contextName = contextNameMatch ? contextNameMatch[1] : 'Unknown';

        if (dbSets.length === 0) {
          return {
            content: [
              {
                type: 'text',
                text: parsed.entity
                  ? `No DbSet found for entity: ${parsed.entity}`
                  : 'No DbSet definitions found in DbContext',
              },
            ],
          };
        }

        // Format output
        const output = [
          `DbContext: ${contextName}`,
          `File: ${relative(config.projectRoot, finalContextFile).replace(/\\/g, '/')}`,
          '',
          `Found ${dbSets.length} DbSets:`,
          '',
          ...dbSets.map((ds) => `  DbSet<${ds.entity}> ${ds.dbSetName}`),
        ];

        return {
          content: [{ type: 'text', text: output.join('\n') }],
        };
      }

      case 'db_check_migrations': {
        const parsed = CheckMigrationsSchema.parse(args);

        // Find the Infrastructure project
        let infraProject = parsed.project;
        if (!infraProject) {
          // Try to find it
          const possiblePaths = [
            'src/Core/Infrastructure/Core.Infrastructure.csproj',
            'src/Infrastructure/Infrastructure.csproj',
          ];

          for (const p of possiblePaths) {
            try {
              await readFile(resolve(config.projectRoot, p), 'utf-8');
              infraProject = p;
              break;
            } catch {
              continue;
            }
          }
        }

        const result = await processService.execute(
          'dotnet',
          ['ef', 'migrations', 'list', ...(infraProject ? ['--project', infraProject] : [])],
          { cwd: config.projectRoot, timeout: 60000 }
        );

        if (!result.success) {
          // Check if EF tools are installed
          if (result.stderr?.includes('No executable found')) {
            return {
              content: [
                {
                  type: 'text',
                  text: 'EF Core tools not installed. Run: dotnet tool install --global dotnet-ef',
                },
              ],
              isError: true,
            };
          }

          return {
            content: [
              {
                type: 'text',
                text: `Failed to list migrations:\n${result.stderr || result.stdout || 'Unknown error'}`,
              },
            ],
            isError: true,
          };
        }

        const output = result.stdout || 'No migrations found';

        return {
          content: [{ type: 'text', text: output }],
        };
      }

      default:
        return {
          content: [{ type: 'text', text: `Unknown database tool: ${name}` }],
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

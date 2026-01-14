import { z } from 'zod';
import { readFile, readdir } from 'fs/promises';
import { resolve, join } from 'path';
import { config } from '../config.js';

// Schemas
export const ListEndpointsSchema = z.object({
  controller: z.string().optional().describe('Filter by controller name'),
});

// Tool definitions
export const apiTools = [
  {
    name: 'api_list_endpoints',
    description:
      'List all API endpoints with HTTP methods, routes, and controller actions. ' +
      'Useful for understanding API surface or building frontend integrations.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        controller: { type: 'string', description: 'Filter by controller name (optional)' },
      },
      required: [],
    },
  },
];

interface Endpoint {
  method: string;
  route: string;
  controller: string;
  action: string;
  returnType: string;
}

// Tool handlers
export async function handleApiTool(
  name: string,
  args: Record<string, unknown>
): Promise<{ content: { type: 'text'; text: string }[]; isError?: boolean }> {
  try {
    switch (name) {
      case 'api_list_endpoints': {
        const parsed = ListEndpointsSchema.parse(args);
        const apiDir = resolve(config.projectRoot, 'src', 'Api', 'Controllers');

        const endpoints: Endpoint[] = [];

        try {
          const files = await readdir(apiDir);

          for (const file of files) {
            if (!file.endsWith('Controller.cs')) continue;
            if (parsed.controller && !file.toLowerCase().includes(parsed.controller.toLowerCase())) continue;

            const content = await readFile(join(apiDir, file), 'utf-8');

            // Extract base route from [Route("...")] attribute
            const controllerMatch = content.match(/\[Route\("([^"]+)"\)\]/);
            const baseRoute = controllerMatch ? `/${controllerMatch[1]}` : '/api';

            // Extract controller name
            const controllerName = file.replace('.cs', '');

            // Match HTTP method attributes and action methods
            const httpMethodRegex =
              /\[(Http(Get|Post|Put|Delete|Patch))(?:\("([^"]*)")?)?\]\s*(?:\[.*?\]\s*)*public\s+(?:async\s+)?(?:Task<)?(?:ActionResult<)?(\w+)/g;

            let match;
            while ((match = httpMethodRegex.exec(content)) !== null) {
              const method = match[2].toUpperCase();
              const routeSuffix = match[3] || '';
              const returnType = match[4];

              // Find the action name (next method name after the attribute)
              const afterMatch = content.substring(match.index);
              const actionMatch = afterMatch.match(/public\s+(?:async\s+)?(?:Task<)?(?:ActionResult<)?(?:\w+>?>?\s+)?(\w+)\s*\(/);
              const action = actionMatch ? actionMatch[1] : 'Unknown';

              endpoints.push({
                method,
                route: routeSuffix ? `${baseRoute}/${routeSuffix}` : baseRoute,
                controller: controllerName,
                action,
                returnType,
              });
            }
          }

          if (endpoints.length === 0) {
            return {
              content: [
                {
                  type: 'text',
                  text: parsed.controller
                    ? `No endpoints found for controller: ${parsed.controller}`
                    : 'No API endpoints found',
                },
              ],
            };
          }

          // Format output
          const output = [
            `Found ${endpoints.length} endpoints:`,
            '',
            ...endpoints.map(
              (e) => `${e.method.padEnd(7)} ${e.route.padEnd(40)} ${e.controller}.${e.action}() -> ${e.returnType}`
            ),
          ];

          return {
            content: [{ type: 'text', text: output.join('\n') }],
          };
        } catch (error) {
          const message = error instanceof Error ? error.message : String(error);
          return {
            content: [{ type: 'text', text: `Error scanning API controllers: ${message}` }],
            isError: true,
          };
        }
      }

      default:
        return {
          content: [{ type: 'text', text: `Unknown API tool: ${name}` }],
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

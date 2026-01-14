import { frontendPrompt, getFrontendPrompt } from './frontend.js';
import { apiIntegrationPrompt, getApiIntegrationPrompt } from './api-integration.js';
import { e2eTestingPrompt, getE2eTestingPrompt } from './e2e-testing.js';
import { agentArchitectPrompt, getAgentArchitectPrompt } from './agent-architect.js';

// All prompt definitions
export const allPrompts = [
  frontendPrompt,
  apiIntegrationPrompt,
  e2eTestingPrompt,
  agentArchitectPrompt,
];

// Prompt handler router
export async function handlePromptRequest(
  name: string,
  args: Record<string, string>
): Promise<{ messages: { role: 'user'; content: { type: 'text'; text: string } }[] }> {
  switch (name) {
    case 'frontend-specialist':
      return getFrontendPrompt({
        task: args.task ?? '',
        technology: args.technology,
      });

    case 'api-integration-specialist':
      return getApiIntegrationPrompt({
        task: args.task ?? '',
        apiType: args.apiType,
      });

    case 'e2e-testing-specialist':
      return getE2eTestingPrompt({
        task: args.task ?? '',
        framework: args.framework,
      });

    case 'agent-architect':
      return getAgentArchitectPrompt({
        task: args.task ?? '',
        pattern: args.pattern,
      });

    default:
      throw new Error(`Unknown prompt: ${name}`);
  }
}

export {
  frontendPrompt,
  apiIntegrationPrompt,
  e2eTestingPrompt,
  agentArchitectPrompt,
  getFrontendPrompt,
  getApiIntegrationPrompt,
  getE2eTestingPrompt,
  getAgentArchitectPrompt,
};

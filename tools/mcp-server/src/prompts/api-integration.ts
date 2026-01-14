import { getSkill } from './loader.js';

export const apiIntegrationPrompt = {
  name: 'api-integration-specialist',
  description:
    'Expert in integrating third-party APIs with proper authentication, error handling, rate limiting, and retry logic. ' +
    'Use for REST APIs, GraphQL, webhooks, OAuth flows, and building robust API clients.',
  arguments: [
    {
      name: 'task',
      description: 'The API integration task or question you need help with',
      required: true,
    },
    {
      name: 'apiType',
      description: 'Type of API (REST, GraphQL, Webhook)',
      required: false,
    },
  ],
};

export async function getApiIntegrationPrompt(args: {
  task: string;
  apiType?: string;
}): Promise<{ messages: { role: 'user'; content: { type: 'text'; text: string } }[] }> {
  const skill = await getSkill('apiIntegration');

  const contextParts = [
    '# API Integration Specialist Mode',
    '',
    `You are now operating as an API integration specialist. ${skill.metadata.description}`,
    '',
    '## Your Expertise',
    skill.content,
  ];

  // Add relevant references
  if (skill.references.size > 0) {
    contextParts.push('', '## Reference Documentation');
    for (const [name, content] of skill.references) {
      const truncated = content.length > 10000 ? content.substring(0, 10000) + '\n...[truncated]' : content;
      contextParts.push('', `### ${name}`, truncated);
    }
  }

  contextParts.push(
    '',
    '---',
    '',
    '## Current Task',
    args.apiType ? `**API Type:** ${args.apiType}` : '',
    '',
    args.task
  );

  return {
    messages: [
      {
        role: 'user',
        content: {
          type: 'text',
          text: contextParts.filter(Boolean).join('\n'),
        },
      },
    ],
  };
}

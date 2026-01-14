import { getSkill } from './loader.js';

export const e2eTestingPrompt = {
  name: 'e2e-testing-specialist',
  description:
    'Master end-to-end testing with Playwright and Cypress to build reliable test suites. ' +
    'Use for implementing E2E tests, debugging flaky tests, or establishing testing standards.',
  arguments: [
    {
      name: 'task',
      description: 'The E2E testing task or question you need help with',
      required: true,
    },
    {
      name: 'framework',
      description: 'Testing framework (Playwright, Cypress)',
      required: false,
    },
  ],
};

export async function getE2eTestingPrompt(args: {
  task: string;
  framework?: string;
}): Promise<{ messages: { role: 'user'; content: { type: 'text'; text: string } }[] }> {
  const skill = await getSkill('e2eTesting');

  const contextParts = [
    '# E2E Testing Specialist Mode',
    '',
    `You are now operating as an E2E testing specialist. ${skill.metadata.description}`,
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
    args.framework ? `**Framework:** ${args.framework}` : '',
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

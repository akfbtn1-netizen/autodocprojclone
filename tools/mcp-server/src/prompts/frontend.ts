import { getSkill } from './loader.js';

export const frontendPrompt = {
  name: 'frontend-specialist',
  description:
    'Senior frontend development expertise for React, Next.js, TypeScript, and modern web development. ' +
    'Use for component development, performance optimization, state management, and UI best practices.',
  arguments: [
    {
      name: 'task',
      description: 'The frontend task or question you need help with',
      required: true,
    },
    {
      name: 'technology',
      description: 'Primary technology focus',
      required: false,
    },
  ],
};

export async function getFrontendPrompt(args: {
  task: string;
  technology?: string;
}): Promise<{ messages: { role: 'user'; content: { type: 'text'; text: string } }[] }> {
  const skill = await getSkill('frontend');

  const contextParts = [
    '# Frontend Specialist Mode',
    '',
    `You are now operating as a senior frontend specialist. ${skill.metadata.description}`,
    '',
    '## Your Expertise',
    skill.content,
  ];

  // Add relevant references
  if (skill.references.size > 0) {
    contextParts.push('', '## Reference Documentation');
    for (const [name, content] of skill.references) {
      // Include key references, truncated if necessary
      const truncated = content.length > 10000 ? content.substring(0, 10000) + '\n...[truncated]' : content;
      contextParts.push('', `### ${name}`, truncated);
    }
  }

  contextParts.push(
    '',
    '---',
    '',
    '## Current Task',
    args.technology ? `**Technology Focus:** ${args.technology}` : '',
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

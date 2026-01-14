import { getSkill } from './loader.js';

export const agentArchitectPrompt = {
  name: 'agent-architect',
  description:
    'Design and implement production-grade AI agent systems using modern orchestration patterns. ' +
    'Covers Claude Agent SDK, LangGraph, CrewAI, MCP servers, multi-agent systems, and agentic RAG.',
  arguments: [
    {
      name: 'task',
      description: 'The AI agent architecture task or question you need help with',
      required: true,
    },
    {
      name: 'pattern',
      description: 'Agent pattern focus (ReAct, Supervisor, Pipeline, RAG)',
      required: false,
    },
  ],
};

export async function getAgentArchitectPrompt(args: {
  task: string;
  pattern?: string;
}): Promise<{ messages: { role: 'user'; content: { type: 'text'; text: string } }[] }> {
  const skill = await getSkill('agentArchitecture');

  const contextParts = [
    '# AI Agent Architect Mode',
    '',
    `You are now operating as an AI agent architecture specialist. ${skill.metadata.description}`,
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
    args.pattern ? `**Pattern Focus:** ${args.pattern}` : '',
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

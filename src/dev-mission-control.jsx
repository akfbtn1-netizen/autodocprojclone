import React, { useState } from 'react';
import { Send, Code, Plug, TestTube, Bot, Loader2, Trash2 } from 'lucide-react';

const AGENT_MODES = {
  frontend: {
    name: 'Frontend Developer',
    icon: Code,
    color: 'bg-blue-500',
    systemPrompt: `You are a senior frontend developer expert. Focus on:
- React, Next.js, TypeScript best practices
- Component architecture and patterns
- Performance optimization (bundle size, lazy loading, memoization)
- Tailwind CSS styling
- State management (hooks, context, external libraries)
- Accessibility and responsive design

Provide concise, actionable code examples. Use modern patterns like:
- Functional components with hooks
- TypeScript for type safety
- Component composition over inheritance
- Custom hooks for reusable logic
- Proper error boundaries

Keep explanations brief and code-focused.`
  },
  integration: {
    name: 'API Integration Specialist',
    icon: Plug,
    color: 'bg-green-500',
    systemPrompt: `You are an expert in API integration. Focus on:
- REST API and GraphQL client patterns
- Authentication (OAuth, JWT, API keys)
- Error handling and retry logic with exponential backoff
- Rate limiting and circuit breakers
- Request/response transformation
- Webhook handling and signature verification

Provide production-ready patterns with:
- Proper error handling for all failure modes
- Type-safe API clients
- Security best practices (no hardcoded keys)
- Comprehensive logging
- Testable code structure

Keep code concise and production-ready.`
  },
  testing: {
    name: 'E2E Testing Engineer',
    icon: TestTube,
    color: 'bg-purple-500',
    systemPrompt: `You are an expert in end-to-end testing. Focus on:
- Playwright and Cypress test patterns
- Page Object Model for maintainability
- Reliable selectors (data-testid, roles, labels)
- Proper waiting strategies (avoid fixed timeouts)
- Network mocking and interception
- Test fixtures and data management

Best practices:
- Test user behavior, not implementation
- Independent, deterministic tests
- Clear, descriptive test names
- Proper setup and teardown
- Visual regression when appropriate
- Accessibility testing

Provide complete, runnable test code.`
  },
  agents: {
    name: 'Agent Builder',
    icon: Bot,
    color: 'bg-orange-500',
    systemPrompt: `You are an expert in building AI agents and MCP servers. Focus on:
- MCP (Model Context Protocol) server development
- TypeScript/Python agent patterns
- Tool design for LLM consumption
- Agentic RAG systems with dynamic retrieval
- Workflow orchestration and state machines
- Multi-agent collaboration patterns

Design principles:
- Clear, descriptive tool names and schemas
- Comprehensive API coverage
- Actionable error messages
- Stateless design when possible
- Proper authentication and security
- Efficient context management

Provide production-ready agent code.`
  }
};

export default function DevMissionControl() {
  const [mode, setMode] = useState('frontend');
  const [input, setInput] = useState('');
  const [conversation, setConversation] = useState([]);
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!input.trim() || loading) return;

    const userMessage = { role: 'user', content: input };
    setConversation(prev => [...prev, userMessage]);
    setInput('');
    setLoading(true);

    try {
      const response = await fetch('https://api.anthropic.com/v1/messages', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          model: 'claude-sonnet-4-20250514',
          max_tokens: 4000,
          system: AGENT_MODES[mode].systemPrompt,
          messages: [
            ...conversation,
            userMessage
          ],
        }),
      });

      if (!response.ok) {
        throw new Error(`API error: ${response.status}`);
      }

      const data = await response.json();
      const assistantMessage = {
        role: 'assistant',
        content: data.content[0].text
      };

      setConversation(prev => [...prev, assistantMessage]);
    } catch (error) {
      console.error('Error calling Claude API:', error);
      setConversation(prev => [...prev, {
        role: 'assistant',
        content: `Error: ${error.message}. Please try again.`
      }]);
    } finally {
      setLoading(false);
    }
  };

  const clearConversation = () => {
    setConversation([]);
  };

  const currentMode = AGENT_MODES[mode];
  const ModeIcon = currentMode.icon;

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 text-white">
      <div className="max-w-6xl mx-auto p-6">
        {/* Header */}
        <div className="text-center mb-8">
          <h1 className="text-4xl font-bold mb-2 bg-gradient-to-r from-blue-400 to-purple-400 bg-clip-text text-transparent">
            Development Mission Control
          </h1>
          <p className="text-slate-400">AI-powered development assistant across your stack</p>
        </div>

        {/* Mode Selector */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-6">
          {Object.entries(AGENT_MODES).map(([key, agentMode]) => {
            const Icon = agentMode.icon;
            const isActive = mode === key;
            return (
              <button
                key={key}
                onClick={() => {
                  setMode(key);
                  setConversation([]);
                }}
                className={`p-4 rounded-lg transition-all ${
                  isActive
                    ? `${agentMode.color} shadow-lg scale-105`
                    : 'bg-slate-700 hover:bg-slate-600'
                }`}
              >
                <Icon className="w-6 h-6 mx-auto mb-2" />
                <div className="text-sm font-semibold">{agentMode.name}</div>
              </button>
            );
          })}
        </div>

        {/* Current Mode Display */}
        <div className={`${currentMode.color} rounded-lg p-4 mb-6 flex items-center gap-3`}>
          <ModeIcon className="w-8 h-8" />
          <div>
            <h2 className="text-xl font-bold">{currentMode.name}</h2>
            <p className="text-sm opacity-90">Active Agent Mode</p>
          </div>
          {conversation.length > 0 && (
            <button
              onClick={clearConversation}
              className="ml-auto p-2 hover:bg-white/20 rounded-lg transition-colors"
              title="Clear conversation"
            >
              <Trash2 className="w-5 h-5" />
            </button>
          )}
        </div>

        {/* Conversation Display */}
        <div className="bg-slate-800 rounded-lg p-6 mb-6 min-h-[400px] max-h-[600px] overflow-y-auto">
          {conversation.length === 0 ? (
            <div className="text-center text-slate-400 py-20">
              <ModeIcon className="w-16 h-16 mx-auto mb-4 opacity-50" />
              <p className="text-lg">Ask me anything about {currentMode.name.toLowerCase()}...</p>
              <p className="text-sm mt-2">I'm here to help with your development tasks</p>
            </div>
          ) : (
            <div className="space-y-4">
              {conversation.map((msg, idx) => (
                <div
                  key={idx}
                  className={`p-4 rounded-lg ${
                    msg.role === 'user'
                      ? 'bg-blue-900/50 ml-12'
                      : 'bg-slate-700 mr-12'
                  }`}
                >
                  <div className="font-semibold mb-2 text-sm opacity-75">
                    {msg.role === 'user' ? 'You' : currentMode.name}
                  </div>
                  <div className="whitespace-pre-wrap font-mono text-sm">
                    {msg.content}
                  </div>
                </div>
              ))}
              {loading && (
                <div className="bg-slate-700 mr-12 p-4 rounded-lg flex items-center gap-3">
                  <Loader2 className="w-5 h-5 animate-spin" />
                  <span className="text-sm opacity-75">{currentMode.name} is thinking...</span>
                </div>
              )}
            </div>
          )}
        </div>

        {/* Input Form */}
        <form onSubmit={handleSubmit} className="relative">
          <textarea
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                handleSubmit(e);
              }
            }}
            placeholder={`Ask ${currentMode.name} for help... (Shift+Enter for new line)`}
            className="w-full bg-slate-700 text-white rounded-lg p-4 pr-12 focus:ring-2 focus:ring-blue-500 focus:outline-none resize-none"
            rows="3"
            disabled={loading}
          />
          <button
            type="submit"
            disabled={!input.trim() || loading}
            className={`absolute right-3 bottom-3 p-2 rounded-lg transition-all ${
              input.trim() && !loading
                ? 'bg-blue-500 hover:bg-blue-600 text-white'
                : 'bg-slate-600 text-slate-400 cursor-not-allowed'
            }`}
          >
            {loading ? (
              <Loader2 className="w-5 h-5 animate-spin" />
            ) : (
              <Send className="w-5 h-5" />
            )}
          </button>
        </form>

        {/* Tips */}
        <div className="mt-4 text-center text-sm text-slate-400">
          <p>💡 Tip: Each mode has specialized knowledge for its domain</p>
          <p className="mt-1">Switching modes will clear the conversation history</p>
        </div>
      </div>
    </div>
  );
}

---
name: ai-agent-architecture
description: |
  Design and implement production-grade AI agent systems using modern orchestration patterns.
  Use when building: multi-agent systems, autonomous workflows, agent orchestration, tool-using agents,
  human-in-the-loop workflows, or agent coordination patterns. Covers 2025 architectures including:
  Claude Agent SDK, LangGraph state machines, CrewAI role-based teams, Google ADK, OpenAI Agents SDK,
  AWS Strands, and MCP (Model Context Protocol) integrations. Patterns: supervisor/worker, swarm,
  hierarchical, sequential pipelines, parallel fan-out, event-driven agents, and ReAct loops.
  Technologies: Claude Agent SDK, LangGraph, CrewAI, AutoGen, MCP servers, tool orchestration.
---

# AI Agent Architecture Patterns

Build autonomous AI systems that reason, plan, execute tools, and collaborate.

## Core Agent Concepts

**AI Agent** = Autonomous system that perceives, reasons, acts, reflects, and learns.

| Aspect | Workflow | Agent |
|--------|----------|-------|
| Control | Predetermined | Goal-driven |
| Decisions | Conditional branches | LLM reasoning |
| Errors | Predefined fallbacks | Adaptive recovery |
| Use When | Predictable tasks | Ambiguous/complex |

---

## Part 1: Orchestration Patterns

### Pattern 1: Single Agent (ReAct Loop)

```python
from anthropic import Anthropic

class ReActAgent:
    """Single agent with observe-think-act loop."""
    
    def __init__(self, tools: List[dict], max_iterations: int = 10):
        self.client = Anthropic()
        self.tools = tools
        self.max_iterations = max_iterations
        
    def run(self, task: str) -> str:
        messages = [{"role": "user", "content": task}]
        
        for _ in range(self.max_iterations):
            response = self.client.messages.create(
                model="claude-sonnet-4-5-20250514",
                max_tokens=4096,
                tools=self.tools,
                messages=messages
            )
            
            if response.stop_reason == "end_turn":
                return self._extract_text(response)
            
            if response.stop_reason == "tool_use":
                tool_results = self._execute_tools(response)
                messages.append({"role": "assistant", "content": response.content})
                messages.append({"role": "user", "content": tool_results})
        
        return "Max iterations reached"
    
    def _execute_tools(self, response) -> List[dict]:
        results = []
        for block in response.content:
            if block.type == "tool_use":
                result = self._call_tool(block.name, block.input)
                results.append({
                    "type": "tool_result",
                    "tool_use_id": block.id,
                    "content": str(result)
                })
        return results
```

### Pattern 2: Supervisor-Worker (Hierarchical)

```python
from langgraph.graph import StateGraph, END
from typing import TypedDict, Literal

class SupervisorState(TypedDict):
    task: str
    current_worker: str
    worker_outputs: dict
    iteration: int

def supervisor(state: SupervisorState) -> SupervisorState:
    """Supervisor delegates to specialized workers."""
    prompt = f"""Task: {state['task']}
    Completed: {state['worker_outputs']}
    
    Workers: researcher, analyst, writer
    
    ASSIGN:<worker> or COMPLETE"""
    
    response = llm.invoke(prompt)
    
    if "COMPLETE" in response:
        return {"current_worker": "complete"}
    
    worker = response.split(":")[-1].strip().lower()
    return {"current_worker": worker, "iteration": state["iteration"] + 1}

def researcher(state: SupervisorState) -> SupervisorState:
    research = llm.invoke(f"Research: {state['task']}")
    outputs = state.get("worker_outputs", {})
    outputs["research"] = research
    return {"worker_outputs": outputs}

def analyst(state: SupervisorState) -> SupervisorState:
    analysis = llm.invoke(f"Analyze: {state['worker_outputs'].get('research')}")
    outputs = state.get("worker_outputs", {})
    outputs["analysis"] = analysis
    return {"worker_outputs": outputs}

def route_worker(state: SupervisorState) -> str:
    if state["current_worker"] == "complete" or state["iteration"] > 5:
        return "end"
    return state["current_worker"]

# Build graph
workflow = StateGraph(SupervisorState)
workflow.add_node("supervisor", supervisor)
workflow.add_node("researcher", researcher)
workflow.add_node("analyst", analyst)

workflow.set_entry_point("supervisor")
workflow.add_conditional_edges("supervisor", route_worker, {
    "researcher": "researcher", "analyst": "analyst", "end": END
})
workflow.add_edge("researcher", "supervisor")
workflow.add_edge("analyst", "supervisor")
```

### Pattern 3: Parallel Fan-Out

```python
import asyncio

class ParallelAgentSystem:
    """Fan-out to multiple agents, aggregate results."""
    
    def __init__(self, agents: dict):
        self.agents = agents
        
    async def run(self, task: str) -> dict:
        # Decompose
        subtasks = await self._decompose(task)
        
        # Parallel execution
        async def run_agent(name: str, subtask: str):
            return (name, await self.agents[name].arun(subtask))
        
        results = await asyncio.gather(*[
            run_agent(name, subtask)
            for name, subtask in subtasks.items()
        ])
        
        # Aggregate
        return await self._aggregate(task, dict(results))
```

### Pattern 4: Sequential Pipeline

```python
class SequentialPipeline:
    """Agents execute in sequence, passing context."""
    
    def __init__(self, stages: List[tuple]):
        self.stages = stages  # [("name", agent), ...]
        
    def run(self, input: str) -> dict:
        context = {"input": input, "stages": {}}
        
        for name, agent in self.stages:
            prompt = f"""Stage: {name}
            Input: {context['input']}
            Previous: {context['stages']}"""
            
            context["stages"][name] = agent.run(prompt)
        
        return context
```

### Pattern 5: Event-Driven Agents

```python
from dataclasses import dataclass
from collections import defaultdict

@dataclass
class AgentEvent:
    type: str
    payload: dict
    source: str

class EventBus:
    def __init__(self):
        self.subscribers = defaultdict(list)
        self.queue = asyncio.Queue()
        
    def subscribe(self, event_type: str, handler):
        self.subscribers[event_type].append(handler)
        
    async def publish(self, event: AgentEvent):
        await self.queue.put(event)
        
    async def process(self):
        while True:
            event = await self.queue.get()
            for handler in self.subscribers.get(event.type, []):
                asyncio.create_task(handler(event))
```

---

## Part 2: Claude Agent SDK

### Basic Agent

```python
from claude_agent_sdk import ClaudeAgentOptions, query

async def agent(task: str) -> str:
    options = ClaudeAgentOptions(
        system_prompt="Helpful assistant with computer tools.",
        allowed_tools=["Bash", "Read", "Write", "Grep"],
        permission_mode="acceptEdits",
        max_turns=10
    )
    
    result = []
    async for msg in query(prompt=task, options=options):
        if hasattr(msg, 'content'):
            for block in msg.content:
                if block.type == "text":
                    result.append(block.text)
    return "\n".join(result)
```

### Agent with Hooks

```python
from claude_agent_sdk import ClaudeAgentOptions, ClaudeSDKClient, HookMatcher

async def secure_agent(task: str):
    async def validate_bash(input_data, tool_use_id, context):
        if input_data["tool_name"] != "Bash":
            return {}
        
        cmd = input_data["tool_input"].get("command", "")
        blocked = ["rm -rf", "sudo", "curl | sh"]
        
        for pattern in blocked:
            if pattern in cmd:
                return {"hookSpecificOutput": {
                    "hookEventName": "PreToolUse",
                    "permissionDecision": "deny",
                    "permissionDecisionReason": f"Blocked: {pattern}"
                }}
        return {}
    
    options = ClaudeAgentOptions(
        allowed_tools=["Bash", "Read", "Write"],
        hooks={"PreToolUse": [HookMatcher(matcher="Bash", hooks=[validate_bash])]}
    )
    
    async with ClaudeSDKClient(options=options) as client:
        await client.query(task)
        async for msg in client.receive_response():
            yield msg
```

---

## Part 3: MCP Integration

### MCP Server

```python
from mcp.server import Server
from mcp.types import Tool

server = Server("custom-tools")

@server.tool()
async def search_database(query: str) -> str:
    """Search internal database."""
    return json.dumps(await db.search(query))

@server.tool()
async def create_ticket(title: str, description: str) -> str:
    """Create JIRA ticket."""
    ticket = await jira.create_issue(title=title, description=description)
    return f"Created: {ticket.key}"
```

### Agent with MCP

```python
class MCPAgent:
    def __init__(self, mcp_servers: List[str]):
        self.client = Anthropic()
        self.tools = []
        for server in mcp_servers:
            self.tools.extend(self._load_tools(server))
    
    def run(self, task: str) -> str:
        messages = [{"role": "user", "content": task}]
        
        while True:
            response = self.client.messages.create(
                model="claude-sonnet-4-5-20250514",
                tools=self.tools,
                messages=messages
            )
            
            if response.stop_reason == "end_turn":
                return self._extract_text(response)
            
            results = [self._call_mcp(tc) for tc in self._get_tool_calls(response)]
            messages.extend([
                {"role": "assistant", "content": response.content},
                {"role": "user", "content": results}
            ])
```

---

## Part 4: CrewAI Teams

```python
from crewai import Agent, Task, Crew, Process

researcher = Agent(
    role="Research Analyst",
    goal="Find cutting-edge developments",
    backstory="Seasoned researcher, 20 years experience.",
    tools=[search_tool]
)

analyst = Agent(
    role="Data Analyst", 
    goal="Extract actionable insights",
    tools=[analysis_tool]
)

research_task = Task(
    description="Research: {topic}",
    agent=researcher
)

analysis_task = Task(
    description="Analyze findings",
    agent=analyst,
    context=[research_task]
)

crew = Crew(
    agents=[researcher, analyst],
    tasks=[research_task, analysis_task],
    process=Process.sequential
)

result = crew.kickoff(inputs={"topic": "AI agents"})
```

---

## Part 5: Human-in-the-Loop

### Approval Gates

```python
class ApprovalAgent:
    def __init__(self, sensitive_tools: List[str]):
        self.sensitive_tools = sensitive_tools
        
    async def run(self, task: str):
        while True:
            response = await self._llm_response(task)
            
            if response.stop_reason == "end_turn":
                return self._extract_text(response)
            
            for tool_call in self._get_tool_calls(response):
                if tool_call.name in self.sensitive_tools:
                    approved = await self._request_approval(tool_call)
                    if not approved:
                        return "Cancelled: approval denied"
                
                await self._execute_tool(tool_call)
```

---

## Part 6: Memory Management

```python
class AgentMemory:
    def __init__(self, max_history: int = 50):
        self.short_term = []  # Recent messages
        self.long_term = {}   # Persistent facts
        self.episodic = []    # Key events
        
    def add_interaction(self, role: str, content: str):
        self.short_term.append({"role": role, "content": content})
        if len(self.short_term) > self.max_history:
            self._summarize_and_trim()
    
    def add_fact(self, key: str, value: str):
        self.long_term[key] = {"value": value, "timestamp": time.time()}
    
    def get_context(self) -> str:
        facts = "\n".join([f"- {k}: {v['value']}" for k, v in self.long_term.items()])
        return f"Known facts:\n{facts}"
```

---

## Decision Framework

```
Task type?
├─ Single domain → Single Agent (ReAct)
├─ Multiple domains → Supervisor-Worker
├─ Strict sequence → Sequential Pipeline
├─ Real-time → Event-Driven
└─ Complex research → Multi-Agent + Memory

Control needs?
├─ High autonomy → Full agent loops
├─ Oversight needed → Human-in-the-loop
├─ Safety critical → Approval gates
└─ Audit required → Event-driven + logging

Scale?
├─ Low latency → Single agent
├─ High throughput → Parallel fan-out
├─ Complex reasoning → Hierarchical
```

---

## References

See `references/` for detailed guides:
- `references/langgraph-advanced.md` - LangGraph patterns
- `references/mcp-servers.md` - MCP implementation
- `references/memory-patterns.md` - Memory strategies

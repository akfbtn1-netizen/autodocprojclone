# LangGraph Advanced Patterns

Production patterns for complex agent orchestration.

---

## State Management

### Typed State with Reducers

```python
from langgraph.graph import StateGraph
from typing import TypedDict, Annotated, List
from operator import add

class AgentState(TypedDict):
    messages: Annotated[List[dict], add]  # Append reducer
    context: dict
    iteration: int

def node_a(state: AgentState) -> AgentState:
    return {
        "messages": [{"role": "assistant", "content": "From node A"}],
        "iteration": state["iteration"] + 1
    }
```

### Checkpointing for Long Tasks

```python
from langgraph.checkpoint.sqlite import SqliteSaver

# Create checkpointer
checkpointer = SqliteSaver.from_conn_string("checkpoints.db")

# Compile with checkpointing
chain = workflow.compile(checkpointer=checkpointer)

# Run with thread_id for resumability
config = {"configurable": {"thread_id": "task-123"}}
result = chain.invoke(state, config)

# Resume interrupted task
result = chain.invoke(None, config)  # Continues from checkpoint
```

---

## Conditional Routing

### Multi-Path Routing

```python
def route_by_intent(state: AgentState) -> str:
    """Route to different subgraphs based on intent."""
    intent = classify_intent(state["messages"][-1]["content"])
    
    routes = {
        "question": "qa_subgraph",
        "task": "task_subgraph",
        "chat": "conversation_subgraph"
    }
    return routes.get(intent, "default_subgraph")

workflow.add_conditional_edges(
    "classifier",
    route_by_intent,
    {
        "qa_subgraph": "qa_agent",
        "task_subgraph": "task_agent",
        "conversation_subgraph": "chat_agent",
        "default_subgraph": "fallback_agent"
    }
)
```

### Loop with Exit Condition

```python
def should_continue(state: AgentState) -> str:
    # Check termination conditions
    if state["iteration"] >= 10:
        return "end"
    if state.get("task_complete"):
        return "end"
    if state.get("error"):
        return "error_handler"
    return "continue"

workflow.add_conditional_edges(
    "worker",
    should_continue,
    {
        "continue": "worker",
        "end": END,
        "error_handler": "error_node"
    }
)
```

---

## Subgraph Composition

### Nested Graphs

```python
# Create subgraph
qa_subgraph = StateGraph(QAState)
qa_subgraph.add_node("retrieve", retrieve_docs)
qa_subgraph.add_node("generate", generate_answer)
qa_subgraph.set_entry_point("retrieve")
qa_subgraph.add_edge("retrieve", "generate")
qa_subgraph.add_edge("generate", END)
qa_compiled = qa_subgraph.compile()

# Use in parent graph
parent = StateGraph(AgentState)
parent.add_node("qa", qa_compiled)  # Embed as node
parent.add_node("task", task_subgraph.compile())
```

---

## Human-in-the-Loop

### Interrupt and Resume

```python
from langgraph.graph import StateGraph

workflow = StateGraph(AgentState)

def tool_executor(state: AgentState) -> AgentState:
    """Execute tool, interrupt if sensitive."""
    tool_call = state["pending_tool"]
    
    if tool_call["name"] in SENSITIVE_TOOLS:
        # Interrupt for human approval
        return {"waiting_for_approval": True, "pending_tool": tool_call}
    
    result = execute_tool(tool_call)
    return {"tool_result": result, "waiting_for_approval": False}

# Add interrupt before sensitive operations
workflow.add_node("tool_executor", tool_executor)

# In runtime
chain = workflow.compile(checkpointer=checkpointer, interrupt_before=["tool_executor"])

# Will pause before tool_executor if condition met
result = chain.invoke(state, config)

if result.get("waiting_for_approval"):
    # Get human decision
    if human_approves():
        chain.update_state(config, {"approved": True})
        result = chain.invoke(None, config)  # Resume
```

---

## Parallel Execution

### Fan-Out Pattern

```python
from langgraph.graph import StateGraph
from typing import TypedDict, List

class ParallelState(TypedDict):
    query: str
    branch_results: List[dict]

def fan_out(state: ParallelState) -> List[dict]:
    """Create parallel branches."""
    return [
        {"branch": "research", "query": state["query"]},
        {"branch": "analysis", "query": state["query"]},
        {"branch": "synthesis", "query": state["query"]}
    ]

def fan_in(state: ParallelState) -> ParallelState:
    """Aggregate parallel results."""
    combined = "\n".join([r["result"] for r in state["branch_results"]])
    return {"final_result": combined}

# Using map-reduce pattern
workflow.add_node("fan_out", fan_out)
workflow.add_node("branch_worker", branch_worker)
workflow.add_node("fan_in", fan_in)

workflow.add_edge("fan_out", "branch_worker")  # Parallel
workflow.add_edge("branch_worker", "fan_in")
```

---

## Error Handling

### Graceful Degradation

```python
def safe_node(state: AgentState) -> AgentState:
    """Node with error handling."""
    try:
        result = risky_operation(state)
        return {"result": result, "error": None}
    except RateLimitError:
        return {"error": "rate_limit", "retry_after": 60}
    except TimeoutError:
        return {"error": "timeout", "fallback_result": get_cached_result()}
    except Exception as e:
        return {"error": str(e), "fallback_result": None}

def error_router(state: AgentState) -> str:
    """Route based on error type."""
    error = state.get("error")
    if not error:
        return "continue"
    if error == "rate_limit":
        return "wait_and_retry"
    if error == "timeout":
        return "use_fallback"
    return "fail_gracefully"
```

---

## Debugging

### Trace Visualization

```python
# Enable detailed tracing
from langgraph.pregel import debug

with debug.trace():
    result = chain.invoke(state)

# Stream with step details
for step in chain.stream(state, stream_mode="updates"):
    print(f"Node: {list(step.keys())[0]}")
    print(f"Output: {step}")

# Visualize graph
print(chain.get_graph().draw_mermaid())
```

---

## Production Patterns

### Retry with Backoff

```python
import asyncio
from tenacity import retry, stop_after_attempt, wait_exponential

@retry(stop=stop_after_attempt(3), wait=wait_exponential(multiplier=1, max=10))
async def resilient_node(state: AgentState) -> AgentState:
    """Node with automatic retry."""
    return await llm_call(state)
```

### Rate Limiting

```python
from asyncio import Semaphore

class RateLimitedGraph:
    def __init__(self, workflow, max_concurrent: int = 5):
        self.chain = workflow.compile()
        self.semaphore = Semaphore(max_concurrent)
    
    async def invoke(self, state):
        async with self.semaphore:
            return await self.chain.ainvoke(state)
```

### Metrics Collection

```python
import time
from dataclasses import dataclass

@dataclass
class NodeMetrics:
    node_name: str
    execution_time: float
    tokens_used: int
    success: bool

class MetricsCollector:
    def __init__(self):
        self.metrics = []
    
    def wrap_node(self, node_fn, node_name: str):
        async def wrapped(state):
            start = time.time()
            try:
                result = await node_fn(state)
                self.metrics.append(NodeMetrics(
                    node_name=node_name,
                    execution_time=time.time() - start,
                    tokens_used=result.get("tokens", 0),
                    success=True
                ))
                return result
            except Exception as e:
                self.metrics.append(NodeMetrics(
                    node_name=node_name,
                    execution_time=time.time() - start,
                    tokens_used=0,
                    success=False
                ))
                raise
        return wrapped
```

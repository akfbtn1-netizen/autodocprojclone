# LangGraph Agentic RAG Patterns

Production patterns for building agentic RAG systems with LangGraph.

---

## Pattern 1: Corrective RAG with Fallback

Complete implementation of CRAG pattern with web search fallback.

```python
from langgraph.graph import StateGraph, END
from typing import TypedDict, List, Literal
from langchain_core.documents import Document

class CRAGState(TypedDict):
    query: str
    documents: List[Document]
    web_search_needed: bool
    generation: str
    
def retrieve(state: CRAGState) -> CRAGState:
    """Initial vector store retrieval."""
    docs = vector_store.similarity_search(state["query"], k=4)
    return {"documents": docs}

def grade_documents(state: CRAGState) -> CRAGState:
    """Grade each document for relevance."""
    query = state["query"]
    docs = state["documents"]
    
    filtered_docs = []
    web_needed = False
    
    for doc in docs:
        grade = llm.invoke(f"""Grade this document's relevance to the query.
        Query: {query}
        Document: {doc.page_content[:500]}
        
        Return ONLY 'yes' or 'no'.""").strip().lower()
        
        if grade == "yes":
            filtered_docs.append(doc)
    
    # If less than 2 relevant docs, need web search
    if len(filtered_docs) < 2:
        web_needed = True
    
    return {"documents": filtered_docs, "web_search_needed": web_needed}

def web_search(state: CRAGState) -> CRAGState:
    """Fallback to web search."""
    results = tavily_client.search(state["query"], max_results=3)
    web_docs = [Document(page_content=r["content"], metadata={"source": r["url"]}) 
                for r in results["results"]]
    
    # Combine with any relevant docs from vector store
    all_docs = state["documents"] + web_docs
    return {"documents": all_docs}

def generate(state: CRAGState) -> CRAGState:
    """Generate answer from filtered documents."""
    context = "\n\n".join([d.page_content for d in state["documents"]])
    
    response = llm.invoke(f"""Answer the question based on the following context.
    
    Context: {context}
    
    Question: {state["query"]}
    
    Answer:""")
    
    return {"generation": response}

def decide_to_generate(state: CRAGState) -> Literal["web_search", "generate"]:
    """Route based on document quality."""
    if state["web_search_needed"]:
        return "web_search"
    return "generate"

# Build the graph
workflow = StateGraph(CRAGState)

workflow.add_node("retrieve", retrieve)
workflow.add_node("grade_documents", grade_documents)
workflow.add_node("web_search", web_search)
workflow.add_node("generate", generate)

workflow.set_entry_point("retrieve")
workflow.add_edge("retrieve", "grade_documents")
workflow.add_conditional_edges(
    "grade_documents",
    decide_to_generate,
    {"web_search": "web_search", "generate": "generate"}
)
workflow.add_edge("web_search", "generate")
workflow.add_edge("generate", END)

crag_chain = workflow.compile()

# Usage
result = crag_chain.invoke({"query": "What are the latest developments in quantum computing?"})
```

---

## Pattern 2: Self-RAG with Reflection

Agent evaluates its own retrieval and generation quality.

```python
class SelfRAGState(TypedDict):
    query: str
    documents: List[Document]
    generation: str
    reflection: str
    iteration: int
    is_satisfactory: bool

def retrieve_and_generate(state: SelfRAGState) -> SelfRAGState:
    """Combined retrieval and generation step."""
    # Retrieve
    if state.get("reflection"):
        # Use reflection to improve query
        improved_query = llm.invoke(
            f"Improve this query based on feedback:\nQuery: {state['query']}\nFeedback: {state['reflection']}"
        )
        docs = vector_store.similarity_search(improved_query, k=5)
    else:
        docs = vector_store.similarity_search(state["query"], k=5)
    
    # Generate
    context = "\n".join([d.page_content for d in docs])
    generation = llm.invoke(
        f"Answer based on context:\n{context}\n\nQuestion: {state['query']}"
    )
    
    return {
        "documents": docs,
        "generation": generation,
        "iteration": state.get("iteration", 0) + 1
    }

def reflect(state: SelfRAGState) -> SelfRAGState:
    """Self-critique the response."""
    reflection_prompt = f"""Critically evaluate this response:
    
    Question: {state['query']}
    Response: {state['generation']}
    
    Evaluate:
    1. Is the response fully supported by retrievable facts? (Yes/Partially/No)
    2. Does it completely answer the question? (Yes/Partially/No)
    3. Is there any hallucination or unsupported claims? (Yes/No)
    
    If any answer is not 'Yes', provide specific feedback for improvement.
    If all are 'Yes', respond with 'SATISFACTORY'.
    """
    
    reflection = llm.invoke(reflection_prompt)
    is_satisfactory = "SATISFACTORY" in reflection.upper()
    
    return {"reflection": reflection, "is_satisfactory": is_satisfactory}

def should_continue(state: SelfRAGState) -> Literal["continue", "end"]:
    """Decide whether to iterate or finish."""
    if state["is_satisfactory"] or state["iteration"] >= 3:
        return "end"
    return "continue"

# Build graph
workflow = StateGraph(SelfRAGState)
workflow.add_node("retrieve_generate", retrieve_and_generate)
workflow.add_node("reflect", reflect)

workflow.set_entry_point("retrieve_generate")
workflow.add_edge("retrieve_generate", "reflect")
workflow.add_conditional_edges(
    "reflect",
    should_continue,
    {"continue": "retrieve_generate", "end": END}
)

self_rag_chain = workflow.compile()
```

---

## Pattern 3: Multi-Agent Research System

Coordinator + specialist agents for complex research queries.

```python
from langgraph.graph import StateGraph, END
from typing import TypedDict, List, Dict, Any

class ResearchState(TypedDict):
    query: str
    research_plan: List[str]
    agent_results: Dict[str, str]
    synthesis: str
    sources: List[str]

def coordinator(state: ResearchState) -> ResearchState:
    """Coordinator agent: creates research plan."""
    plan_prompt = f"""You are a research coordinator. Break this query into specific research tasks.
    
    Query: {state['query']}
    
    Create 2-4 specific research questions that different specialists should investigate.
    Return as JSON list of strings."""
    
    plan = json.loads(llm.invoke(plan_prompt))
    return {"research_plan": plan}

def research_agent(state: ResearchState, task: str, agent_type: str) -> str:
    """Generic research agent that can specialize."""
    # Retrieve relevant documents
    docs = vector_store.similarity_search(task, k=3)
    
    agent_prompt = f"""You are a {agent_type} research specialist.
    
    Research task: {task}
    Available information: {docs}
    
    Provide a focused, factual response addressing the specific task.
    Cite sources when possible."""
    
    return llm.invoke(agent_prompt)

def parallel_research(state: ResearchState) -> ResearchState:
    """Execute research agents in parallel."""
    import asyncio
    
    async def run_all_agents():
        tasks = [
            research_agent(state, task, determine_agent_type(task))
            for task in state["research_plan"]
        ]
        results = await asyncio.gather(*tasks)
        return dict(zip(state["research_plan"], results))
    
    agent_results = asyncio.run(run_all_agents())
    return {"agent_results": agent_results}

def synthesizer(state: ResearchState) -> ResearchState:
    """Synthesize all agent findings into coherent response."""
    findings = "\n\n".join([
        f"### {task}\n{result}" 
        for task, result in state["agent_results"].items()
    ])
    
    synthesis_prompt = f"""Synthesize these research findings into a comprehensive answer.
    
    Original question: {state['query']}
    
    Research findings:
    {findings}
    
    Provide a coherent, well-structured response that addresses the original question.
    Include key insights from all research areas."""
    
    synthesis = llm.invoke(synthesis_prompt)
    
    # Extract sources
    sources = extract_sources_from_results(state["agent_results"])
    
    return {"synthesis": synthesis, "sources": sources}

# Build multi-agent graph
workflow = StateGraph(ResearchState)
workflow.add_node("coordinator", coordinator)
workflow.add_node("research", parallel_research)
workflow.add_node("synthesize", synthesizer)

workflow.set_entry_point("coordinator")
workflow.add_edge("coordinator", "research")
workflow.add_edge("research", "synthesize")
workflow.add_edge("synthesize", END)

research_system = workflow.compile()
```

---

## Pattern 4: Adaptive RAG Router

Intelligently route queries to different retrieval strategies.

```python
class AdaptiveRAGState(TypedDict):
    query: str
    query_type: str
    complexity: str
    documents: List[Document]
    generation: str

def analyze_query(state: AdaptiveRAGState) -> AdaptiveRAGState:
    """Analyze query to determine optimal strategy."""
    analysis = llm.invoke(f"""Analyze this query:
    
    Query: {state['query']}
    
    Return JSON with:
    - type: "factual" | "analytical" | "comparative" | "procedural"
    - complexity: "simple" | "moderate" | "complex"
    - keywords: list of key terms
    - needs_current_info: boolean
    """)
    
    parsed = json.loads(analysis)
    return {
        "query_type": parsed["type"],
        "complexity": parsed["complexity"]
    }

def simple_retrieve(state: AdaptiveRAGState) -> AdaptiveRAGState:
    """Simple single-pass retrieval for straightforward queries."""
    docs = vector_store.similarity_search(state["query"], k=3)
    return {"documents": docs}

def multi_hop_retrieve(state: AdaptiveRAGState) -> AdaptiveRAGState:
    """Multi-hop retrieval for complex queries."""
    all_docs = []
    current_query = state["query"]
    
    for hop in range(3):  # Max 3 hops
        docs = vector_store.similarity_search(current_query, k=2)
        all_docs.extend(docs)
        
        # Generate follow-up query based on findings
        if hop < 2:
            follow_up = llm.invoke(
                f"Based on: {docs}\nWhat else should we search to answer: {state['query']}?"
            )
            if "COMPLETE" in follow_up:
                break
            current_query = follow_up
    
    return {"documents": all_docs}

def comparative_retrieve(state: AdaptiveRAGState) -> AdaptiveRAGState:
    """Parallel retrieval for comparison queries."""
    # Extract comparison subjects
    subjects = llm.invoke(
        f"Extract the items being compared in: {state['query']}\nReturn as JSON list."
    )
    subjects = json.loads(subjects)
    
    all_docs = []
    for subject in subjects:
        docs = vector_store.similarity_search(f"{subject} {state['query']}", k=2)
        all_docs.extend(docs)
    
    return {"documents": all_docs}

def route_by_complexity(state: AdaptiveRAGState) -> str:
    """Route to appropriate retriever based on analysis."""
    if state["complexity"] == "simple":
        return "simple"
    elif state["query_type"] == "comparative":
        return "comparative"
    else:
        return "multi_hop"

# Build adaptive router
workflow = StateGraph(AdaptiveRAGState)
workflow.add_node("analyze", analyze_query)
workflow.add_node("simple", simple_retrieve)
workflow.add_node("multi_hop", multi_hop_retrieve)
workflow.add_node("comparative", comparative_retrieve)
workflow.add_node("generate", generate)

workflow.set_entry_point("analyze")
workflow.add_conditional_edges(
    "analyze",
    route_by_complexity,
    {"simple": "simple", "multi_hop": "multi_hop", "comparative": "comparative"}
)
for node in ["simple", "multi_hop", "comparative"]:
    workflow.add_edge(node, "generate")
workflow.add_edge("generate", END)

adaptive_rag = workflow.compile()
```

---

## Pattern 5: Checkpointed Long-Running Research

For research tasks that may take minutes/hours with checkpoint support.

```python
from langgraph.checkpoint.sqlite import SqliteSaver

class LongResearchState(TypedDict):
    query: str
    research_phases: List[str]
    current_phase: int
    accumulated_findings: Dict[str, str]
    final_report: str

def plan_research(state: LongResearchState) -> LongResearchState:
    """Create detailed research plan with phases."""
    plan = llm.invoke(f"""Create a detailed research plan for:
    {state['query']}
    
    Break into 3-5 sequential phases, each building on previous findings.
    Return as JSON list of phase descriptions.""")
    
    return {"research_phases": json.loads(plan), "current_phase": 0}

def execute_phase(state: LongResearchState) -> LongResearchState:
    """Execute current research phase."""
    phase = state["research_phases"][state["current_phase"]]
    previous_findings = state.get("accumulated_findings", {})
    
    # Include previous findings as context
    context = "\n".join([f"{k}: {v}" for k, v in previous_findings.items()])
    
    # Deep research for this phase
    docs = vector_store.similarity_search(phase, k=5)
    
    finding = llm.invoke(f"""Research phase: {phase}
    Previous findings: {context}
    Retrieved information: {docs}
    
    Provide detailed findings for this phase.""")
    
    accumulated = state.get("accumulated_findings", {})
    accumulated[phase] = finding
    
    return {
        "accumulated_findings": accumulated,
        "current_phase": state["current_phase"] + 1
    }

def should_continue_research(state: LongResearchState) -> str:
    if state["current_phase"] < len(state["research_phases"]):
        return "continue"
    return "compile"

def compile_report(state: LongResearchState) -> LongResearchState:
    """Compile all findings into final report."""
    all_findings = "\n\n".join([
        f"## {phase}\n{finding}"
        for phase, finding in state["accumulated_findings"].items()
    ])
    
    report = llm.invoke(f"""Compile these research findings into a comprehensive report:
    
    Original question: {state['query']}
    
    Findings:
    {all_findings}
    
    Create a well-structured report with executive summary, key findings, and conclusions.""")
    
    return {"final_report": report}

# Build with checkpointing for long-running tasks
workflow = StateGraph(LongResearchState)
workflow.add_node("plan", plan_research)
workflow.add_node("execute", execute_phase)
workflow.add_node("compile", compile_report)

workflow.set_entry_point("plan")
workflow.add_edge("plan", "execute")
workflow.add_conditional_edges(
    "execute",
    should_continue_research,
    {"continue": "execute", "compile": "compile"}
)
workflow.add_edge("compile", END)

# Add SQLite checkpointer for persistence
checkpointer = SqliteSaver.from_conn_string("research_checkpoints.db")
research_chain = workflow.compile(checkpointer=checkpointer)

# Usage with thread_id for resumability
config = {"configurable": {"thread_id": "research-123"}}
result = research_chain.invoke({"query": "Comprehensive analysis of AI regulation trends"}, config)
```

---

## Debugging LangGraph Workflows

```python
from langgraph.pregel import debug

# Enable step-by-step debugging
with debug.trace():
    result = workflow.invoke({"query": "test"})
    
# Print state at each node
for step in workflow.stream({"query": "test"}):
    print(f"Node: {step.keys()}")
    print(f"State: {step}")
    print("---")

# Visualize the graph
workflow.get_graph().draw_mermaid()
```

---
name: agentic-rag-implementation
description: |
  Build production-grade Agentic RAG systems that go beyond static retrieval with autonomous 
  AI agents, dynamic workflows, and iterative refinement. Use when implementing RAG systems 
  that require: multi-step reasoning, adaptive retrieval strategies, query routing, self-correction 
  loops, multi-agent collaboration, tool use during retrieval, or hybrid search combining semantic
  and keyword approaches. Covers 2025 patterns including: Self-RAG, CRAG (Corrective RAG), 
  deep research agents, ReAct-style retrieval, query decomposition, hierarchical retrieval, 
  and LLM-as-Judge evaluation. Technologies: LangGraph, LlamaIndex, CrewAI, Claude Agent SDK, 
  Qdrant, Pinecone, ColBERT, BM25 hybrid search.
---

# Agentic RAG Implementation

Build autonomous retrieval systems that reason, adapt, and iteratively refine their search strategies.

## Core Concept: From Static to Agentic RAG

**Traditional RAG** follows a linear pipeline: Query → Embed → Retrieve → Generate. It assumes one retrieval pass suffices.

**Agentic RAG** embeds autonomous agents into the pipeline with capabilities for:
- **Reflection**: Evaluate retrieval quality, decide if more context needed
- **Planning**: Decompose complex queries into sub-queries
- **Tool Use**: Invoke external APIs, databases, web search dynamically
- **Multi-Agent Collaboration**: Specialized agents for retrieval, reasoning, synthesis

---

## Part 1: Agentic RAG Architectures

### Architecture 1: Single Agent RAG (Entry Point)

```python
from langgraph.graph import StateGraph, END
from typing import TypedDict, List, Annotated

class RAGState(TypedDict):
    query: str
    documents: List[str]
    answer: str
    retrieval_attempts: int
    needs_more_context: bool

def retrieve(state: RAGState) -> RAGState:
    """Retrieve documents based on current query."""
    docs = vector_store.similarity_search(state["query"], k=5)
    return {"documents": [d.page_content for d in docs], "retrieval_attempts": state["retrieval_attempts"] + 1}

def grade_documents(state: RAGState) -> RAGState:
    """Grade retrieved documents for relevance."""
    prompt = f"""Grade these documents for relevance to: {state['query']}
    Documents: {state['documents']}
    Return 'relevant' or 'not_relevant'"""
    grade = llm.invoke(prompt)
    return {"needs_more_context": "not_relevant" in grade.lower()}

def generate(state: RAGState) -> RAGState:
    """Generate answer from retrieved context."""
    context = "\n".join(state["documents"])
    answer = llm.invoke(f"Answer based on context:\n{context}\n\nQuestion: {state['query']}")
    return {"answer": answer}

def should_continue(state: RAGState) -> str:
    """Decide whether to retrieve more or generate."""
    if state["needs_more_context"] and state["retrieval_attempts"] < 3:
        return "retrieve"
    return "generate"

# Build graph
workflow = StateGraph(RAGState)
workflow.add_node("retrieve", retrieve)
workflow.add_node("grade", grade_documents)
workflow.add_node("generate", generate)

workflow.set_entry_point("retrieve")
workflow.add_edge("retrieve", "grade")
workflow.add_conditional_edges("grade", should_continue, {"retrieve": "retrieve", "generate": "generate"})
workflow.add_edge("generate", END)

agent = workflow.compile()
```

### Architecture 2: Router Agent (Multi-Source)

Route queries to appropriate retrieval sources based on intent.

```python
class RouterState(TypedDict):
    query: str
    route: str
    documents: List[str]
    answer: str

def route_query(state: RouterState) -> RouterState:
    """Determine optimal retrieval source."""
    prompt = f"""Classify this query into one category:
    - 'vectordb': General knowledge questions
    - 'sql': Structured data questions (numbers, dates, aggregations)
    - 'web': Current events, real-time information
    - 'code': Programming, technical documentation
    
    Query: {state['query']}
    Category:"""
    route = llm.invoke(prompt).strip().lower()
    return {"route": route}

def retrieve_vectordb(state: RouterState) -> RouterState:
    docs = vector_store.search(state["query"], k=5)
    return {"documents": docs}

def retrieve_sql(state: RouterState) -> RouterState:
    # Generate and execute SQL
    sql = text_to_sql(state["query"])
    results = db.execute(sql)
    return {"documents": [str(results)]}

def retrieve_web(state: RouterState) -> RouterState:
    results = web_search(state["query"])
    return {"documents": results}

def route_to_retriever(state: RouterState) -> str:
    return state["route"]

# Build router graph
workflow = StateGraph(RouterState)
workflow.add_node("router", route_query)
workflow.add_node("vectordb", retrieve_vectordb)
workflow.add_node("sql", retrieve_sql)
workflow.add_node("web", retrieve_web)
workflow.add_node("generate", generate)

workflow.set_entry_point("router")
workflow.add_conditional_edges("router", route_to_retriever, {
    "vectordb": "vectordb",
    "sql": "sql", 
    "web": "web"
})
# All retrievers lead to generate
for node in ["vectordb", "sql", "web"]:
    workflow.add_edge(node, "generate")
workflow.add_edge("generate", END)
```

### Architecture 3: Multi-Agent Collaborative RAG

Specialized agents collaborate on complex queries.

```python
from typing import Literal

class MultiAgentState(TypedDict):
    query: str
    decomposed_queries: List[str]
    partial_answers: dict
    synthesized_answer: str
    
def decompose_query(state: MultiAgentState) -> MultiAgentState:
    """Break complex query into sub-questions."""
    prompt = f"""Decompose this complex question into 2-4 simpler sub-questions:
    Question: {state['query']}
    Return as JSON list."""
    sub_queries = json.loads(llm.invoke(prompt))
    return {"decomposed_queries": sub_queries}

def research_agent(state: MultiAgentState, query: str) -> str:
    """Individual research agent for sub-query."""
    docs = vector_store.search(query, k=3)
    answer = llm.invoke(f"Answer this specific question: {query}\nContext: {docs}")
    return answer

def parallel_research(state: MultiAgentState) -> MultiAgentState:
    """Execute research agents in parallel."""
    import asyncio
    
    async def research_all():
        tasks = [research_agent(state, q) for q in state["decomposed_queries"]]
        return await asyncio.gather(*tasks)
    
    answers = asyncio.run(research_all())
    return {"partial_answers": dict(zip(state["decomposed_queries"], answers))}

def synthesize(state: MultiAgentState) -> MultiAgentState:
    """Synthesize partial answers into coherent response."""
    context = "\n".join([f"Q: {q}\nA: {a}" for q, a in state["partial_answers"].items()])
    final = llm.invoke(f"""Synthesize these findings into a comprehensive answer:
    Original question: {state['query']}
    Research findings:\n{context}""")
    return {"synthesized_answer": final}
```

---

## Part 2: Advanced Retrieval Patterns

### Self-RAG: Self-Reflective Retrieval

```python
def self_rag_retrieve(query: str, max_iterations: int = 3) -> str:
    """
    Self-RAG pattern: Retrieve, generate, critique, refine.
    Based on arXiv:2310.11511
    """
    for iteration in range(max_iterations):
        # Retrieve
        docs = retriever.get_relevant_documents(query)
        
        # Generate with retrieval tokens
        response = llm.invoke(f"""
        [Retrieval needed: Yes]
        Context: {docs}
        Question: {query}
        
        Generate answer and self-critique:
        - Is retrieval relevant? [Yes/No]
        - Is response supported? [Fully/Partially/No]
        - Is response useful? [5-1 scale]
        """)
        
        # Parse self-critique
        if "[Fully]" in response and "[Yes]" in response:
            return extract_answer(response)
        
        # Refine query based on critique
        query = refine_query(query, response)
    
    return extract_answer(response)
```

### Corrective RAG (CRAG)

```python
def corrective_rag(query: str) -> str:
    """
    CRAG: Grade documents and take corrective action.
    Based on arXiv:2401.15884
    """
    docs = retriever.get_relevant_documents(query)
    
    # Grade each document
    grades = []
    for doc in docs:
        grade = llm.invoke(f"""
        Grade document relevance to query.
        Query: {query}
        Document: {doc}
        Grade: [Correct/Incorrect/Ambiguous]""")
        grades.append((doc, grade))
    
    correct_docs = [d for d, g in grades if "Correct" in g]
    
    # Corrective actions based on grades
    if len(correct_docs) >= len(docs) * 0.5:
        # Sufficient relevant docs - proceed
        return generate_answer(query, correct_docs)
    elif len(correct_docs) > 0:
        # Some relevant - augment with web search
        web_results = web_search(query)
        all_docs = correct_docs + web_results
        return generate_answer(query, all_docs)
    else:
        # No relevant docs - full web search
        web_results = web_search(query)
        return generate_answer(query, web_results)
```

### Adaptive RAG: Dynamic Strategy Selection

```python
def adaptive_rag(query: str) -> str:
    """
    Dynamically select retrieval strategy based on query analysis.
    """
    # Analyze query complexity
    analysis = llm.invoke(f"""
    Analyze this query:
    Query: {query}
    
    Classify:
    - Complexity: [Simple/Medium/Complex]
    - Type: [Factual/Analytical/Creative/Comparison]
    - Domains: [list relevant domains]
    - Needs current info: [Yes/No]
    """)
    
    # Select strategy based on analysis
    if "Complex" in analysis:
        # Multi-hop retrieval with query decomposition
        return multi_hop_retrieve(query)
    elif "Comparison" in analysis:
        # Parallel retrieval for comparison items
        return parallel_comparison_retrieve(query)
    elif "current info: Yes" in analysis:
        # Hybrid with web search
        return hybrid_web_retrieve(query)
    else:
        # Standard single-hop retrieval
        return standard_retrieve(query)
```

---

## Part 3: Hybrid Search Implementation

### BM25 + Dense Vector Hybrid

```python
from rank_bm25 import BM25Okapi
from qdrant_client import QdrantClient
from qdrant_client.models import models

class HybridRetriever:
    """Combine BM25 keyword search with dense vector search."""
    
    def __init__(self, collection_name: str):
        self.qdrant = QdrantClient(url="http://localhost:6333")
        self.collection = collection_name
        self.bm25 = None
        self.corpus = []
        
    def index_documents(self, documents: List[str], embeddings: List[List[float]]):
        """Index documents for both sparse and dense search."""
        # BM25 index
        tokenized = [doc.lower().split() for doc in documents]
        self.bm25 = BM25Okapi(tokenized)
        self.corpus = documents
        
        # Qdrant dense index
        points = [
            models.PointStruct(id=i, vector=emb, payload={"text": doc})
            for i, (doc, emb) in enumerate(zip(documents, embeddings))
        ]
        self.qdrant.upsert(collection_name=self.collection, points=points)
    
    def search(self, query: str, query_embedding: List[float], 
               k: int = 10, alpha: float = 0.5) -> List[str]:
        """
        Hybrid search with configurable weighting.
        alpha=1.0: Pure dense, alpha=0.0: Pure BM25
        """
        # BM25 search
        tokenized_query = query.lower().split()
        bm25_scores = self.bm25.get_scores(tokenized_query)
        bm25_top_k = sorted(range(len(bm25_scores)), 
                           key=lambda i: bm25_scores[i], reverse=True)[:k*2]
        
        # Dense search
        dense_results = self.qdrant.search(
            collection_name=self.collection,
            query_vector=query_embedding,
            limit=k*2
        )
        dense_scores = {r.id: r.score for r in dense_results}
        
        # Reciprocal Rank Fusion
        rrf_scores = {}
        for rank, doc_id in enumerate(bm25_top_k):
            rrf_scores[doc_id] = rrf_scores.get(doc_id, 0) + (1 - alpha) / (60 + rank)
        for rank, result in enumerate(dense_results):
            rrf_scores[result.id] = rrf_scores.get(result.id, 0) + alpha / (60 + rank)
        
        # Sort by combined score
        top_ids = sorted(rrf_scores, key=rrf_scores.get, reverse=True)[:k]
        return [self.corpus[i] for i in top_ids]
```

### ColBERT Late Interaction Reranking

```python
from colbert.infra import ColBERTConfig
from colbert.modeling.checkpoint import Checkpoint

class ColBERTReranker:
    """Late interaction reranking for precision."""
    
    def __init__(self, checkpoint_path: str):
        self.config = ColBERTConfig(checkpoint=checkpoint_path)
        self.checkpoint = Checkpoint(self.config)
    
    def rerank(self, query: str, documents: List[str], top_k: int = 5) -> List[str]:
        """
        Rerank documents using ColBERT late interaction.
        Best used after initial retrieval to refine top results.
        """
        # Encode query and documents
        query_embedding = self.checkpoint.queryFromText([query])[0]
        doc_embeddings = self.checkpoint.docFromText(documents)
        
        # MaxSim scoring (late interaction)
        scores = []
        for doc_emb in doc_embeddings:
            # For each query token, find max similarity with any doc token
            sim_matrix = torch.matmul(query_embedding, doc_emb.T)
            max_sims = sim_matrix.max(dim=1).values
            scores.append(max_sims.sum().item())
        
        # Return top-k by score
        ranked_indices = sorted(range(len(scores)), key=lambda i: scores[i], reverse=True)
        return [documents[i] for i in ranked_indices[:top_k]]
```

---

## Part 4: Query Processing

### Query Decomposition

```python
def decompose_query(query: str) -> List[str]:
    """Break complex query into retrievable sub-queries."""
    prompt = f"""Decompose this query into simpler sub-questions that can be answered independently.
    
    Query: {query}
    
    Rules:
    1. Each sub-question should be self-contained
    2. Sub-questions should cover different aspects
    3. Return 2-4 sub-questions as JSON list
    
    Example:
    Query: "Compare React and Vue for enterprise applications considering performance and learning curve"
    Sub-questions: [
        "What is React's performance in enterprise applications?",
        "What is Vue's performance in enterprise applications?",
        "What is the learning curve for React?",
        "What is the learning curve for Vue?"
    ]"""
    
    result = llm.invoke(prompt)
    return json.loads(result)
```

### HyDE: Hypothetical Document Embeddings

```python
def hyde_retrieve(query: str, k: int = 5) -> List[str]:
    """
    Generate hypothetical answer, embed it, retrieve similar real docs.
    Improves retrieval for questions where query doesn't match document phrasing.
    """
    # Generate hypothetical document
    hypothetical = llm.invoke(f"""
    Write a detailed paragraph that would perfectly answer this question:
    {query}
    
    Write as if from an authoritative source. Include specific details.""")
    
    # Embed hypothetical answer (not the query)
    hyde_embedding = embed(hypothetical)
    
    # Retrieve documents similar to hypothetical answer
    results = vector_store.search(hyde_embedding, k=k)
    return results
```

### Query Rewriting

```python
def rewrite_query(query: str, conversation_history: List[dict] = None) -> str:
    """Rewrite query for better retrieval, incorporating context."""
    
    history_context = ""
    if conversation_history:
        history_context = "\n".join([
            f"{m['role']}: {m['content']}" for m in conversation_history[-3:]
        ])
    
    prompt = f"""Rewrite this query to be more effective for document retrieval.
    
    {f'Conversation context:{history_context}' if history_context else ''}
    
    Original query: {query}
    
    Guidelines:
    - Expand abbreviations
    - Add relevant synonyms
    - Remove conversational filler
    - Make implicit context explicit
    - Preserve original intent
    
    Rewritten query:"""
    
    return llm.invoke(prompt).strip()
```

---

## Part 5: Evaluation & Quality Control

### LLM-as-Judge Evaluation

```python
def evaluate_retrieval_quality(query: str, documents: List[str], answer: str) -> dict:
    """
    Use LLM to judge retrieval and generation quality.
    Returns structured quality metrics.
    """
    eval_prompt = f"""Evaluate this RAG response:

    Query: {query}
    Retrieved Documents: {documents[:3]}  # Top 3 for brevity
    Generated Answer: {answer}
    
    Rate each dimension 1-5 and explain:
    
    1. Retrieval Relevance: Are retrieved docs relevant to query?
    2. Faithfulness: Is answer grounded in retrieved docs?
    3. Answer Completeness: Does answer fully address query?
    4. Coherence: Is answer well-structured and clear?
    5. Conciseness: Is answer appropriately detailed (not too verbose/brief)?
    
    Return as JSON:
    {{
        "retrieval_relevance": {{"score": X, "reason": "..."}},
        "faithfulness": {{"score": X, "reason": "..."}},
        "completeness": {{"score": X, "reason": "..."}},
        "coherence": {{"score": X, "reason": "..."}},
        "conciseness": {{"score": X, "reason": "..."}}
    }}"""
    
    return json.loads(llm.invoke(eval_prompt))
```

### Automated Retrieval Testing

```python
class RAGEvaluator:
    """Automated evaluation pipeline for RAG systems."""
    
    def __init__(self, test_cases: List[dict]):
        """
        test_cases format:
        [{"query": "...", "expected_topics": [...], "ground_truth": "..."}]
        """
        self.test_cases = test_cases
        
    def evaluate(self, rag_pipeline) -> dict:
        results = {
            "recall@5": [],
            "mrr": [],
            "answer_similarity": [],
            "faithfulness": []
        }
        
        for case in self.test_cases:
            # Run RAG
            docs, answer = rag_pipeline(case["query"])
            
            # Recall: How many expected topics are in retrieved docs?
            doc_text = " ".join(docs)
            recall = sum(1 for t in case["expected_topics"] if t.lower() in doc_text.lower())
            recall /= len(case["expected_topics"])
            results["recall@5"].append(recall)
            
            # MRR: Position of first relevant doc
            for i, doc in enumerate(docs):
                if any(t.lower() in doc.lower() for t in case["expected_topics"]):
                    results["mrr"].append(1 / (i + 1))
                    break
            else:
                results["mrr"].append(0)
            
            # Answer similarity to ground truth
            similarity = compute_semantic_similarity(answer, case["ground_truth"])
            results["answer_similarity"].append(similarity)
        
        return {k: sum(v)/len(v) for k, v in results.items()}
```

---

## Part 6: Production Patterns

### Caching Strategy

```python
import hashlib
from functools import lru_cache

class RAGCache:
    """Multi-level caching for RAG systems."""
    
    def __init__(self, embedding_cache_size=10000, retrieval_cache_size=1000):
        self.embedding_cache = {}
        self.retrieval_cache = {}
        self.max_embedding = embedding_cache_size
        self.max_retrieval = retrieval_cache_size
    
    def _hash_query(self, query: str) -> str:
        return hashlib.md5(query.encode()).hexdigest()
    
    def get_embedding(self, text: str) -> Optional[List[float]]:
        return self.embedding_cache.get(self._hash_query(text))
    
    def set_embedding(self, text: str, embedding: List[float]):
        if len(self.embedding_cache) >= self.max_embedding:
            # FIFO eviction
            oldest = next(iter(self.embedding_cache))
            del self.embedding_cache[oldest]
        self.embedding_cache[self._hash_query(text)] = embedding
    
    def get_retrieval(self, query: str, k: int) -> Optional[List[str]]:
        key = f"{self._hash_query(query)}_{k}"
        return self.retrieval_cache.get(key)
    
    def set_retrieval(self, query: str, k: int, results: List[str]):
        if len(self.retrieval_cache) >= self.max_retrieval:
            oldest = next(iter(self.retrieval_cache))
            del self.retrieval_cache[oldest]
        key = f"{self._hash_query(query)}_{k}"
        self.retrieval_cache[key] = results
```

### Streaming Responses

```python
async def stream_rag_response(query: str):
    """Stream RAG response for better UX."""
    
    # Phase 1: Retrieve (show progress)
    yield {"type": "status", "message": "Searching knowledge base..."}
    docs = await async_retrieve(query)
    yield {"type": "status", "message": f"Found {len(docs)} relevant documents"}
    
    # Phase 2: Generate with streaming
    yield {"type": "status", "message": "Generating response..."}
    
    context = "\n".join(docs)
    async for chunk in llm.stream(
        f"Answer based on context:\n{context}\n\nQuestion: {query}"
    ):
        yield {"type": "content", "chunk": chunk}
    
    # Phase 3: Return sources
    yield {"type": "sources", "documents": docs}
```

### Error Recovery

```python
class ResilientRAG:
    """RAG with automatic fallback and recovery."""
    
    def __init__(self, primary_retriever, fallback_retriever, web_search):
        self.primary = primary_retriever
        self.fallback = fallback_retriever
        self.web = web_search
        
    async def retrieve(self, query: str, retries: int = 3) -> List[str]:
        # Try primary retriever
        for attempt in range(retries):
            try:
                docs = await self.primary.search(query)
                if docs:
                    return docs
            except Exception as e:
                logger.warning(f"Primary retrieval failed: {e}")
                await asyncio.sleep(2 ** attempt)  # Exponential backoff
        
        # Fallback to secondary
        try:
            docs = await self.fallback.search(query)
            if docs:
                return docs
        except Exception as e:
            logger.warning(f"Fallback retrieval failed: {e}")
        
        # Last resort: web search
        return await self.web.search(query)
```

---

## Decision Framework

### When to Use Agentic RAG

```
Query complexity?
├─ Simple factual → Standard RAG (single retrieval)
├─ Multi-faceted → Query decomposition + parallel retrieval
├─ Comparative → Multi-source routing
└─ Research-level → Full agentic loop with self-correction

Retrieval quality requirement?
├─ High precision needed → Add reranking (ColBERT)
├─ Recall critical → Hybrid search (BM25 + dense)
├─ Both → Hybrid + reranking pipeline
└─ Speed priority → Dense only, larger k

Context freshness?
├─ Static knowledge → Vector DB only
├─ Semi-dynamic → Scheduled re-indexing
├─ Real-time needed → Hybrid with web search
└─ Mixed → Adaptive routing based on query

Error tolerance?
├─ Mission critical → Multi-agent verification
├─ Standard → Self-RAG with critique
└─ Best effort → Single-pass with grading
```

---

## References

See `references/` for detailed implementation guides:
- `references/langgraph-patterns.md` - LangGraph workflow patterns
- `references/evaluation-metrics.md` - RAG evaluation frameworks
- `references/chunking-advanced.md` - Advanced chunking strategies

# Advanced Chunking Strategies (2025)

Comprehensive guide to document chunking for optimal RAG retrieval.

## Chunking Strategy Selection Matrix

| Strategy | Best For | Recall Improvement | Compute Cost | Implementation |
|----------|----------|-------------------|--------------|----------------|
| **Semantic** | Technical docs, manuals | +9% vs fixed | High (embeddings) | Medium |
| **Recursive** | General content | Baseline | Low | Simple |
| **Hierarchical** | Structured docs (legal, academic) | +15% for complex queries | Medium | Complex |
| **Agentic** | Mixed content types | +20% adaptive | Very High | Advanced |
| **Late Chunking** | Long documents | +12% context retention | High | Medium |

---

## 1. Semantic Chunking

Splits text at natural conceptual boundaries using embedding similarity.

```python
from langchain_experimental.text_splitter import SemanticChunker
from langchain_openai import OpenAIEmbeddings

def semantic_chunk(text: str, breakpoint_type: str = "percentile") -> List[str]:
    """
    Split on semantic boundaries detected via embedding similarity.
    
    breakpoint_types:
    - "percentile": Split when similarity drops below Xth percentile
    - "standard_deviation": Split when similarity drops by N std devs
    - "interquartile": Split using IQR for outlier detection
    - "gradient": Use gradient of similarity for boundary detection (best for domain-specific)
    """
    embeddings = OpenAIEmbeddings()
    chunker = SemanticChunker(
        embeddings=embeddings,
        breakpoint_threshold_type=breakpoint_type,
        breakpoint_threshold_amount=0.3  # Tune based on content
    )
    return chunker.split_text(text)

# For domain-specific content (legal, medical):
def domain_semantic_chunk(text: str) -> List[str]:
    """Use gradient-based splitting for highly correlated domain content."""
    embeddings = OpenAIEmbeddings()
    chunker = SemanticChunker(
        embeddings=embeddings,
        breakpoint_threshold_type="gradient",  # Best for domain-specific
        sentence_split_regex=r"(?<=[.?!])\s+"
    )
    return chunker.split_text(text)
```

**When to use:**
- Documents with clear topic shifts
- Quality > speed requirements
- RAG systems needing high retrieval precision

---

## 2. Hierarchical Chunking

Preserves document structure through parent-child relationships.

```python
from llama_index.core.node_parser import HierarchicalNodeParser, SentenceSplitter

def hierarchical_chunk(documents: List[str]) -> dict:
    """
    Create multi-level chunk hierarchy: document -> section -> paragraph -> sentence.
    Enables retrieval at multiple granularity levels.
    """
    parser = HierarchicalNodeParser.from_defaults(
        chunk_sizes=[2048, 512, 128],  # Parent, child, leaf sizes
        chunk_overlap=20
    )
    
    nodes = parser.get_nodes_from_documents(documents)
    
    # Organize by hierarchy
    hierarchy = {"parents": [], "children": [], "leaves": []}
    for node in nodes:
        if node.metadata.get("depth") == 0:
            hierarchy["parents"].append(node)
        elif node.metadata.get("depth") == 1:
            hierarchy["children"].append(node)
        else:
            hierarchy["leaves"].append(node)
    
    return hierarchy

# Auto-merging retrieval: retrieve leaves, merge to parent if majority retrieved
from llama_index.core.retrievers import AutoMergingRetriever

def auto_merging_retrieve(query: str, hierarchy: dict) -> List[str]:
    """
    Retrieve leaf chunks, automatically merge to parent when threshold met.
    Provides focused retrieval with broader context when needed.
    """
    retriever = AutoMergingRetriever(
        vector_retriever=leaf_index.as_retriever(similarity_top_k=6),
        storage_context=storage_context,
        simple_ratio_thresh=0.3  # Merge if 30% of children retrieved
    )
    return retriever.retrieve(query)
```

---

## 3. Agentic Chunking

AI agent dynamically selects chunking strategy per document section.

```python
def agentic_chunk(document: str) -> List[dict]:
    """
    Use LLM to intelligently segment document based on content analysis.
    Most accurate but highest compute cost.
    """
    analysis_prompt = f"""Analyze this document and identify optimal chunk boundaries.
    
    Document: {document[:3000]}...
    
    For each section, specify:
    1. Start/end markers
    2. Recommended chunk strategy: 
       - "semantic" for flowing narrative
       - "fixed" for lists/tables
       - "paragraph" for structured prose
       - "preserve" for code/formulas (don't split)
    3. Optimal chunk size for this section
    
    Return as JSON list."""
    
    analysis = json.loads(llm.invoke(analysis_prompt))
    
    chunks = []
    for section in analysis:
        section_text = extract_section(document, section["start"], section["end"])
        
        if section["strategy"] == "semantic":
            section_chunks = semantic_chunk(section_text)
        elif section["strategy"] == "fixed":
            section_chunks = fixed_chunk(section_text, section["chunk_size"])
        elif section["strategy"] == "preserve":
            section_chunks = [section_text]  # Keep intact
        else:
            section_chunks = paragraph_chunk(section_text)
        
        chunks.extend([{
            "text": c, 
            "strategy": section["strategy"],
            "section_type": section.get("type", "unknown")
        } for c in section_chunks])
    
    return chunks
```

---

## 4. Late Chunking (Jina AI Pattern)

Preserve full document context during embedding, chunk afterward.

```python
def late_chunk(document: str, chunk_size: int = 512) -> List[dict]:
    """
    Late chunking: Embed full document, then chunk embeddings.
    Preserves long-range context that's lost in chunk-then-embed.
    
    Based on Jina AI research: maintains document-level coherence.
    """
    # Step 1: Get document-level embedding with position info
    full_embedding = embed_with_positions(document)  # Returns token-level embeddings
    
    # Step 2: Segment text into chunks
    words = document.split()
    text_chunks = []
    for i in range(0, len(words), chunk_size):
        text_chunks.append(" ".join(words[i:i+chunk_size]))
    
    # Step 3: Aggregate embeddings per chunk (using position mapping)
    chunk_embeddings = []
    for chunk_idx, chunk in enumerate(text_chunks):
        start_token = chunk_idx * chunk_size
        end_token = min(start_token + chunk_size, len(full_embedding))
        
        # Mean pooling over token embeddings in this chunk
        chunk_emb = mean_pool(full_embedding[start_token:end_token])
        chunk_embeddings.append(chunk_emb)
    
    return [{"text": t, "embedding": e} for t, e in zip(text_chunks, chunk_embeddings)]
```

---

## 5. Context-Enriched Chunks

Add metadata and surrounding context to each chunk.

```python
def enrich_chunks(chunks: List[str], document: str, metadata: dict) -> List[dict]:
    """
    Enrich chunks with contextual information for better retrieval.
    """
    enriched = []
    
    for i, chunk in enumerate(chunks):
        # Generate chunk summary
        summary = llm.invoke(f"Summarize in 1 sentence: {chunk}")
        
        # Extract key entities
        entities = extract_entities(chunk)
        
        # Add neighboring context hints
        prev_context = chunks[i-1][-100:] if i > 0 else ""
        next_context = chunks[i+1][:100] if i < len(chunks)-1 else ""
        
        enriched.append({
            "text": chunk,
            "summary": summary,
            "entities": entities,
            "prev_hint": prev_context,
            "next_hint": next_context,
            "position": i / len(chunks),  # Relative position
            "word_count": len(chunk.split()),
            **metadata
        })
    
    return enriched
```

---

## Optimal Chunk Sizes by Use Case

| Use Case | Chunk Size (tokens) | Overlap | Rationale |
|----------|---------------------|---------|-----------|
| Q&A / FAQ | 256-512 | 10-15% | Focused, single-topic answers |
| Technical docs | 512-1024 | 15-20% | Code examples need context |
| Legal/contracts | 1024-2048 | 20-25% | Clause integrity matters |
| Conversational | 256-384 | 5-10% | Quick, relevant responses |
| Research papers | 512-768 | 15% | Balance section/paragraph |
| Code files | 256-512 | 25% | Function boundaries |

---

## Evaluation: How to Test Chunking Quality

```python
def evaluate_chunking_strategy(
    chunks: List[str], 
    test_queries: List[dict],  # {"query": str, "relevant_chunk_indices": List[int]}
    embedding_model
) -> dict:
    """
    Evaluate chunking quality using retrieval metrics.
    """
    metrics = {"recall@5": [], "mrr": [], "chunk_coherence": []}
    
    # Embed all chunks once
    chunk_embeddings = [embedding_model.embed(c) for c in chunks]
    
    for test in test_queries:
        query_emb = embedding_model.embed(test["query"])
        
        # Compute similarities
        similarities = [cosine_sim(query_emb, ce) for ce in chunk_embeddings]
        top_5_indices = sorted(range(len(similarities)), 
                               key=lambda i: similarities[i], reverse=True)[:5]
        
        # Recall@5
        relevant_found = len(set(top_5_indices) & set(test["relevant_chunk_indices"]))
        metrics["recall@5"].append(relevant_found / len(test["relevant_chunk_indices"]))
        
        # MRR
        for rank, idx in enumerate(top_5_indices):
            if idx in test["relevant_chunk_indices"]:
                metrics["mrr"].append(1 / (rank + 1))
                break
        else:
            metrics["mrr"].append(0)
    
    # Chunk coherence: measure semantic self-similarity within chunks
    for chunk in chunks:
        sentences = chunk.split(". ")
        if len(sentences) > 1:
            sent_embs = [embedding_model.embed(s) for s in sentences]
            coherence = mean([cosine_sim(sent_embs[i], sent_embs[i+1]) 
                             for i in range(len(sent_embs)-1)])
            metrics["chunk_coherence"].append(coherence)
    
    return {k: sum(v)/len(v) if v else 0 for k, v in metrics.items()}
```

---

## Quick Reference: Chunking Decision Tree

```
Document type?
├─ Structured (headings, sections) → Hierarchical
├─ Narrative (articles, books) → Semantic
├─ Technical (code, formulas) → Preserve blocks + semantic for prose
├─ Mixed → Agentic chunking
└─ Tables/lists → Fixed size, no overlap

Quality requirement?
├─ Production, high stakes → Semantic + evaluation testing
├─ Development/MVP → Recursive character (LangChain default)
└─ Prototyping → Fixed size (fastest)

Context preservation need?
├─ Critical (legal, research) → Large overlap (25%) + parent references
├─ Standard → 15-20% overlap
└─ Not critical → 10% overlap or none
```

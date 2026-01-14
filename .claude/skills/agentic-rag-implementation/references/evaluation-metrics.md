# RAG Evaluation Metrics & Frameworks (2025)

Comprehensive guide to evaluating Retrieval-Augmented Generation systems.

---

## Core Metrics Overview

| Metric | Measures | Range | Target |
|--------|----------|-------|--------|
| **Retrieval Recall@k** | % of relevant docs retrieved | 0-1 | >0.8 |
| **MRR** | Rank position of first relevant | 0-1 | >0.7 |
| **NDCG@k** | Ranking quality with position weights | 0-1 | >0.75 |
| **Faithfulness** | Answer grounded in context | 0-1 | >0.9 |
| **Answer Relevance** | Answer addresses query | 0-1 | >0.85 |
| **Context Precision** | Retrieved context relevance | 0-1 | >0.8 |
| **Context Recall** | Coverage of ground truth | 0-1 | >0.75 |

---

## 1. Retrieval Metrics

### Recall@k

```python
def recall_at_k(retrieved_ids: List[str], relevant_ids: List[str], k: int = 5) -> float:
    """
    Proportion of relevant documents retrieved in top-k.
    
    recall@k = |retrieved ∩ relevant| / |relevant|
    """
    retrieved_set = set(retrieved_ids[:k])
    relevant_set = set(relevant_ids)
    
    if not relevant_set:
        return 0.0
    
    return len(retrieved_set & relevant_set) / len(relevant_set)

# Example
retrieved = ["doc1", "doc3", "doc5", "doc7", "doc9"]
relevant = ["doc1", "doc2", "doc3"]
print(recall_at_k(retrieved, relevant, k=5))  # 0.67
```

### Mean Reciprocal Rank (MRR)

```python
def mrr(queries: List[dict]) -> float:
    """
    Average of reciprocal ranks of first relevant document.
    
    queries: [{"retrieved": [...], "relevant": [...]}]
    """
    reciprocal_ranks = []
    
    for q in queries:
        for rank, doc_id in enumerate(q["retrieved"], 1):
            if doc_id in q["relevant"]:
                reciprocal_ranks.append(1.0 / rank)
                break
        else:
            reciprocal_ranks.append(0.0)
    
    return sum(reciprocal_ranks) / len(reciprocal_ranks)
```

### NDCG@k (Normalized Discounted Cumulative Gain)

```python
import numpy as np

def ndcg_at_k(retrieved_ids: List[str], relevance_scores: Dict[str, float], k: int = 5) -> float:
    """
    Measures ranking quality with position-based discounting.
    
    relevance_scores: {doc_id: relevance_score} where score typically 0-3
    """
    def dcg(scores):
        return sum(score / np.log2(rank + 2) 
                   for rank, score in enumerate(scores))
    
    # Get relevance scores for retrieved docs
    retrieved_scores = [relevance_scores.get(doc_id, 0) for doc_id in retrieved_ids[:k]]
    
    # Ideal ranking (sort by relevance)
    ideal_scores = sorted(relevance_scores.values(), reverse=True)[:k]
    
    dcg_score = dcg(retrieved_scores)
    idcg_score = dcg(ideal_scores)
    
    return dcg_score / idcg_score if idcg_score > 0 else 0.0
```

---

## 2. Generation Metrics

### Faithfulness (Groundedness)

```python
def compute_faithfulness(answer: str, context: str) -> float:
    """
    Measure how well the answer is grounded in retrieved context.
    Uses LLM-as-judge approach.
    """
    # Extract claims from answer
    claims_prompt = f"""Extract factual claims from this answer as a JSON list:
    Answer: {answer}
    
    Return only concrete, verifiable claims."""
    
    claims = json.loads(llm.invoke(claims_prompt))
    
    if not claims:
        return 1.0  # No claims = perfectly grounded (vacuously true)
    
    # Verify each claim against context
    supported_count = 0
    for claim in claims:
        verification = llm.invoke(f"""Is this claim supported by the context?
        
        Claim: {claim}
        Context: {context}
        
        Answer only 'supported' or 'not_supported'.""")
        
        if "supported" in verification.lower() and "not" not in verification.lower():
            supported_count += 1
    
    return supported_count / len(claims)
```

### Answer Relevance

```python
def compute_answer_relevance(query: str, answer: str) -> float:
    """
    Measure how well the answer addresses the query.
    """
    # Generate questions that the answer would address
    questions_prompt = f"""Generate 3-5 questions that this answer addresses:
    
    Answer: {answer}
    
    Return as JSON list."""
    
    generated_questions = json.loads(llm.invoke(questions_prompt))
    
    # Compute semantic similarity between original query and generated questions
    query_embedding = embed(query)
    question_embeddings = [embed(q) for q in generated_questions]
    
    similarities = [cosine_similarity(query_embedding, qe) for qe in question_embeddings]
    
    return np.mean(similarities)
```

### Context Precision & Recall

```python
def compute_context_metrics(
    retrieved_context: List[str], 
    ground_truth_answer: str,
    query: str
) -> Dict[str, float]:
    """
    Context Precision: How much of retrieved context is actually useful?
    Context Recall: How much of needed info is in retrieved context?
    """
    # Context Precision: Grade each retrieved chunk
    useful_chunks = 0
    for chunk in retrieved_context:
        usefulness = llm.invoke(f"""Is this context useful for answering the query?
        Query: {query}
        Context: {chunk}
        
        Answer 'useful' or 'not_useful'.""")
        
        if "useful" in usefulness.lower() and "not" not in usefulness.lower():
            useful_chunks += 1
    
    precision = useful_chunks / len(retrieved_context) if retrieved_context else 0
    
    # Context Recall: Can ground truth be derived from context?
    context_combined = "\n".join(retrieved_context)
    
    # Extract key points from ground truth
    key_points = llm.invoke(f"""Extract key factual points from this answer:
    {ground_truth_answer}
    Return as JSON list.""")
    key_points = json.loads(key_points)
    
    # Check coverage
    covered = 0
    for point in key_points:
        coverage = llm.invoke(f"""Is this point covered in the context?
        Point: {point}
        Context: {context_combined}
        
        Answer 'covered' or 'not_covered'.""")
        
        if "covered" in coverage.lower() and "not" not in coverage.lower():
            covered += 1
    
    recall = covered / len(key_points) if key_points else 0
    
    return {"context_precision": precision, "context_recall": recall}
```

---

## 3. End-to-End Evaluation with RAGAS

```python
from ragas import evaluate
from ragas.metrics import (
    faithfulness,
    answer_relevancy,
    context_precision,
    context_recall,
)
from datasets import Dataset

def evaluate_rag_with_ragas(
    questions: List[str],
    answers: List[str],
    contexts: List[List[str]],
    ground_truths: List[str]
) -> Dict[str, float]:
    """
    Full RAG evaluation using RAGAS framework.
    """
    # Create dataset
    data = {
        "question": questions,
        "answer": answers,
        "contexts": contexts,
        "ground_truth": ground_truths
    }
    dataset = Dataset.from_dict(data)
    
    # Run evaluation
    result = evaluate(
        dataset=dataset,
        metrics=[
            faithfulness,
            answer_relevancy,
            context_precision,
            context_recall,
        ]
    )
    
    return result.to_pandas().mean().to_dict()

# Example usage
results = evaluate_rag_with_ragas(
    questions=["What is RAG?", "How does retrieval work?"],
    answers=["RAG combines retrieval with generation...", "Retrieval uses..."],
    contexts=[["RAG stands for...", "It combines..."], ["Retrieval involves..."]],
    ground_truths=["RAG is a technique...", "Retrieval finds relevant..."]
)
```

---

## 4. LLM-as-Judge Evaluation

### Structured Evaluation Rubric

```python
def llm_judge_evaluate(query: str, context: str, answer: str) -> Dict[str, Any]:
    """
    Comprehensive LLM-as-judge evaluation with structured output.
    """
    evaluation_prompt = f"""You are an expert evaluator for RAG systems.
    
    Evaluate this response across multiple dimensions.
    
    Query: {query}
    Retrieved Context: {context[:2000]}
    Generated Answer: {answer}
    
    Score each dimension 1-5 and provide brief justification:
    
    1. **Relevance** (Does answer address the query?)
    2. **Faithfulness** (Is answer grounded in context?)
    3. **Completeness** (Does answer fully address query?)
    4. **Coherence** (Is answer well-organized and clear?)
    5. **Conciseness** (Is answer appropriately detailed?)
    
    Also identify:
    - Any hallucinations (claims not in context)
    - Missing information (important context not used)
    - Contradictions (answer vs context)
    
    Return as JSON:
    {{
        "scores": {{
            "relevance": {{"score": X, "reason": "..."}},
            "faithfulness": {{"score": X, "reason": "..."}},
            "completeness": {{"score": X, "reason": "..."}},
            "coherence": {{"score": X, "reason": "..."}},
            "conciseness": {{"score": X, "reason": "..."}}
        }},
        "issues": {{
            "hallucinations": ["..."],
            "missing_info": ["..."],
            "contradictions": ["..."]
        }},
        "overall_score": X,
        "recommendation": "pass" | "needs_improvement" | "fail"
    }}"""
    
    return json.loads(llm.invoke(evaluation_prompt))
```

---

## 5. Automated Test Suite

```python
class RAGTestSuite:
    """Comprehensive test suite for RAG systems."""
    
    def __init__(self, rag_pipeline, test_cases: List[dict]):
        """
        test_cases format:
        [
            {
                "query": "...",
                "expected_topics": ["topic1", "topic2"],
                "ground_truth": "...",
                "difficulty": "easy" | "medium" | "hard"
            }
        ]
        """
        self.rag = rag_pipeline
        self.test_cases = test_cases
        
    def run_full_evaluation(self) -> Dict[str, Any]:
        """Run all evaluation metrics."""
        results = {
            "retrieval": {"recall@5": [], "mrr": []},
            "generation": {"faithfulness": [], "relevance": []},
            "e2e": {"correct": [], "latency": []},
            "by_difficulty": {"easy": [], "medium": [], "hard": []}
        }
        
        for case in self.test_cases:
            start_time = time.time()
            
            # Run RAG
            retrieved_docs, answer = self.rag(case["query"])
            
            latency = time.time() - start_time
            results["e2e"]["latency"].append(latency)
            
            # Retrieval metrics
            doc_texts = [d.page_content for d in retrieved_docs]
            recall = self._compute_topic_recall(doc_texts, case["expected_topics"])
            results["retrieval"]["recall@5"].append(recall)
            
            # Generation metrics
            faithfulness = self._compute_faithfulness(answer, doc_texts)
            results["generation"]["faithfulness"].append(faithfulness)
            
            relevance = self._compute_relevance(case["query"], answer)
            results["generation"]["relevance"].append(relevance)
            
            # E2E correctness
            correct = self._check_correctness(answer, case["ground_truth"])
            results["e2e"]["correct"].append(correct)
            
            # By difficulty
            results["by_difficulty"][case["difficulty"]].append(correct)
        
        # Aggregate
        return {
            "retrieval_recall@5": np.mean(results["retrieval"]["recall@5"]),
            "retrieval_mrr": np.mean(results["retrieval"]["mrr"]),
            "generation_faithfulness": np.mean(results["generation"]["faithfulness"]),
            "generation_relevance": np.mean(results["generation"]["relevance"]),
            "e2e_accuracy": np.mean(results["e2e"]["correct"]),
            "avg_latency_ms": np.mean(results["e2e"]["latency"]) * 1000,
            "accuracy_by_difficulty": {
                k: np.mean(v) if v else 0 
                for k, v in results["by_difficulty"].items()
            }
        }
    
    def _compute_topic_recall(self, docs: List[str], topics: List[str]) -> float:
        combined = " ".join(docs).lower()
        found = sum(1 for t in topics if t.lower() in combined)
        return found / len(topics) if topics else 0
    
    def _compute_faithfulness(self, answer: str, context: List[str]) -> float:
        # Simplified: check if answer terms appear in context
        context_combined = " ".join(context).lower()
        answer_terms = set(answer.lower().split())
        context_terms = set(context_combined.split())
        overlap = len(answer_terms & context_terms)
        return overlap / len(answer_terms) if answer_terms else 0
    
    def _compute_relevance(self, query: str, answer: str) -> float:
        query_emb = embed(query)
        answer_emb = embed(answer)
        return cosine_similarity(query_emb, answer_emb)
    
    def _check_correctness(self, answer: str, ground_truth: str) -> bool:
        # Use LLM to check semantic equivalence
        check = llm.invoke(f"""Are these answers semantically equivalent?
        Answer 1: {answer}
        Answer 2: {ground_truth}
        
        Reply 'yes' or 'no'.""")
        return "yes" in check.lower()

# Usage
test_cases = [
    {"query": "What is RAG?", "expected_topics": ["retrieval", "generation"], 
     "ground_truth": "RAG combines retrieval with LLM generation", "difficulty": "easy"},
    # More test cases...
]

suite = RAGTestSuite(my_rag_pipeline, test_cases)
results = suite.run_full_evaluation()
print(json.dumps(results, indent=2))
```

---

## 6. Continuous Monitoring Dashboard Metrics

```python
from dataclasses import dataclass
from datetime import datetime
from collections import deque

@dataclass
class RAGMetricPoint:
    timestamp: datetime
    query: str
    retrieval_time_ms: float
    generation_time_ms: float
    num_docs_retrieved: int
    faithfulness_score: float
    user_feedback: Optional[int]  # 1-5 or None

class RAGMonitor:
    """Production monitoring for RAG systems."""
    
    def __init__(self, window_size: int = 1000):
        self.metrics = deque(maxlen=window_size)
        
    def log(self, metric: RAGMetricPoint):
        self.metrics.append(metric)
        
    def get_dashboard_metrics(self) -> Dict[str, Any]:
        if not self.metrics:
            return {}
            
        recent = list(self.metrics)[-100:]  # Last 100 queries
        
        return {
            "total_queries": len(self.metrics),
            "avg_latency_ms": {
                "retrieval": np.mean([m.retrieval_time_ms for m in recent]),
                "generation": np.mean([m.generation_time_ms for m in recent]),
                "total": np.mean([m.retrieval_time_ms + m.generation_time_ms for m in recent])
            },
            "p95_latency_ms": {
                "total": np.percentile([m.retrieval_time_ms + m.generation_time_ms for m in recent], 95)
            },
            "avg_docs_retrieved": np.mean([m.num_docs_retrieved for m in recent]),
            "avg_faithfulness": np.mean([m.faithfulness_score for m in recent]),
            "user_satisfaction": np.mean([m.user_feedback for m in recent if m.user_feedback]),
            "queries_per_minute": self._compute_qpm()
        }
    
    def _compute_qpm(self) -> float:
        if len(self.metrics) < 2:
            return 0
        time_span = (self.metrics[-1].timestamp - self.metrics[0].timestamp).total_seconds()
        return len(self.metrics) / (time_span / 60) if time_span > 0 else 0
```

---

## Quick Reference: Metric Thresholds

| Metric | Poor | Acceptable | Good | Excellent |
|--------|------|------------|------|-----------|
| Recall@5 | <0.5 | 0.5-0.7 | 0.7-0.85 | >0.85 |
| MRR | <0.4 | 0.4-0.6 | 0.6-0.8 | >0.8 |
| Faithfulness | <0.7 | 0.7-0.85 | 0.85-0.95 | >0.95 |
| Answer Relevance | <0.6 | 0.6-0.75 | 0.75-0.9 | >0.9 |
| P95 Latency | >5s | 2-5s | 1-2s | <1s |

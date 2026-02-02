# Agent Memory Patterns

Strategies for managing agent memory across sessions.

---

## Memory Types

| Type | Duration | Use Case | Storage |
|------|----------|----------|---------|
| **Working** | Single turn | Tool results, intermediate state | In-context |
| **Short-term** | Session | Conversation history | In-memory |
| **Long-term** | Persistent | User preferences, facts | Database |
| **Episodic** | Persistent | Key events, experiences | Vector DB |
| **Semantic** | Persistent | Knowledge graphs | Graph DB |

---

## Working Memory (In-Context)

```python
class WorkingMemory:
    """Track current turn's state."""
    
    def __init__(self, max_tokens: int = 4000):
        self.max_tokens = max_tokens
        self.items = []
    
    def add(self, item: dict):
        """Add item, evicting old if over limit."""
        self.items.append(item)
        self._trim_to_limit()
    
    def _trim_to_limit(self):
        """Remove oldest items if over token limit."""
        while self._count_tokens() > self.max_tokens and len(self.items) > 1:
            self.items.pop(0)
    
    def get_context(self) -> str:
        """Get formatted context for prompt."""
        return "\n".join([
            f"[{item['type']}]: {item['content']}"
            for item in self.items
        ])
```

---

## Short-Term Memory (Conversation)

```python
class ConversationMemory:
    """Manage conversation history within session."""
    
    def __init__(self, max_messages: int = 50, summarize_threshold: int = 30):
        self.messages = []
        self.max_messages = max_messages
        self.summarize_threshold = summarize_threshold
        self.summary = ""
    
    def add_message(self, role: str, content: str):
        self.messages.append({"role": role, "content": content})
        
        if len(self.messages) > self.summarize_threshold:
            self._summarize_old_messages()
    
    def _summarize_old_messages(self):
        """Summarize older messages to save space."""
        old_messages = self.messages[:self.summarize_threshold // 2]
        
        summary = llm.invoke(f"""Summarize this conversation:
        {self._format_messages(old_messages)}
        
        Key points only, be concise.""")
        
        self.summary = summary
        self.messages = self.messages[self.summarize_threshold // 2:]
    
    def get_messages(self) -> List[dict]:
        """Get messages for API call."""
        result = []
        
        if self.summary:
            result.append({
                "role": "system",
                "content": f"Previous conversation summary: {self.summary}"
            })
        
        result.extend(self.messages[-self.max_messages:])
        return result
```

---

## Long-Term Memory (Persistent Facts)

```python
from datetime import datetime
import sqlite3

class LongTermMemory:
    """Persistent storage for facts and preferences."""
    
    def __init__(self, db_path: str):
        self.conn = sqlite3.connect(db_path)
        self._init_schema()
    
    def _init_schema(self):
        self.conn.execute("""
            CREATE TABLE IF NOT EXISTS memories (
                id INTEGER PRIMARY KEY,
                user_id TEXT,
                key TEXT,
                value TEXT,
                confidence REAL DEFAULT 1.0,
                source TEXT,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                accessed_at TIMESTAMP,
                access_count INTEGER DEFAULT 0,
                UNIQUE(user_id, key)
            )
        """)
    
    def remember(self, user_id: str, key: str, value: str, 
                 confidence: float = 1.0, source: str = "conversation"):
        """Store or update a fact."""
        self.conn.execute("""
            INSERT INTO memories (user_id, key, value, confidence, source)
            VALUES (?, ?, ?, ?, ?)
            ON CONFLICT(user_id, key) DO UPDATE SET
                value = excluded.value,
                confidence = excluded.confidence,
                accessed_at = CURRENT_TIMESTAMP
        """, (user_id, key, value, confidence, source))
        self.conn.commit()
    
    def recall(self, user_id: str, key: str = None) -> dict:
        """Retrieve facts for a user."""
        if key:
            cursor = self.conn.execute(
                "SELECT key, value, confidence FROM memories WHERE user_id = ? AND key = ?",
                (user_id, key)
            )
            row = cursor.fetchone()
            if row:
                # Update access stats
                self.conn.execute(
                    "UPDATE memories SET accessed_at = ?, access_count = access_count + 1 WHERE user_id = ? AND key = ?",
                    (datetime.now(), user_id, key)
                )
                return {"key": row[0], "value": row[1], "confidence": row[2]}
            return None
        else:
            cursor = self.conn.execute(
                "SELECT key, value, confidence FROM memories WHERE user_id = ? ORDER BY access_count DESC",
                (user_id,)
            )
            return [{"key": r[0], "value": r[1], "confidence": r[2]} for r in cursor.fetchall()]
    
    def forget(self, user_id: str, key: str):
        """Remove a specific memory."""
        self.conn.execute(
            "DELETE FROM memories WHERE user_id = ? AND key = ?",
            (user_id, key)
        )
        self.conn.commit()
    
    def decay_confidence(self, days_threshold: int = 30):
        """Reduce confidence of old, unused memories."""
        self.conn.execute("""
            UPDATE memories 
            SET confidence = confidence * 0.9
            WHERE accessed_at < datetime('now', ?)
        """, (f'-{days_threshold} days',))
        self.conn.commit()
```

---

## Episodic Memory (Events)

```python
from qdrant_client import QdrantClient
from qdrant_client.models import PointStruct, Distance, VectorParams

class EpisodicMemory:
    """Store and retrieve significant experiences."""
    
    def __init__(self, qdrant_url: str):
        self.client = QdrantClient(url=qdrant_url)
        self._init_collection()
    
    def _init_collection(self):
        self.client.recreate_collection(
            collection_name="episodes",
            vectors_config=VectorParams(size=1536, distance=Distance.COSINE)
        )
    
    def store_episode(self, user_id: str, event: str, 
                      importance: float = 0.5, context: dict = None):
        """Store a significant event."""
        embedding = embed(event)
        
        point = PointStruct(
            id=uuid.uuid4().hex,
            vector=embedding,
            payload={
                "user_id": user_id,
                "event": event,
                "importance": importance,
                "context": context or {},
                "timestamp": datetime.now().isoformat()
            }
        )
        
        self.client.upsert(collection_name="episodes", points=[point])
    
    def recall_similar(self, user_id: str, query: str, limit: int = 5) -> List[dict]:
        """Find episodes similar to query."""
        query_embedding = embed(query)
        
        results = self.client.search(
            collection_name="episodes",
            query_vector=query_embedding,
            query_filter={"must": [{"key": "user_id", "match": {"value": user_id}}]},
            limit=limit
        )
        
        return [
            {
                "event": r.payload["event"],
                "importance": r.payload["importance"],
                "timestamp": r.payload["timestamp"],
                "relevance": r.score
            }
            for r in results
        ]
    
    def recall_recent(self, user_id: str, limit: int = 10) -> List[dict]:
        """Get most recent episodes."""
        # Scroll through all points for user, sorted by timestamp
        results = self.client.scroll(
            collection_name="episodes",
            scroll_filter={"must": [{"key": "user_id", "match": {"value": user_id}}]},
            limit=limit,
            with_payload=True
        )
        
        episodes = sorted(
            [r.payload for r in results[0]],
            key=lambda x: x["timestamp"],
            reverse=True
        )
        return episodes[:limit]
```

---

## Semantic Memory (Knowledge Graph)

```python
from neo4j import GraphDatabase

class SemanticMemory:
    """Knowledge graph for relationships and concepts."""
    
    def __init__(self, uri: str, user: str, password: str):
        self.driver = GraphDatabase.driver(uri, auth=(user, password))
    
    def add_entity(self, user_id: str, name: str, type: str, properties: dict = None):
        """Add an entity to the knowledge graph."""
        with self.driver.session() as session:
            session.run("""
                MERGE (e:Entity {name: $name, user_id: $user_id})
                SET e.type = $type, e += $properties
            """, name=name, user_id=user_id, type=type, properties=properties or {})
    
    def add_relationship(self, user_id: str, entity1: str, relation: str, entity2: str):
        """Add a relationship between entities."""
        with self.driver.session() as session:
            session.run("""
                MATCH (e1:Entity {name: $entity1, user_id: $user_id})
                MATCH (e2:Entity {name: $entity2, user_id: $user_id})
                MERGE (e1)-[r:RELATES {type: $relation}]->(e2)
            """, entity1=entity1, entity2=entity2, relation=relation, user_id=user_id)
    
    def query_relationships(self, user_id: str, entity: str) -> List[dict]:
        """Get all relationships for an entity."""
        with self.driver.session() as session:
            result = session.run("""
                MATCH (e:Entity {name: $entity, user_id: $user_id})-[r]-(related)
                RETURN type(r) as relation, related.name as related_entity, 
                       related.type as entity_type
            """, entity=entity, user_id=user_id)
            
            return [dict(record) for record in result]
    
    def find_path(self, user_id: str, start: str, end: str) -> List[str]:
        """Find connection path between entities."""
        with self.driver.session() as session:
            result = session.run("""
                MATCH path = shortestPath(
                    (start:Entity {name: $start, user_id: $user_id})-[*]-(end:Entity {name: $end, user_id: $user_id})
                )
                RETURN [n in nodes(path) | n.name] as path
            """, start=start, end=end, user_id=user_id)
            
            record = result.single()
            return record["path"] if record else []
```

---

## Unified Memory System

```python
class AgentMemorySystem:
    """Unified memory system combining all types."""
    
    def __init__(self, config: dict):
        self.working = WorkingMemory()
        self.conversation = ConversationMemory()
        self.long_term = LongTermMemory(config["sqlite_path"])
        self.episodic = EpisodicMemory(config["qdrant_url"])
        self.semantic = SemanticMemory(
            config["neo4j_uri"],
            config["neo4j_user"],
            config["neo4j_password"]
        )
    
    def get_context(self, user_id: str, query: str) -> str:
        """Assemble relevant context from all memory types."""
        context_parts = []
        
        # Long-term facts
        facts = self.long_term.recall(user_id)
        if facts:
            context_parts.append("Known facts:\n" + "\n".join(
                [f"- {f['key']}: {f['value']}" for f in facts[:10]]
            ))
        
        # Relevant episodes
        episodes = self.episodic.recall_similar(user_id, query, limit=3)
        if episodes:
            context_parts.append("Relevant past experiences:\n" + "\n".join(
                [f"- {e['event']}" for e in episodes]
            ))
        
        # Working memory
        if self.working.items:
            context_parts.append("Current context:\n" + self.working.get_context())
        
        return "\n\n".join(context_parts)
    
    def process_turn(self, user_id: str, user_input: str, agent_response: str):
        """Update memories after each turn."""
        # Update conversation
        self.conversation.add_message("user", user_input)
        self.conversation.add_message("assistant", agent_response)
        
        # Extract and store facts
        facts = self._extract_facts(user_input + " " + agent_response)
        for fact in facts:
            self.long_term.remember(user_id, fact["key"], fact["value"])
        
        # Store significant events
        if self._is_significant(user_input, agent_response):
            self.episodic.store_episode(
                user_id,
                f"User asked about {self._extract_topic(user_input)}",
                importance=0.7
            )
    
    def _extract_facts(self, text: str) -> List[dict]:
        """Use LLM to extract facts from text."""
        extraction = llm.invoke(f"""Extract factual information from this text.
        Return as JSON list with 'key' and 'value' fields.
        Only include clear, specific facts (names, dates, preferences).
        
        Text: {text}""")
        
        try:
            return json.loads(extraction)
        except:
            return []
    
    def _is_significant(self, user_input: str, response: str) -> bool:
        """Determine if interaction is worth remembering."""
        # Simple heuristic: longer interactions are more significant
        return len(user_input) + len(response) > 500
```

---

## Memory Retrieval Strategies

### Recency-Weighted Retrieval

```python
def get_relevant_memories(query: str, memories: List[dict], 
                          recency_weight: float = 0.3) -> List[dict]:
    """Retrieve memories weighted by relevance and recency."""
    now = datetime.now()
    
    scored = []
    for memory in memories:
        # Semantic relevance (0-1)
        relevance = compute_similarity(query, memory["content"])
        
        # Recency score (0-1, exponential decay)
        age_days = (now - memory["timestamp"]).days
        recency = math.exp(-age_days / 30)  # Half-life of 30 days
        
        # Combined score
        score = (1 - recency_weight) * relevance + recency_weight * recency
        scored.append((memory, score))
    
    # Return top memories by combined score
    scored.sort(key=lambda x: x[1], reverse=True)
    return [m for m, s in scored[:10]]
```

### Importance-Based Retrieval

```python
def get_important_memories(user_id: str, limit: int = 10) -> List[dict]:
    """Retrieve most important memories."""
    # Combine multiple importance signals
    long_term = memory.long_term.recall(user_id)
    episodes = memory.episodic.recall_recent(user_id, limit=20)
    
    # Score by: access frequency, confidence, importance rating
    scored = []
    for fact in long_term:
        score = fact["confidence"] * fact.get("access_count", 1) / 10
        scored.append((fact, score, "fact"))
    
    for episode in episodes:
        score = episode["importance"]
        scored.append((episode, score, "episode"))
    
    # Return top by importance
    scored.sort(key=lambda x: x[1], reverse=True)
    return [(m, t) for m, s, t in scored[:limit]]
```

# AI/Agent Development Task

## Expert Mode: Agent Builder
Reference: mcp-builder + agentic-rag-implementation + azure-openai-integration

## Task Description
[DESCRIBE YOUR AI/AGENT TASK HERE]

## Apply These Patterns

### MCP Server Development
- TypeScript with streamable HTTP transport
- Comprehensive API coverage
- Clear, descriptive tool names and schemas
- Actionable error messages
- Stateless design for scalability

### Azure OpenAI Integration (.NET)
```csharp
// Using Azure OpenAI SDK
var client = new AzureOpenAIClient(
    new Uri(endpoint),
    new AzureKeyCredential(apiKey));

var chatClient = client.GetChatClient("gpt-4o");

var response = await chatClient.CompleteChatAsync(
    new ChatMessage[]
    {
        new SystemChatMessage("You are a helpful assistant."),
        new UserChatMessage("What is the capital of France?")
    });
```

### Semantic Kernel Patterns
```csharp
var kernel = Kernel.CreateBuilder()
    .AddAzureOpenAIChatCompletion(deploymentName, endpoint, apiKey)
    .Build();

// Function calling
var function = kernel.CreateFunctionFromPrompt(
    "Summarize this document: {{$input}}",
    new OpenAIPromptExecutionSettings 
    { 
        Temperature = 0.7,
        MaxTokens = 500
    });

var result = await kernel.InvokeAsync(function, new() { ["input"] = document });
```

### RAG Implementation
```csharp
// Vector search with Azure AI Search
var searchClient = new SearchClient(endpoint, indexName, credential);

var vectorQuery = new VectorizedQuery(queryEmbedding)
{
    KNearestNeighborsCount = 5,
    Fields = { "contentVector" }
};

var searchResults = await searchClient.SearchAsync<Document>(
    searchText: null,
    new SearchOptions { VectorSearch = new() { Queries = { vectorQuery } } });
```

### Agentic RAG with Self-Reflection
```csharp
// Multi-step retrieval with query refinement
public async Task<string> AgenticRAG(string query)
{
    // Step 1: Initial retrieval
    var docs = await RetrieveDocuments(query);
    
    // Step 2: Generate answer
    var answer = await GenerateAnswer(query, docs);
    
    // Step 3: Self-reflection - is answer sufficient?
    var isSufficient = await EvaluateAnswer(query, answer);
    
    if (!isSufficient)
    {
        // Step 4: Refine query and retry
        var refinedQuery = await RefineQuery(query, answer);
        docs = await RetrieveDocuments(refinedQuery);
        answer = await GenerateAnswer(refinedQuery, docs);
    }
    
    return answer;
}
```

### Tool/Function Definition for LLMs
```typescript
// MCP tool schema
{
  name: "search_documents",
  description: "Search through documentation using semantic search. Returns the 5 most relevant documents.",
  inputSchema: {
    type: "object",
    properties: {
      query: {
        type: "string",
        description: "The search query in natural language"
      },
      filters: {
        type: "object",
        description: "Optional filters for document type, date range, etc."
      }
    },
    required: ["query"]
  }
}
```

### Workflow Orchestration (Saga Pattern)
```csharp
public class DocumentProcessingSaga : Saga<DocumentProcessingState>
{
    public void Configure()
    {
        Initially(
            When(DocumentUploaded)
                .Then(ctx => ExtractText(ctx.Message))
                .TransitionTo(TextExtracted));
                
        During(TextExtracted,
            When(TextExtracted)
                .Then(ctx => GenerateEmbeddings(ctx.Message))
                .TransitionTo(EmbeddingsGenerated));
                
        During(EmbeddingsGenerated,
            When(EmbeddingsGenerated)
                .Then(ctx => IndexDocument(ctx.Message))
                .TransitionTo(Completed));
    }
}
```

## Architecture Patterns

### Agent Architecture
```
User Query
    ↓
Planning Agent (decides which tools to use)
    ↓
Tool Execution (search, code, calculate)
    ↓
Synthesis Agent (combines results)
    ↓
Response
```

### RAG Pipeline
```
Query → Embedding → Vector Search → Context Retrieval → LLM Generation → Response
         ↓                                                      ↑
    Query Rewriting ←------- Insufficient Answer? ------------┘
```

## Success Criteria
- [ ] Tool schemas are clear and complete
- [ ] Error messages guide toward solutions
- [ ] Stateless design (no session state)
- [ ] Proper authentication/authorization
- [ ] Rate limiting implemented
- [ ] Comprehensive logging
- [ ] Token usage optimization
- [ ] Fallback strategies for failures
- [ ] Human-in-the-loop for critical decisions

## Azure OpenAI Best Practices
- Use PTUs for production workloads
- Implement retry with exponential backoff
- Monitor token usage and costs
- Use structured outputs when possible
- Implement caching for repeated queries
- Use streaming for better UX
- Set appropriate temperature (0-0.3 for factual, 0.7-1.0 for creative)

## Testing AI Systems
- Unit tests for individual components
- Integration tests with mocked LLM responses
- End-to-end tests with real API (sparingly)
- Evaluation metrics (accuracy, relevance, hallucination rate)
- Human evaluation for quality
- A/B testing for improvements

## Common Pitfalls to Avoid
- ❌ No error handling for API failures
- ❌ Not handling rate limits
- ❌ Storing API keys in code
- ❌ No token usage monitoring
- ❌ Synchronous calls blocking UI
- ❌ No fallback when LLM unavailable
- ❌ Not validating LLM outputs
- ❌ Ignoring hallucination risks

## Next Steps After Implementation
1. Test with various inputs (edge cases)
2. Measure response quality
3. Monitor token usage and costs
4. Implement caching strategy
5. Add telemetry and logging
6. Document prompts and expected outputs
7. Set up A/B testing for improvements

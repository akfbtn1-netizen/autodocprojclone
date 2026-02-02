---
name: azure-openai-integration
description: Comprehensive Azure OpenAI integration patterns for enterprise .NET applications. Covers GPT-5/o-series models, Foundry Agent Service, Semantic Kernel, RAG, realtime audio, structured outputs, function calling, MCP protocol, PTU optimization, and VNet security. Use when integrating Azure OpenAI, building AI agents, implementing RAG systems, or using Semantic Kernel. December 2025 current.
license: MIT
---

# Azure OpenAI Integration Patterns Skill

Enterprise-grade Azure OpenAI integration patterns for .NET applications, covering the complete December 2025 feature landscape including GPT-5 models, Azure AI Foundry Agent Service, and advanced orchestration patterns.

## When to Use This Skill

Activate when working with:
- Azure OpenAI API integration (.NET, Python)
- GPT-5, GPT-4.1, o-series reasoning models
- Azure AI Foundry and Agent Service
- Semantic Kernel orchestration
- RAG implementations with Azure AI Search
- Realtime audio/voice APIs
- Structured outputs and function calling
- Model Context Protocol (MCP)
- Provisioned Throughput (PTU) optimization
- Enterprise security and VNet configuration

## Quick Reference: December 2025 Model Landscape

### GPT-5 Family (GA)
| Model | Use Case | Context | Notes |
|-------|----------|---------|-------|
| gpt-5 | Logic-heavy, multi-step tasks | 128k | Registration required |
| gpt-5-mini | Cost-sensitive applications | 128k | No registration |
| gpt-5-nano | Speed-optimized, low-latency | 128k | Lowest cost |
| gpt-5-chat | Multimodal conversations | 128k | Text + image |
| gpt-5-codex | Multimodal coding | 128k | Repo-aware, async |
| gpt-5.2 | Reasoning-intensive enterprise | 128k | December 2025 release |

### O-Series Reasoning Models
| Model | Best For | Notes |
|-------|----------|-------|
| o3 | Complex reasoning, research | Registration required |
| o4-mini | RFT fine-tuning, adaptive reasoning | Fast inference |
| o1 | Multi-step planning | Stable production |

### GPT-4.1 Family
| Model | Use Case | Training Support |
|-------|----------|------------------|
| gpt-4.1-2025-04-14 | Enterprise reasoning | SFT available |
| gpt-4.1-mini | Balanced cost/performance | SFT available |
| gpt-4.1-nano | High-volume, low-latency | SFT + distillation |

## .NET SDK Integration

### Package References
```xml
<PackageReference Include="Azure.AI.OpenAI" Version="2.1.0" />
<PackageReference Include="Azure.Identity" Version="1.13.1" />
<PackageReference Include="Microsoft.Extensions.AI.OpenAI" Version="9.3.0-preview" />
<PackageReference Include="Microsoft.SemanticKernel" Version="1.30.0" />
```

### Authentication Patterns

#### Entra ID (Recommended for Production)
```csharp
using Azure.AI.OpenAI;
using Azure.Identity;

// DefaultAzureCredential for production
var credential = new DefaultAzureCredential();
var client = new AzureOpenAIClient(
    new Uri(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")!),
    credential
);

// Get chat client for specific deployment
var chatClient = client.GetChatClient("gpt-5-deployment");

// Complete a chat request
var response = await chatClient.CompleteChatAsync(
    new List<ChatMessage>
    {
        new SystemChatMessage("You are an expert assistant."),
        new UserChatMessage("Explain CQRS pattern in .NET")
    },
    new ChatCompletionOptions
    {
        MaxOutputTokenCount = 2048,
        Temperature = 0.7f
    }
);

Console.WriteLine(response.Value.Content[0].Text);
```

#### API Key Authentication
```csharp
using Azure.AI.OpenAI;
using System.ClientModel;

var client = new AzureOpenAIClient(
    new Uri(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")!),
    new ApiKeyCredential(Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")!)
);
```

#### v1 API with OpenAI SDK Compatibility
```csharp
using OpenAI;
using Azure.Identity;

// New v1 API endpoint (August 2025+)
// Endpoint: https://YOUR-RESOURCE.openai.azure.com/openai/v1/
var client = new OpenAIClient(
    new BearerTokenPolicy(
        new DefaultAzureCredential(),
        "https://ai.azure.com/.default"
    ),
    new OpenAIClientOptions
    {
        Endpoint = new Uri($"{endpoint}/openai/v1/")
    }
);
```

### Streaming Responses
```csharp
public async IAsyncEnumerable<string> StreamChatAsync(string prompt)
{
    var chatClient = _client.GetChatClient("gpt-5-deployment");
    
    await foreach (var update in chatClient.CompleteChatStreamingAsync(
        new List<ChatMessage>
        {
            new UserChatMessage(prompt)
        }))
    {
        foreach (var part in update.ContentUpdate)
        {
            yield return part.Text;
        }
    }
}
```

### Dependency Injection Pattern
```csharp
public static class AzureOpenAIServiceExtensions
{
    public static IServiceCollection AddAzureOpenAI(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton(sp =>
        {
            var endpoint = configuration["AzureOpenAI:Endpoint"]
                ?? throw new InvalidOperationException("AzureOpenAI:Endpoint not configured");
            
            return new AzureOpenAIClient(
                new Uri(endpoint),
                new DefaultAzureCredential()
            );
        });
        
        return services;
    }
}
```

## Structured Outputs

### JSON Schema Response Format
```csharp
public class ProductAnalysis
{
    public string ProductName { get; set; } = string.Empty;
    public List<string> Features { get; set; } = new();
    public decimal PriceEstimate { get; set; }
    public string MarketSegment { get; set; } = string.Empty;
}

var options = new ChatCompletionOptions
{
    ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
        "product_analysis",
        BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "productName": { "type": "string" },
                "features": { 
                    "type": "array", 
                    "items": { "type": "string" } 
                },
                "priceEstimate": { "type": "number" },
                "marketSegment": { 
                    "type": "string",
                    "enum": ["consumer", "enterprise", "prosumer"]
                }
            },
            "required": ["productName", "features", "priceEstimate", "marketSegment"],
            "additionalProperties": false
        }
        """),
        jsonSchemaIsStrict: true
    )
};

var response = await chatClient.CompleteChatAsync(messages, options);
var analysis = JsonSerializer.Deserialize<ProductAnalysis>(
    response.Value.Content[0].Text
);
```

### Structured Output Constraints
- Root must be an object type
- All fields must be marked as required
- No `anyOf` at top level
- Use `additionalProperties: false` for strict mode
- Supported types: string, number, integer, boolean, null, array, object, enum

## Function Calling / Tools

### Basic Tool Definition
```csharp
var tools = new List<ChatTool>
{
    ChatTool.CreateFunctionTool(
        "search_database",
        "Search the company database for records",
        BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "query": { 
                    "type": "string", 
                    "description": "Search query" 
                },
                "table": { 
                    "type": "string",
                    "enum": ["customers", "orders", "products"]
                },
                "limit": { 
                    "type": "integer", 
                    "default": 10 
                }
            },
            "required": ["query", "table"]
        }
        """)
    )
};

var options = new ChatCompletionOptions
{
    Tools = { tools[0] },
    ToolChoice = ChatToolChoice.CreateAutoChoice()
};
```

### Processing Tool Calls
```csharp
public async Task<string> ProcessWithToolsAsync(string userMessage)
{
    var messages = new List<ChatMessage>
    {
        new UserChatMessage(userMessage)
    };
    
    var response = await _chatClient.CompleteChatAsync(messages, _toolOptions);
    
    while (response.Value.FinishReason == ChatFinishReason.ToolCalls)
    {
        var assistantMessage = new AssistantChatMessage(response.Value);
        messages.Add(assistantMessage);
        
        foreach (var toolCall in response.Value.ToolCalls)
        {
            var result = await ExecuteToolAsync(toolCall);
            messages.Add(new ToolChatMessage(toolCall.Id, result));
        }
        
        response = await _chatClient.CompleteChatAsync(messages, _toolOptions);
    }
    
    return response.Value.Content[0].Text;
}

private async Task<string> ExecuteToolAsync(ChatToolCall toolCall)
{
    return toolCall.FunctionName switch
    {
        "search_database" => await SearchDatabaseAsync(
            JsonSerializer.Deserialize<SearchRequest>(toolCall.FunctionArguments)!
        ),
        "get_weather" => await GetWeatherAsync(
            JsonSerializer.Deserialize<WeatherRequest>(toolCall.FunctionArguments)!
        ),
        _ => throw new ArgumentException($"Unknown tool: {toolCall.FunctionName}")
    };
}
```

### Strict Schema Validation
```csharp
var options = new ChatCompletionOptions
{
    Tools = { tool },
    ToolChoice = ChatToolChoice.CreateRequiredChoice(),
    // Enable strict schema validation
    AllowParallelToolCalls = false
};

// For strict function schemas, set in tool definition:
ChatTool.CreateFunctionTool(
    name: "calculate",
    description: "Perform calculation",
    parameters: schema,
    functionSchemaIsStrict: true  // Enforces schema adherence
);
```

## RAG Implementation Patterns

### Agentic Retrieval (Preview)
```csharp
// Azure AI Search + Agentic Retrieval
public class AgenticRagService
{
    private readonly AzureOpenAIClient _openAiClient;
    private readonly SearchClient _searchClient;
    
    public async Task<string> QueryWithRagAsync(
        string question,
        List<ChatMessage> conversationHistory)
    {
        // Step 1: Query planning with LLM
        var planningPrompt = $"""
            Given the conversation and question, generate optimized search queries.
            Question: {question}
            Return JSON: {{"queries": ["query1", "query2"]}}
            """;
        
        var queryPlan = await GenerateQueryPlanAsync(planningPrompt);
        
        // Step 2: Parallel search execution
        var searchTasks = queryPlan.Queries
            .Select(q => ExecuteHybridSearchAsync(q));
        var allResults = await Task.WhenAll(searchTasks);
        
        // Step 3: Rerank and deduplicate
        var rankedDocs = await RerankResultsAsync(allResults.SelectMany(r => r));
        
        // Step 4: Generate grounded response
        return await GenerateGroundedResponseAsync(question, rankedDocs);
    }
    
    private async Task<List<SearchResult>> ExecuteHybridSearchAsync(string query)
    {
        var options = new SearchOptions
        {
            QueryType = SearchQueryType.Semantic,
            SemanticSearch = new SemanticSearchOptions
            {
                SemanticConfigurationName = "default",
                QueryCaption = new QueryCaption(QueryCaptionType.Extractive),
                QueryAnswer = new QueryAnswer(QueryAnswerType.Extractive)
            },
            VectorSearch = new VectorSearchOptions
            {
                Queries = { new VectorizedQuery(await GetEmbeddingAsync(query)) }
            },
            Size = 10
        };
        
        var response = await _searchClient.SearchAsync<SearchDocument>(query, options);
        return response.Value.GetResults().ToList();
    }
}
```

### Azure OpenAI On Your Data
```csharp
var dataSource = new AzureSearchChatDataSource
{
    Endpoint = new Uri(Environment.GetEnvironmentVariable("AZURE_SEARCH_ENDPOINT")!),
    IndexName = "documents-index",
    Authentication = DataSourceAuthentication.FromApiKey(
        Environment.GetEnvironmentVariable("AZURE_SEARCH_KEY")!
    ),
    QueryType = DataSourceQueryType.VectorSemanticHybrid,
    VectorizationSource = DataSourceVectorizer.FromDeploymentName("text-embedding-ada-002"),
    Strictness = 3,  // 1-5 scale
    TopNDocuments = 5,
    InScope = true
};

var options = new ChatCompletionOptions
{
    DataSources = { dataSource }
};

var response = await chatClient.CompleteChatAsync(messages, options);

// Access citations
foreach (var citation in response.Value.Citations)
{
    Console.WriteLine($"Source: {citation.Title} - {citation.Url}");
}
```

## Semantic Kernel Integration

### Basic Setup
```csharp
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

var builder = Kernel.CreateBuilder();

builder.AddAzureOpenAIChatCompletion(
    deploymentName: "gpt-5-deployment",
    endpoint: Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")!,
    credentials: new DefaultAzureCredential()
);

var kernel = builder.Build();

// Simple completion
var response = await kernel.InvokePromptAsync(
    "Summarize the key benefits of microservices architecture"
);
```

### Plugin System
```csharp
public class DatabasePlugin
{
    private readonly IDbContext _context;
    
    public DatabasePlugin(IDbContext context) => _context = context;
    
    [KernelFunction("search_customers")]
    [Description("Search for customers by name or email")]
    public async Task<List<Customer>> SearchCustomersAsync(
        [Description("Search query")] string query,
        [Description("Maximum results")] int limit = 10)
    {
        return await _context.Customers
            .Where(c => c.Name.Contains(query) || c.Email.Contains(query))
            .Take(limit)
            .ToListAsync();
    }
    
    [KernelFunction("get_order_history")]
    [Description("Get order history for a customer")]
    public async Task<List<Order>> GetOrderHistoryAsync(
        [Description("Customer ID")] int customerId)
    {
        return await _context.Orders
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();
    }
}

// Register plugin
kernel.Plugins.AddFromObject(new DatabasePlugin(dbContext), "Database");

// Auto-invoke functions
var settings = new PromptExecutionSettings
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};

var response = await kernel.InvokePromptAsync(
    "Find customers named John and show their recent orders",
    new KernelArguments(settings)
);
```

### Multi-Agent Chat Pattern
```csharp
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;

// Create specialized agents
var analystAgent = new ChatCompletionAgent
{
    Name = "DataAnalyst",
    Instructions = "You analyze data and provide insights.",
    Kernel = kernel
};

var writerAgent = new ChatCompletionAgent
{
    Name = "ReportWriter",
    Instructions = "You write clear, professional reports.",
    Kernel = kernel
};

// Create agent group chat
var chat = new AgentGroupChat(analystAgent, writerAgent)
{
    ExecutionSettings = new AgentGroupChatSettings
    {
        TerminationStrategy = new MaximumIterationsTerminationStrategy(10)
    }
};

await chat.AddChatMessageAsync(new ChatMessageContent(
    AuthorRole.User,
    "Analyze Q4 sales data and create an executive summary"
));

await foreach (var message in chat.InvokeAsync())
{
    Console.WriteLine($"{message.AuthorName}: {message.Content}");
}
```

## Azure AI Foundry Agent Service

### Agent Creation
```csharp
using Azure.AI.Projects;

var connectionString = Environment.GetEnvironmentVariable("AZURE_AI_PROJECT_CONNECTION")!;
var projectClient = new AIProjectClient(connectionString, new DefaultAzureCredential());
var agentsClient = projectClient.GetAgentsClient();

// Create agent with tools
var agent = await agentsClient.CreateAgentAsync(
    model: "gpt-5",
    name: "ResearchAssistant",
    instructions: """
        You are a research assistant that helps analyze documents and search for information.
        Use the provided tools to gather data before formulating responses.
        Always cite your sources.
        """,
    tools: new List<ToolDefinition>
    {
        new CodeInterpreterToolDefinition(),
        new FileSearchToolDefinition(),
        new FunctionToolDefinition(
            name: "search_web",
            description: "Search the web for current information",
            parameters: BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "query": { "type": "string" }
                    },
                    "required": ["query"]
                }
                """)
        )
    }
);
```

### Thread and Run Management
```csharp
// Create thread
var thread = await agentsClient.CreateThreadAsync();

// Add user message
await agentsClient.CreateMessageAsync(
    thread.Value.Id,
    MessageRole.User,
    "Analyze the uploaded Q3 financial report and compare with industry benchmarks"
);

// Run agent
var run = await agentsClient.CreateRunAsync(
    thread.Value.Id,
    agent.Value.Id
);

// Poll for completion with tool handling
while (run.Value.Status == RunStatus.InProgress || 
       run.Value.Status == RunStatus.RequiresAction)
{
    await Task.Delay(1000);
    run = await agentsClient.GetRunAsync(thread.Value.Id, run.Value.Id);
    
    if (run.Value.Status == RunStatus.RequiresAction)
    {
        var toolOutputs = await ProcessToolCallsAsync(run.Value.RequiredAction);
        run = await agentsClient.SubmitToolOutputsToRunAsync(
            thread.Value.Id,
            run.Value.Id,
            toolOutputs
        );
    }
}

// Get messages
var messages = await agentsClient.GetMessagesAsync(thread.Value.Id);
```

### Deep Research Tool Integration
```csharp
// Agent with Deep Research (uses o3-deep-research + Bing)
var researchAgent = await agentsClient.CreateAgentAsync(
    model: "gpt-5",
    name: "DeepResearchAgent",
    instructions: "Conduct thorough research on topics using web search.",
    tools: new List<ToolDefinition>
    {
        new BingGroundingToolDefinition()  // Deep Research capability
    }
);
```

## Realtime Audio API

### WebSocket Connection (Server-Side)
```csharp
using System.Net.WebSockets;
using System.Text.Json;

public class RealtimeAudioService
{
    private ClientWebSocket _webSocket;
    private readonly string _endpoint;
    private readonly string _apiKey;
    
    public async Task ConnectAsync()
    {
        _webSocket = new ClientWebSocket();
        _webSocket.Options.SetRequestHeader("api-key", _apiKey);
        
        var uri = new Uri($"{_endpoint}/openai/realtime?api-version=2024-12-17&deployment=gpt-realtime");
        await _webSocket.ConnectAsync(uri, CancellationToken.None);
        
        // Configure session
        await SendEventAsync(new
        {
            type = "session.update",
            session = new
            {
                modalities = new[] { "text", "audio" },
                instructions = "You are a helpful voice assistant. Speak clearly and professionally.",
                voice = "alloy",
                input_audio_format = "pcm16",
                output_audio_format = "pcm16",
                turn_detection = new
                {
                    type = "server_vad",
                    threshold = 0.5,
                    prefix_padding_ms = 300,
                    silence_duration_ms = 500
                }
            }
        });
    }
    
    public async Task SendAudioAsync(byte[] audioData)
    {
        var base64Audio = Convert.ToBase64String(audioData);
        await SendEventAsync(new
        {
            type = "input_audio_buffer.append",
            audio = base64Audio
        });
    }
    
    public async IAsyncEnumerable<RealtimeEvent> ReceiveEventsAsync()
    {
        var buffer = new byte[8192];
        
        while (_webSocket.State == WebSocketState.Open)
        {
            var result = await _webSocket.ReceiveAsync(buffer, CancellationToken.None);
            var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
            var evt = JsonSerializer.Deserialize<RealtimeEvent>(json);
            yield return evt!;
        }
    }
}
```

### Voice Live API (Managed Pipeline)
```csharp
// Voice Live API provides fully managed STT + LLM + TTS + Avatar
// 140+ locales, 600+ voices, noise suppression built-in

var voiceLiveConfig = new VoiceLiveConfiguration
{
    SpeechRecognition = new SpeechConfig
    {
        Language = "en-US",
        EnablePunctuation = true
    },
    Language = new LanguageModelConfig
    {
        DeploymentName = "gpt-5-chat",
        SystemPrompt = "You are a customer service representative."
    },
    TextToSpeech = new TtsConfig
    {
        Voice = "en-US-JennyNeural",
        Style = "customerservice",
        Rate = 1.0f
    },
    EnableNoiseSuppression = true,
    EnableEchoCancellation = true
};
```

## Batch API

### Creating Batch Requests
```csharp
public class BatchProcessor
{
    private readonly AzureOpenAIClient _client;
    
    public async Task<string> CreateBatchAsync(List<BatchRequest> requests)
    {
        // Create JSONL file content
        var jsonlContent = string.Join("\n", requests.Select(r => 
            JsonSerializer.Serialize(new
            {
                custom_id = r.CustomId,
                method = "POST",
                url = "/chat/completions",
                body = new
                {
                    model = "gpt-5-deployment",
                    messages = r.Messages,
                    max_tokens = r.MaxTokens
                }
            })
        ));
        
        // Upload file
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonlContent));
        var fileResponse = await _client.GetFileClient().UploadFileAsync(
            stream,
            "batch_requests.jsonl",
            FilePurpose.Batch
        );
        
        // Create batch
        var batchResponse = await CreateBatchJobAsync(fileResponse.Value.Id);
        return batchResponse.Id;
    }
    
    public async Task<BatchStatus> CheckBatchStatusAsync(string batchId)
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("api-key", _apiKey);
        
        var response = await httpClient.GetAsync(
            $"{_endpoint}/openai/batches/{batchId}?api-version=2025-03-01-preview"
        );
        
        return await response.Content.ReadFromJsonAsync<BatchStatus>();
    }
}

// Batch API Benefits:
// - 50% cost reduction vs standard
// - 24-hour target turnaround
// - Separate quota (doesn't impact real-time workloads)
// - Up to 50,000 requests per batch
// - Up to 200 MB per file
```

## Prompt Caching

### Automatic Caching (Enabled by Default)
```csharp
// Prompt caching is automatic for prompts ≥1024 tokens
// Structure requests with static content first

var messages = new List<ChatMessage>
{
    // Static content at beginning (gets cached)
    new SystemChatMessage("""
        You are an expert documentation assistant for enterprise software.
        
        ## Documentation Standards
        - Use clear, professional language
        - Include code examples where appropriate
        - Follow ISO 11179 naming conventions
        - Reference official documentation
        
        ## Response Format
        - Start with executive summary
        - Provide detailed explanation
        - Include implementation examples
        - End with best practices
        
        [... additional static instructions ...]
        """),
    
    // Dynamic content at end (not cached)
    new UserChatMessage($"Document the following stored procedure: {procedureCode}")
};

// Check cache usage in response
var response = await chatClient.CompleteChatAsync(messages);
var cachedTokens = response.Value.Usage.PromptTokenDetails?.CachedTokens ?? 0;
Console.WriteLine($"Cached tokens: {cachedTokens}");
```

### Prompt Cache Key for Improved Hits
```csharp
// Use prompt_cache_key for consistent routing
var options = new ChatCompletionOptions
{
    PromptCacheKey = "documentation-template-v1"  // Influences routing
};

// Cache considerations:
// - First 1024 tokens must be identical for cache hit
// - After 1024, cache in 128-token increments
// - Cleared after 5-10 min inactivity, max 1 hour
// - Keep request rate <15/min per prefix+key combo
```

## Fine-Tuning Patterns

### Supervised Fine-Tuning (SFT)
```csharp
// Training data format (JSONL)
var trainingExamples = new List<object>
{
    new
    {
        messages = new[]
        {
            new { role = "system", content = "You are a SQL expert." },
            new { role = "user", content = "Write a query to find top customers" },
            new { role = "assistant", content = "SELECT TOP 10 c.Name, SUM(o.Total) ..." }
        }
    }
};

// Cost estimation:
// price = training_tokens × epochs × price_per_token
// Global training: 10-30% discount vs regional
// Developer tier: 50% discount, spot capacity
```

### Reinforcement Fine-Tuning (RFT)
```csharp
// RFT for o4-mini - aligns model with complex business logic
// Rewards accurate reasoning, penalizes undesirable outputs

// RFT Observability (July 2025+):
// - Automatic evaluation job created when RFT starts
// - Monitor prompts/responses/grades at each checkpoint
// - Debug and steer training in real-time
// - Reduces wasted time/budget from wrong grader selection
```

### Fine-Tuning Best Practices
```
Hub/Spoke Architecture:
├── Hub (Training Resource)
│   ├── Data scientists with controlled access
│   ├── Training data in secure storage
│   └── Model training and evaluation
├── Spoke (Production Resources)
│   ├── Deployed fine-tuned models
│   ├── Multi-region deployment
│   └── Cross-tenant support available
└── Pipeline
    ├── Data preparation and validation
    ├── Training in Hub
    ├── Evaluation
    └── Promotion to Spokes

Key Considerations:
- 15-day inactivity deletion policy
- Hourly hosting cost regardless of usage
- Developer tier for evaluation (no hourly fee, 24hr limit)
- Extended support: 12 months past retirement for deployed models
```

## Provisioned Throughput (PTU)

### PTU Configuration
```csharp
// PTU for predictable, high-throughput workloads
// - Hourly billing (prorated by minute)
// - Model-independent quota
// - 1-month or 1-year reservations (up to 70% savings)

public class PtuOptimizationService
{
    public PtuRecommendation CalculatePtu(WorkloadMetrics metrics)
    {
        // Use Azure PTU Calculator for accurate estimates
        // Factors: calls/min, avg input tokens, avg output tokens
        
        return new PtuRecommendation
        {
            BasePtu = metrics.PeakCallsPerMinute * metrics.AvgTotalTokens / 1000,
            RecommendedBuffer = 1.2, // 20% headroom
            Strategy = metrics.TrafficVariability > 0.3
                ? DeploymentStrategy.HybridPtuPayg
                : DeploymentStrategy.PtuOnly
        };
    }
}

// Hybrid Strategy Pattern:
// - PTUs for baseline/regular load
// - Spillover to pay-as-you-go for peaks
// - Dynamic spillover feature prevents 429 errors
```

### PTU Monitoring
```csharp
// Monitor with Provisioned-Managed Utilization V2 metric
// Alerts on 429 errors indicate capacity issues

var metricsQuery = new MetricsQueryClient(new DefaultAzureCredential());
var response = await metricsQuery.QueryResourceAsync(
    resourceId,
    new[] { "ProvisionedManagedUtilizationV2" },
    new MetricsQueryOptions
    {
        Granularity = TimeSpan.FromMinutes(1),
        TimeRange = new QueryTimeRange(TimeSpan.FromHours(1))
    }
);
```

## Cost Optimization Strategies

### Token Management
```csharp
public class TokenOptimizationService
{
    // 1. Model Selection
    public string SelectOptimalModel(TaskType task)
    {
        return task switch
        {
            TaskType.SimpleClassification => "gpt-5-nano",      // $0.05/1M
            TaskType.ContentGeneration => "gpt-5-mini",         // $0.25/1M
            TaskType.ComplexReasoning => "gpt-5",               // $1.25/1M
            TaskType.CodeGeneration => "gpt-5-codex",
            _ => "gpt-5-mini"
        };
    }
    
    // 2. Prompt Compression
    public string CompressSystemPrompt(string prompt)
    {
        // Remove redundant instructions
        // Use abbreviations where clear
        // Remove examples if model performs well without
        // Target: 30-50% token reduction
        return OptimizePrompt(prompt);
    }
    
    // 3. Response Length Control
    public int CalculateMaxTokens(TaskType task)
    {
        return task switch
        {
            TaskType.YesNo => 10,
            TaskType.ShortAnswer => 100,
            TaskType.Explanation => 500,
            TaskType.DetailedAnalysis => 2000,
            _ => 500
        };
    }
    
    // 4. Batch Processing
    public bool ShouldUseBatch(List<Request> requests)
    {
        // Use batch for non-interactive, high-volume tasks
        // 50% cost savings, 24hr turnaround
        return requests.Count > 100 && !IsTimeConsensitive;
    }
}
```

### Cost Monitoring
```csharp
// FinOps integration with Azure Cost Management
public class AiCostMonitor
{
    public async Task SetupAlertsAsync()
    {
        // Budget thresholds
        await CreateBudgetAlert("openai-monthly", 10000, 0.8);
        
        // Anomaly detection
        await CreateAnomalyAlert("token-usage-spike", 2.0); // 2x normal
        
        // Per-request cost tracking middleware
        // Tag requests by: user, feature, project
    }
}
```

## MCP (Model Context Protocol) Integration

### MCP Server Implementation
```csharp
// Azure AI Foundry supports MCP for dynamic tool discovery
// Donated to Agentic AI Foundation (Linux Foundation) Nov 2025

public class McpServerConfiguration
{
    public string Name { get; set; } = "enterprise-data-server";
    public string Transport { get; set; } = "http"; // or "stdio"
    public List<McpTool> Tools { get; set; } = new();
    public List<McpResource> Resources { get; set; } = new();
}

// Available Azure MCP Servers:
// - CosmosDB, SQL Server, SharePoint, Bing, Fabric
// - 1,400+ connectors via Logic Apps

// Session configuration for Realtime API
var sessionConfig = new
{
    mcp_servers = new[]
    {
        new { url = "https://mcp.enterprise.com/data" }
    }
};
```

## Security and Networking

### Private Endpoint Configuration
```csharp
// VNet isolation with private endpoints
// Traffic stays on Azure backbone

/*
Architecture:
┌─────────────────────────────────────────────────────────┐
│ Virtual Network                                         │
│  ┌─────────────────┐    ┌─────────────────────────────┐│
│  │ App Service     │───▶│ Private Endpoint            ││
│  │ (VNet Integrated)│    │ (Azure OpenAI)              ││
│  └─────────────────┘    └─────────────────────────────┘│
│           │                        │                    │
│           ▼                        ▼                    │
│  ┌─────────────────┐    ┌─────────────────────────────┐│
│  │ Private DNS Zone│    │ Azure OpenAI Service        ││
│  │ (privatelink.   │    │ (Public disabled)           ││
│  │  openai.azure   │    │                             ││
│  │  .com)          │    │                             ││
│  └─────────────────┘    └─────────────────────────────┘│
└─────────────────────────────────────────────────────────┘
*/

// Configuration steps:
// 1. Create VNet in same region as OpenAI resource
// 2. Disable public network access on OpenAI resource
// 3. Create private endpoint in VNet subnet
// 4. Configure Private DNS Zone
// 5. Update NSGs for traffic control
// 6. Custom subdomain required for private endpoint requests
```

### APIM Integration
```csharp
// Azure API Management for enterprise gateway
public class ApimConfiguration
{
    // Policies for Azure OpenAI
    public string GetApimPolicy()
    {
        return """
            <policies>
                <inbound>
                    <authentication-managed-identity 
                        resource="https://cognitiveservices.azure.com/" />
                    <set-backend-service 
                        backend-id="azure-openai-backend" />
                    <rate-limit-by-key 
                        calls="100" 
                        renewal-period="60" 
                        counter-key="@(context.Subscription.Id)" />
                    <azure-openai-token-limit
                        tokens-per-minute="100000"
                        counter-key="@(context.Subscription.Id)" />
                </inbound>
                <outbound>
                    <azure-openai-emit-token-metric
                        namespace="Azure.OpenAI.Usage" />
                </outbound>
            </policies>
            """;
    }
}
```

### Security Checklist
```
□ Enable Managed Identity (RBAC for service-to-service)
□ Configure Private Endpoints
□ Set up Private DNS Zones
□ Disable public network access
□ Configure NSGs for traffic control
□ Enable Azure DDoS Protection
□ Apply Azure Policy (restrict models, context length)
□ Configure monitoring (Azure Monitor, Log Analytics)
□ Implement data anonymization before inference
□ Verify compliance (GDPR, HIPAA, ISO 27001, SOC 2)
```

## Deployment Types Comparison

| Type | Use Case | Cost | Data Residency |
|------|----------|------|----------------|
| Global Standard | Cost-optimized | Lowest | At rest only |
| Data Zone | EU/US compliance | Medium | Zone-level |
| Regional | Full residency | Highest | Full regional |
| Global Batch | Async processing | 50% off | At rest only |

## API Version Strategy

```csharp
// New v1 API (August 2025+)
// - No more monthly api-version updates
// - Use "latest" or "preview" instead
// - OpenAI SDK compatibility

public class ApiVersionStrategy
{
    // Production: Use stable "latest"
    public const string Production = "latest";
    
    // Preview features: Use "preview"  
    public const string Preview = "preview";
    
    // Legacy compatibility
    public const string Legacy = "2025-03-01-preview";
}
```

## Error Handling Patterns

```csharp
public class ResilientOpenAiClient
{
    private readonly AzureOpenAIClient _client;
    private readonly ResiliencePipeline _pipeline;
    
    public ResilientOpenAiClient(AzureOpenAIClient client)
    {
        _client = client;
        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder()
                    .Handle<RequestFailedException>(ex => 
                        ex.Status == 429 || // Rate limited
                        ex.Status == 500 || // Server error
                        ex.Status == 503)   // Service unavailable
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 10,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30)
            })
            .Build();
    }
    
    public async Task<ChatCompletion> CompleteChatAsync(
        List<ChatMessage> messages,
        CancellationToken ct = default)
    {
        return await _pipeline.ExecuteAsync(async token =>
        {
            var chatClient = _client.GetChatClient("gpt-5");
            var response = await chatClient.CompleteChatAsync(messages);
            return response.Value;
        }, ct);
    }
}
```

## Sample Repositories

| Repository | Description |
|------------|-------------|
| azure-search-openai-demo | RAG chat app (Python/JS/.NET/Java) |
| aisearch-openai-rag-audio | VoiceRAG implementation |
| openai-dotnet-samples | .NET examples |
| semantic-kernel-workshop | SK learning path |
| GPT-RAG | Zero-Trust RAG with AI agents |
| remote-mcp-apim-functions-python | MCP server example |
| aoai-apim | Scaling with APIM, PTUs |

## Quick Decision Trees

### Model Selection
```
Need reasoning? → o-series (o3, o4-mini)
Need speed? → gpt-5-nano
Need balance? → gpt-5-mini
Need maximum capability? → gpt-5 or gpt-5.2
Need code? → gpt-5-codex
Need voice? → gpt-realtime
```

### Deployment Type
```
Compliance requirement?
├── Yes, EU/US → Data Zone
├── Yes, regional → Regional
└── No → Global (lowest cost)

High throughput, predictable?
├── Yes → Provisioned (PTU)
└── No → Standard (pay-as-you-go)

Async, high volume?
└── Yes → Batch (50% off)
```

### Cost Optimization Priority
```
1. Right-size model selection
2. Enable prompt caching (automatic)
3. Use batch for async workloads
4. Implement semantic caching (APIM)
5. Fine-tune for domain tasks
6. PTU for predictable workloads
```

## References

- [Azure OpenAI Documentation](https://learn.microsoft.com/azure/ai-services/openai/)
- [Azure AI Foundry](https://learn.microsoft.com/azure/ai-foundry/)
- [Semantic Kernel](https://github.com/microsoft/semantic-kernel)
- [MCP Specification](https://modelcontextprotocol.io)
- [OpenAI API Reference](https://platform.openai.com/docs/)
- [Azure OpenAI Pricing](https://azure.microsoft.com/pricing/details/cognitive-services/openai-service/)

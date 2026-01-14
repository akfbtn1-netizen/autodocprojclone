# Azure OpenAI Quick Reference - December 2025

## Model Selection Cheat Sheet

```
┌─────────────────────────────────────────────────────────────────┐
│ Task Type                    │ Recommended Model    │ Cost/1M   │
├─────────────────────────────────────────────────────────────────┤
│ Simple classification        │ gpt-5-nano          │ $0.05 in  │
│ Content generation           │ gpt-5-mini          │ $0.25 in  │
│ Complex reasoning            │ gpt-5 / gpt-5.2     │ $1.25 in  │
│ Multi-step planning          │ o3 / o4-mini        │ varies    │
│ Code generation              │ gpt-5-codex         │ $1.25 in  │
│ Voice/Audio                  │ gpt-realtime        │ per-sec   │
│ Batch processing (async)     │ Any (Batch API)     │ 50% off   │
└─────────────────────────────────────────────────────────────────┘
```

## Deployment Type Decision

```
┌─────────────────────────────────────────────────────────────────┐
│ Requirement                  │ Deployment Type     │ Notes      │
├─────────────────────────────────────────────────────────────────┤
│ Lowest cost                  │ Global Standard     │ Default    │
│ EU/US data zone              │ Data Zone           │ Compliance │
│ Full regional residency      │ Regional            │ Highest $  │
│ Predictable high throughput  │ PTU (Provisioned)   │ Commit $   │
│ Async bulk processing        │ Global Batch        │ 50% off    │
└─────────────────────────────────────────────────────────────────┘
```

## .NET SDK Quick Start

```csharp
// 1. Install packages
// Azure.AI.OpenAI, Azure.Identity

// 2. Create client (Entra ID - recommended)
var client = new AzureOpenAIClient(
    new Uri(endpoint),
    new DefaultAzureCredential()
);

// 3. Get chat client
var chatClient = client.GetChatClient("gpt-5-deployment");

// 4. Complete chat
var response = await chatClient.CompleteChatAsync(
    new List<ChatMessage>
    {
        new SystemChatMessage("You are helpful."),
        new UserChatMessage("Hello!")
    }
);

Console.WriteLine(response.Value.Content[0].Text);
```

## Structured Output Template

```csharp
var options = new ChatCompletionOptions
{
    ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
        "schema_name",
        BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "field1": { "type": "string" },
                "field2": { "type": "integer" }
            },
            "required": ["field1", "field2"],
            "additionalProperties": false
        }
        """),
        jsonSchemaIsStrict: true
    )
};
```

## Function Calling Template

```csharp
var tool = ChatTool.CreateFunctionTool(
    name: "function_name",
    description: "What it does",
    parameters: BinaryData.FromString("""
    {
        "type": "object",
        "properties": {
            "param1": { "type": "string", "description": "desc" }
        },
        "required": ["param1"]
    }
    """),
    functionSchemaIsStrict: true
);

var options = new ChatCompletionOptions
{
    Tools = { tool },
    ToolChoice = ChatToolChoice.CreateAutoChoice()
};
```

## Prompt Caching (Automatic)

```
Requirements for cache hits:
✓ Prompt ≥ 1024 tokens
✓ First 1024 tokens identical
✓ Same deployment
✓ Request within 5-10 min of previous

Benefits:
• Up to 90% input token discount (Standard)
• Up to 100% discount (Provisioned)
• Reduced latency

Structure prompts: Static content FIRST, dynamic content LAST
```

## Cost Optimization Priority

```
1. Right-size model (nano < mini < standard)
2. Enable prompt caching (automatic, structure prompts correctly)
3. Use Batch API for async workloads (50% off)
4. Implement semantic caching (APIM)
5. Fine-tune for domain tasks (reduce tokens)
6. PTU for predictable workloads (up to 70% savings with commitment)
```

## API Version Strategy

```
Production:   api-version=latest
Preview:      api-version=preview
Legacy:       api-version=2025-03-01-preview

New v1 API (August 2025+):
Endpoint: https://YOUR-RESOURCE.openai.azure.com/openai/v1/
```

## Security Checklist

```
□ Use Entra ID (DefaultAzureCredential)
□ Enable Private Endpoints
□ Configure Private DNS Zones
□ Disable public network access
□ Set up NSG rules
□ Enable Azure DDoS Protection
□ Apply Azure Policy constraints
□ Configure monitoring/alerting
```

## Common HTTP Status Codes

```
200 - Success
400 - Bad request (check schema)
401 - Authentication failed
403 - Authorization failed
404 - Deployment not found
429 - Rate limited (consider PTU)
500 - Server error (retry with backoff)
503 - Service unavailable (retry)
```

## Retry Policy Template

```csharp
var pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(1),
        BackoffType = DelayBackoffType.Exponential,
        ShouldHandle = new PredicateBuilder()
            .Handle<RequestFailedException>(ex => 
                ex.Status == 429 || ex.Status >= 500)
    })
    .Build();
```

## Key Links

- Docs: https://learn.microsoft.com/azure/ai-services/openai/
- Foundry: https://ai.azure.com
- Pricing: https://azure.microsoft.com/pricing/details/cognitive-services/openai-service/
- SDK: https://github.com/Azure/azure-sdk-for-net/tree/main/sdk/openai

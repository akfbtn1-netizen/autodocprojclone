using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ClientModel;
using System.Text.Json;

namespace Enterprise.AzureOpenAI.Services;

/// <summary>
/// Production-ready Azure OpenAI service with retry logic, streaming, and structured outputs
/// </summary>
public sealed class AzureOpenAIService : IAzureOpenAIService, IDisposable
{
    private readonly AzureOpenAIClient _client;
    private readonly ILogger<AzureOpenAIService> _logger;
    private readonly AzureOpenAIOptions _options;
    private bool _disposed;

    public AzureOpenAIService(
        IOptions<AzureOpenAIOptions> options,
        ILogger<AzureOpenAIService> logger)
    {
        _options = options.Value;
        _logger = logger;

        _client = CreateClient();
    }

    private AzureOpenAIClient CreateClient()
    {
        var endpoint = new Uri(_options.Endpoint);

        if (_options.UseEntraId)
        {
            _logger.LogInformation("Using Entra ID authentication for Azure OpenAI");
            return new AzureOpenAIClient(endpoint, new DefaultAzureCredential());
        }

        if (string.IsNullOrEmpty(_options.ApiKey))
        {
            throw new InvalidOperationException("API key required when not using Entra ID");
        }

        _logger.LogInformation("Using API key authentication for Azure OpenAI");
        return new AzureOpenAIClient(endpoint, new ApiKeyCredential(_options.ApiKey));
    }

    /// <summary>
    /// Complete a chat request with automatic retry and error handling
    /// </summary>
    public async Task<ChatCompletionResult> CompleteChatAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var deploymentName = SelectDeployment(request.TaskType);
        var chatClient = _client.GetChatClient(deploymentName);

        var messages = BuildMessages(request);
        var options = BuildOptions(request);

        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var response = await chatClient.CompleteChatAsync(messages, options, cancellationToken);
            stopwatch.Stop();

            var usage = response.Value.Usage;
            var cachedTokens = usage.InputTokenDetails?.CachedTokens ?? 0;

            _logger.LogInformation(
                "Chat completed: {InputTokens} input ({CachedTokens} cached), {OutputTokens} output, {Duration}ms",
                usage.InputTokenCount,
                cachedTokens,
                usage.OutputTokenCount,
                stopwatch.ElapsedMilliseconds);

            return new ChatCompletionResult
            {
                Content = response.Value.Content[0].Text,
                FinishReason = response.Value.FinishReason.ToString(),
                InputTokens = usage.InputTokenCount,
                OutputTokens = usage.OutputTokenCount,
                CachedTokens = cachedTokens,
                DurationMs = stopwatch.ElapsedMilliseconds,
                Model = deploymentName
            };
        }
        catch (ClientResultException ex) when (ex.Status == 429)
        {
            _logger.LogWarning("Rate limited by Azure OpenAI, consider using PTU for predictable workloads");
            throw new RateLimitException("Azure OpenAI rate limit exceeded", ex);
        }
        catch (ClientResultException ex)
        {
            _logger.LogError(ex, "Azure OpenAI request failed with status {Status}", ex.Status);
            throw;
        }
    }

    /// <summary>
    /// Stream chat responses for real-time UI updates
    /// </summary>
    public async IAsyncEnumerable<StreamChunk> StreamChatAsync(
        ChatRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var deploymentName = SelectDeployment(request.TaskType);
        var chatClient = _client.GetChatClient(deploymentName);

        var messages = BuildMessages(request);
        var options = BuildOptions(request);

        await foreach (var update in chatClient.CompleteChatStreamingAsync(messages, options, cancellationToken))
        {
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                {
                    yield return new StreamChunk
                    {
                        Content = part.Text,
                        FinishReason = update.FinishReason?.ToString()
                    };
                }
            }
        }
    }

    /// <summary>
    /// Complete chat with structured JSON output
    /// </summary>
    public async Task<T> CompleteChatStructuredAsync<T>(
        ChatRequest request,
        string schemaName,
        string jsonSchema,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(schemaName);
        ArgumentException.ThrowIfNullOrEmpty(jsonSchema);

        var deploymentName = SelectDeployment(request.TaskType);
        var chatClient = _client.GetChatClient(deploymentName);

        var messages = BuildMessages(request);
        var options = BuildOptions(request);

        options.ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
            schemaName,
            BinaryData.FromString(jsonSchema),
            jsonSchemaIsStrict: true);

        try
        {
            var response = await chatClient.CompleteChatAsync(messages, options, cancellationToken);
            var json = response.Value.Content[0].Text;

            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to deserialize response");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse structured output");
            throw new StructuredOutputException("Model response did not match expected schema", ex);
        }
    }

    /// <summary>
    /// Execute chat with function calling / tools
    /// </summary>
    public async Task<ToolCallResult> CompleteChatWithToolsAsync(
        ChatRequest request,
        IReadOnlyList<ChatTool> tools,
        Func<ChatToolCall, Task<string>> toolExecutor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(toolExecutor);

        var deploymentName = SelectDeployment(request.TaskType);
        var chatClient = _client.GetChatClient(deploymentName);

        var messages = BuildMessages(request);
        var options = BuildOptions(request);

        foreach (var tool in tools)
        {
            options.Tools.Add(tool);
        }
        options.ToolChoice = ChatToolChoice.CreateAutoChoice();

        var toolCallCount = 0;
        const int maxIterations = 10;

        while (toolCallCount < maxIterations)
        {
            var response = await chatClient.CompleteChatAsync(messages, options, cancellationToken);

            if (response.Value.FinishReason != ChatFinishReason.ToolCalls)
            {
                return new ToolCallResult
                {
                    Content = response.Value.Content[0].Text,
                    ToolCallCount = toolCallCount
                };
            }

            // Add assistant message with tool calls
            messages.Add(new AssistantChatMessage(response.Value));

            // Execute tools and add results
            foreach (var toolCall in response.Value.ToolCalls)
            {
                _logger.LogDebug("Executing tool: {ToolName}", toolCall.FunctionName);

                var result = await toolExecutor(toolCall);
                messages.Add(new ToolChatMessage(toolCall.Id, result));
                toolCallCount++;
            }
        }

        throw new MaxToolIterationsException($"Exceeded maximum tool iterations ({maxIterations})");
    }

    private string SelectDeployment(TaskType taskType)
    {
        return taskType switch
        {
            TaskType.SimpleClassification => _options.NanoDeployment ?? _options.DefaultDeployment,
            TaskType.ContentGeneration => _options.MiniDeployment ?? _options.DefaultDeployment,
            TaskType.ComplexReasoning => _options.ReasoningDeployment ?? _options.DefaultDeployment,
            TaskType.CodeGeneration => _options.CodexDeployment ?? _options.DefaultDeployment,
            _ => _options.DefaultDeployment
        };
    }

    private static List<ChatMessage> BuildMessages(ChatRequest request)
    {
        var messages = new List<ChatMessage>();

        if (!string.IsNullOrEmpty(request.SystemPrompt))
        {
            messages.Add(new SystemChatMessage(request.SystemPrompt));
        }

        foreach (var msg in request.Messages)
        {
            messages.Add(msg.Role.ToLower() switch
            {
                "user" => new UserChatMessage(msg.Content),
                "assistant" => new AssistantChatMessage(msg.Content),
                _ => throw new ArgumentException($"Unknown role: {msg.Role}")
            });
        }

        return messages;
    }

    private ChatCompletionOptions BuildOptions(ChatRequest request)
    {
        var options = new ChatCompletionOptions
        {
            MaxOutputTokenCount = request.MaxTokens ?? _options.DefaultMaxTokens,
            Temperature = request.Temperature ?? _options.DefaultTemperature
        };

        if (!string.IsNullOrEmpty(request.PromptCacheKey))
        {
            options.PromptCacheKey = request.PromptCacheKey;
        }

        return options;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // AzureOpenAIClient doesn't implement IDisposable, but we follow the pattern
    }
}

#region Models

public class AzureOpenAIOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public bool UseEntraId { get; set; } = true;
    public string DefaultDeployment { get; set; } = "gpt-5";
    public string? NanoDeployment { get; set; }
    public string? MiniDeployment { get; set; }
    public string? ReasoningDeployment { get; set; }
    public string? CodexDeployment { get; set; }
    public int DefaultMaxTokens { get; set; } = 2048;
    public float DefaultTemperature { get; set; } = 0.7f;
}

public class ChatRequest
{
    public string? SystemPrompt { get; set; }
    public List<ChatMessageDto> Messages { get; set; } = new();
    public TaskType TaskType { get; set; } = TaskType.Default;
    public int? MaxTokens { get; set; }
    public float? Temperature { get; set; }
    public string? PromptCacheKey { get; set; }
}

public class ChatMessageDto
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
}

public class ChatCompletionResult
{
    public string Content { get; set; } = string.Empty;
    public string FinishReason { get; set; } = string.Empty;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int CachedTokens { get; set; }
    public long DurationMs { get; set; }
    public string Model { get; set; } = string.Empty;
}

public class StreamChunk
{
    public string Content { get; set; } = string.Empty;
    public string? FinishReason { get; set; }
}

public class ToolCallResult
{
    public string Content { get; set; } = string.Empty;
    public int ToolCallCount { get; set; }
}

public enum TaskType
{
    Default,
    SimpleClassification,
    ContentGeneration,
    ComplexReasoning,
    CodeGeneration
}

#endregion

#region Exceptions

public class RateLimitException : Exception
{
    public RateLimitException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}

public class StructuredOutputException : Exception
{
    public StructuredOutputException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}

public class MaxToolIterationsException : Exception
{
    public MaxToolIterationsException(string message)
        : base(message) { }
}

#endregion

#region Interface

public interface IAzureOpenAIService
{
    Task<ChatCompletionResult> CompleteChatAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<StreamChunk> StreamChatAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default);

    Task<T> CompleteChatStructuredAsync<T>(
        ChatRequest request,
        string schemaName,
        string jsonSchema,
        CancellationToken cancellationToken = default) where T : class;

    Task<ToolCallResult> CompleteChatWithToolsAsync(
        ChatRequest request,
        IReadOnlyList<ChatTool> tools,
        Func<ChatToolCall, Task<string>> toolExecutor,
        CancellationToken cancellationToken = default);
}

#endregion

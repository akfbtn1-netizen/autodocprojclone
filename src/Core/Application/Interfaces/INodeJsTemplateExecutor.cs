using Enterprise.Documentation.Core.Domain.Entities;

namespace Core.Application.Interfaces;

public class TemplateExecutionRequest
{
    public string TemplateName { get; set; } = string.Empty;
    public ExcelChangeEntry Data { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public Dictionary<string, object> TemplateConfig { get; set; } = new();
}

public class DocumentGenerationResult
{
    public bool Success { get; set; }
    public string? GeneratedContent { get; set; }
    public string? OutputFilePath { get; set; }
    public TimeSpan GenerationTime { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public interface INodeJsTemplateExecutor
{
    Task<DocumentGenerationResult> ExecuteTemplateAsync(TemplateExecutionRequest request, CancellationToken cancellationToken = default);
    Task<DocumentGenerationResult> GenerateDocumentAsync(TemplateExecutionRequest request, CancellationToken cancellationToken = default);
    Task<bool> ValidateEnvironmentAsync(CancellationToken cancellationToken = default);
    Task<List<string>> GetAvailableTemplatesAsync(CancellationToken cancellationToken = default);
}
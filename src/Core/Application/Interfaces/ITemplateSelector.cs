using Enterprise.Documentation.Core.Domain.Entities;

namespace Core.Application.Interfaces;

public class TemplateInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
    public string Complexity { get; set; } = string.Empty;
    public List<string> SupportedDocumentTypes { get; set; } = new();
    public List<string> SupportedObjectTypes { get; set; } = new();
    public string? RequiredSchema { get; set; }
    public TimeSpan EstimatedTime { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public interface ITemplateSelector
{
    Task<TemplateInfo> SelectTemplateAsync(ExcelChangeEntry entry, string tier, CancellationToken cancellationToken = default);
    Task<List<TemplateInfo>> GetAvailableTemplatesAsync(string tier, CancellationToken cancellationToken = default);
    Task<bool> ValidateTemplateAsync(TemplateInfo template, ExcelChangeEntry entry, CancellationToken cancellationToken = default);
}
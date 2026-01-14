using Enterprise.Documentation.Core.Domain.Entities;

namespace Core.Application.Interfaces;

public class TierConfig
{
    public string Tier { get; set; } = string.Empty;
    public int SLAHours { get; set; }
    public bool RequiresApproval { get; set; }
    public string TemplateComplexity { get; set; } = string.Empty;
    public string EstimatedEffort { get; set; } = string.Empty;
    public bool ReviewRequired { get; set; }
}

public interface ITierClassifierService
{
    Task<string> ClassifyTierAsync(ExcelChangeEntry entry, CancellationToken cancellationToken = default);
    Task<string> ClassifyAsync(ExcelChangeEntry entry, CancellationToken cancellationToken = default);
    Task<TierConfig> GetTierConfigAsync(string tier, CancellationToken cancellationToken = default);
    Task<bool> ValidateTierAsync(ExcelChangeEntry entry, string expectedTier, CancellationToken cancellationToken = default);
}
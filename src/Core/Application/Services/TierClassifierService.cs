using Enterprise.Documentation.Core.Domain.Entities;
using Core.Application.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Core.Application.Services;

/// <summary>
/// Service for classifying documents into tiers based on complexity and business impact
/// </summary>
public class TierClassifierService : Core.Application.Interfaces.ITierClassifierService
{
    private readonly ILogger<TierClassifierService> _logger;

    public TierClassifierService(ILogger<TierClassifierService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Classifies document complexity into Tier 1 (simple), Tier 2 (moderate), or Tier 3 (complex)
    /// </summary>
    public async Task<string> ClassifyTierAsync(ExcelChangeEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            var tier = await DetermineTierBasedOnCriteriaAsync(entry);
            _logger.LogInformation("Classified entry {JiraNumber} as {Tier}", entry.JiraNumber, tier);
            return tier;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error classifying tier for entry {JiraNumber}", entry.JiraNumber);
            return "Tier2"; // Default fallback
        }
    }

    private async Task<string> DetermineTierBasedOnCriteriaAsync(ExcelChangeEntry entry)
    {
        var score = 0;

        // Complexity indicators
        if (!string.IsNullOrEmpty(entry.ObjectName))
        {
            // Stored procedures are typically more complex
            if (entry.ObjectName.Contains("sp_") || entry.ObjectName.Contains("proc"))
                score += 2;

            // Functions have moderate complexity
            if (entry.ObjectName.Contains("fn_") || entry.ObjectName.Contains("func"))
                score += 1;

            // Views are usually simpler
            if (entry.ObjectName.Contains("vw_") || entry.ObjectName.Contains("view"))
                score += 0;
        }

        // Schema complexity
        if (!string.IsNullOrEmpty(entry.SchemaName))
        {
            // Core business schemas are more critical
            if (entry.SchemaName.Equals("dbo", StringComparison.OrdinalIgnoreCase) ||
                entry.SchemaName.Contains("core", StringComparison.OrdinalIgnoreCase) ||
                entry.SchemaName.Contains("business", StringComparison.OrdinalIgnoreCase))
                score += 1;
        }

        // Document type complexity
        if (!string.IsNullOrEmpty(entry.DocumentType))
        {
            switch (entry.DocumentType.ToLower())
            {
                case "stored procedure":
                case "complex view":
                case "trigger":
                    score += 3;
                    break;
                case "function":
                case "aggregate":
                    score += 2;
                    break;
                case "table":
                case "simple view":
                    score += 1;
                    break;
                default:
                    score += 0;
                    break;
            }
        }

        // Business impact based on description patterns
        var description = entry.Description?.ToLower() ?? string.Empty;
        if (description.Contains("critical") || description.Contains("production") ||
            description.Contains("security") || description.Contains("compliance"))
            score += 2;

        if (description.Contains("performance") || description.Contains("optimization"))
            score += 1;

        // Classification logic
        return score switch
        {
            >= 5 => "Tier3", // Complex/High Impact
            >= 2 => "Tier2", // Moderate Complexity
            _ => "Tier1"      // Simple/Standard
        };
    }

    /// <summary>
    /// Gets tier-specific processing configuration
    /// </summary>
    public async Task<TierConfig> GetTierConfigAsync(string tier, CancellationToken cancellationToken = default)
    {
        return tier.ToLower() switch
        {
            "tier1" => new TierConfig
            {
                Tier = "Tier1",
                SLAHours = 24,
                RequiresApproval = false,
                TemplateComplexity = "Simple",
                EstimatedEffort = "Low",
                ReviewRequired = false
            },
            "tier2" => new TierConfig
            {
                Tier = "Tier2",
                SLAHours = 48,
                RequiresApproval = true,
                TemplateComplexity = "Moderate",
                EstimatedEffort = "Medium",
                ReviewRequired = true
            },
            "tier3" => new TierConfig
            {
                Tier = "Tier3",
                SLAHours = 72,
                RequiresApproval = true,
                TemplateComplexity = "Complex",
                EstimatedEffort = "High",
                ReviewRequired = true
            },
            _ => throw new ArgumentException($"Unknown tier: {tier}")
        };
    }

    /// <summary>
    /// Validates if an entry meets tier requirements
    /// </summary>
    public async Task<bool> ValidateTierAsync(ExcelChangeEntry entry, string expectedTier, CancellationToken cancellationToken = default)
    {
        var actualTier = await ClassifyTierAsync(entry, cancellationToken);
        return actualTier.Equals(expectedTier, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Classifies tier for an entry (alias for ClassifyTierAsync)
    /// </summary>
    public async Task<string> ClassifyAsync(ExcelChangeEntry entry, CancellationToken cancellationToken = default)
    {
        return await ClassifyTierAsync(entry, cancellationToken);
    }
}
using Enterprise.Documentation.Core.Application.Services;
using Enterprise.Documentation.Core.Application.Models;
using Enterprise.Documentation.Core.Domain.Entities;
using Core.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Core.Application.Services;

/// <summary>
/// Service for classifying documents into tiers based on complexity and business impact
/// Updated to work with your Azure OpenAI gpt-4.1 deployment
/// </summary>
public class TierClassifierService_Updated : ITierClassifierService
{
    private readonly ILogger<TierClassifierService_Updated> _logger;

    public TierClassifierService_Updated(ILogger<TierClassifierService_Updated> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Classifies document complexity and returns tier with recommended model (gpt-4.1)
    /// </summary>
    public async Task<TierClassification> ClassifyAsync(ObjectAnalysis analysis)
    {
        try
        {
            var tier = await DetermineTierAsync(analysis);
            
            var result = new TierClassification
            {
                Tier = tier,
                RecommendedModel = "gpt-4.1", // Using your Azure deployment
                Reasoning = GetTierReasoning(tier, analysis)
            };

            _logger.LogInformation("Classified {ObjectName} as Tier {Tier} using {Model}", 
                analysis.ObjectName, tier, result.RecommendedModel);
                
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error classifying tier for {ObjectName}", analysis.ObjectName);
            
            // Default fallback
            return new TierClassification
            {
                Tier = 2,
                RecommendedModel = "gpt-4.1",
                Reasoning = "Default tier due to classification error"
            };
        }
    }

    private async Task<int> DetermineTierAsync(ObjectAnalysis analysis)
    {
        await Task.CompletedTask; // For async consistency
        
        var score = 0;

        // Object type complexity scoring
        if (analysis.ObjectType?.ToLowerInvariant() == "table")
        {
            if (analysis.ColumnCount > 20) score += 2;
            else if (analysis.ColumnCount > 10) score += 1;
        }
        else if (analysis.ObjectType?.ToLowerInvariant() == "procedure")
        {
            score += 2; // Stored procedures are inherently complex
        }
        else if (analysis.ObjectType?.ToLowerInvariant() == "function")
        {
            score += 1; // Functions have moderate complexity
        }

        // Business criticality
        if (analysis.BusinessCriticality?.ToLowerInvariant() == "high") score += 2;
        else if (analysis.BusinessCriticality?.ToLowerInvariant() == "medium") score += 1;

        // Data classification
        if (analysis.DataClassification?.ToLowerInvariant() == "confidential" || 
            analysis.DataClassification?.ToLowerInvariant() == "restricted") score += 2;
        else if (analysis.DataClassification?.ToLowerInvariant() == "internal") score += 1;

        // PII indicator
        if (analysis.PIIIndicator) score += 1;

        // Confidence score adjustment
        if (analysis.ConfidenceScore >= 0.85m) score += 1;
        else if (analysis.ConfidenceScore <= 0.70m) score -= 1;

        // Determine tier based on total score
        if (score >= 5) return 1; // Tier 1: Comprehensive documentation
        if (score >= 2) return 2; // Tier 2: Standard documentation  
        return 3; // Tier 3: Lightweight documentation
    }

    private string GetTierReasoning(int tier, ObjectAnalysis analysis)
    {
        return tier switch
        {
            1 => $"Tier 1 (Comprehensive): High complexity {analysis.ObjectType} with {analysis.ColumnCount} columns, " +
                 $"{analysis.BusinessCriticality} criticality, {analysis.DataClassification} classification",
            2 => $"Tier 2 (Standard): Medium complexity {analysis.ObjectType} with balanced requirements",
            3 => $"Tier 3 (Lightweight): Simple {analysis.ObjectType} with basic documentation needs",
            _ => "Unknown tier classification"
        };
    }

    // Legacy method support for backward compatibility
    public async Task<string> ClassifyTierAsync(ExcelChangeEntry entry, CancellationToken cancellationToken = default)
    {
        // Convert ExcelChangeEntry to ObjectAnalysis for the new method
        var analysis = new ObjectAnalysis
        {
            ObjectName = entry.ColumnName ?? "Unknown",
            ObjectType = DetermineObjectType(entry),
            BusinessCriticality = "MEDIUM", // Default
            DataClassification = "INTERNAL", // Default
            PIIIndicator = false,
            ConfidenceScore = 0.80m,
            ColumnCount = 5 // Default estimate
        };

        var result = await ClassifyAsync(analysis);
        return $"Tier{result.Tier}";
    }

    public async Task<string> ClassifyAsync(ExcelChangeEntry entry, CancellationToken cancellationToken = default)
    {
        return await ClassifyTierAsync(entry, cancellationToken);
    }

    public async Task<TierConfig> GetTierConfigAsync(string tier, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        
        return tier.ToLowerInvariant() switch
        {
            "tier1" => new TierConfig
            {
                Tier = "Tier1",
                SLAHours = 72,
                RequiresApproval = true,
                TemplateComplexity = "Complex",
                EstimatedEffort = "High",
                ReviewRequired = true
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
                SLAHours = 24,
                RequiresApproval = false,
                TemplateComplexity = "Simple",
                EstimatedEffort = "Low",
                ReviewRequired = false
            },
            _ => throw new ArgumentException($"Unknown tier: {tier}")
        };
    }

    public async Task<bool> ValidateTierAsync(ExcelChangeEntry entry, string expectedTier, CancellationToken cancellationToken = default)
    {
        var actualTier = await ClassifyTierAsync(entry, cancellationToken);
        return actualTier.Equals(expectedTier, StringComparison.OrdinalIgnoreCase);
    }

    private string DetermineObjectType(ExcelChangeEntry entry)
    {
        var changeType = entry.ChangeType?.ToLowerInvariant();
        return changeType switch
        {
            "new_table" => "TABLE",
            "new_view" => "VIEW",
            "new_procedure" => "PROCEDURE",
            "column_addition" => "TABLE",
            _ => "TABLE"
        };
    }
}
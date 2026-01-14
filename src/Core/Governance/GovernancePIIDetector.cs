using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace Enterprise.Documentation.Core.Governance;

/// <summary>
/// Enterprise V2 PII Detection Engine for comprehensive personally identifiable information detection.
/// Implements pattern-based PII detection with configurable confidence scoring.
/// Enhanced from V1 patterns with async support and modern ML-ready architecture.
/// </summary>
public class GovernancePIIDetector
{
    private readonly ILogger<GovernancePIIDetector> _logger;
    private readonly ActivitySource _activitySource;

    // PII detection patterns with confidence scoring
    private readonly Dictionary<PIIType, List<PIIPattern>> _piiPatterns = new()
    {
        [PIIType.EmailAddress] = new()
        {
            new PIIPattern(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", 0.95, "Standard email pattern"),
            new PIIPattern(@"\b\w+@\w+\.\w+\b", 0.85, "Simple email pattern")
        },
        
        [PIIType.PhoneNumber] = new()
        {
            new PIIPattern(@"\b\d{3}-\d{3}-\d{4}\b", 0.95, "US phone format (XXX-XXX-XXXX)"),
            new PIIPattern(@"\b\(\d{3}\)\s*\d{3}-\d{4}\b", 0.95, "US phone format ((XXX) XXX-XXXX)"),
            new PIIPattern(@"\b\d{10}\b", 0.70, "10-digit number"),
            new PIIPattern(@"\+\d{1,3}\s?\d{3,4}\s?\d{3,4}\s?\d{3,4}", 0.85, "International phone format")
        },
        
        [PIIType.SSN] = new()
        {
            new PIIPattern(@"\b\d{3}-\d{2}-\d{4}\b", 0.98, "US SSN format (XXX-XX-XXXX)"),
            new PIIPattern(@"\b\d{9}\b", 0.60, "9-digit number (potential SSN)")
        },
        
        [PIIType.CreditCard] = new()
        {
            new PIIPattern(@"\b4\d{3}[\s-]?\d{4}[\s-]?\d{4}[\s-]?\d{4}\b", 0.95, "Visa credit card"),
            new PIIPattern(@"\b5[1-5]\d{2}[\s-]?\d{4}[\s-]?\d{4}[\s-]?\d{4}\b", 0.95, "MasterCard credit card"),
            new PIIPattern(@"\b3[47]\d{2}[\s-]?\d{6}[\s-]?\d{5}\b", 0.95, "American Express credit card"),
            new PIIPattern(@"\b\d{4}[\s-]?\d{4}[\s-]?\d{4}[\s-]?\d{4}\b", 0.70, "Generic 16-digit card pattern")
        },
        
        [PIIType.PersonName] = new()
        {
            new PIIPattern(@"\b[A-Z][a-z]+\s+[A-Z][a-z]+\b", 0.60, "First Last name pattern"),
            new PIIPattern(@"\b[A-Z][a-z]+\s+[A-Z]\.\s+[A-Z][a-z]+\b", 0.70, "First M. Last name pattern")
        },
        
        [PIIType.Address] = new()
        {
            new PIIPattern(@"\b\d+\s+[A-Za-z\s]+\s+(Street|St|Avenue|Ave|Road|Rd|Drive|Dr|Lane|Ln|Boulevard|Blvd)\b", 0.80, "US street address"),
            new PIIPattern(@"\b[A-Za-z\s]+,\s*[A-Z]{2}\s+\d{5}(-\d{4})?\b", 0.85, "City, State ZIP format")
        },
        
        [PIIType.DateOfBirth] = new()
        {
            new PIIPattern(@"\b(0[1-9]|1[0-2])/(0[1-9]|[12]\d|3[01])/(19|20)\d{2}\b", 0.90, "MM/DD/YYYY date format"),
            new PIIPattern(@"\b(19|20)\d{2}-(0[1-9]|1[0-2])-(0[1-9]|[12]\d|3[01])\b", 0.90, "YYYY-MM-DD date format")
        }
    };

    // Column name patterns that suggest PII content
    private readonly Dictionary<PIIType, List<string>> _piiColumnPatterns = new()
    {
        [PIIType.EmailAddress] = new() { "email", "e_mail", "mail", "contact_email" },
        [PIIType.PhoneNumber] = new() { "phone", "telephone", "mobile", "cell", "contact_phone" },
        [PIIType.SSN] = new() { "ssn", "social_security", "tax_id", "national_id" },
        [PIIType.PersonName] = new() { "name", "first_name", "last_name", "full_name", "firstname", "lastname" },
        [PIIType.Address] = new() { "address", "street", "city", "state", "zip", "postal", "location" },
        [PIIType.DateOfBirth] = new() { "birth", "dob", "date_of_birth", "birthday", "birthdate" },
        [PIIType.CreditCard] = new() { "card", "credit", "payment", "cc_number", "card_number" }
    };

    public GovernancePIIDetector(ILogger<GovernancePIIDetector> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activitySource = new ActivitySource($"{nameof(GovernancePIIDetector)}-v2");
    }

    /// <summary>
    /// Detects PII in the provided data value.
    /// </summary>
    public async Task<PIIDetectionResult> DetectPIIAsync(string columnName, string value, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("DetectPII");
        activity?.SetTag("column.name", columnName);
        activity?.SetTag("value.length", value?.Length ?? 0);

        if (string.IsNullOrWhiteSpace(value))
        {
            return new PIIDetectionResult { IsPII = false };
        }

        try
        {
            return await Task.Run(() =>
            {
                var bestMatch = new PIIDetectionResult { IsPII = false };
                double highestConfidence = 0;

                // Check each PII type pattern
                foreach (var piiTypePatterns in _piiPatterns)
                {
                    foreach (var pattern in piiTypePatterns.Value)
                    {
                        if (pattern.Regex.IsMatch(value))
                        {
                            var confidence = pattern.BaseConfidence;
                            
                            // Boost confidence if column name suggests this PII type
                            if (IsColumnNameSuggestivePII(columnName, piiTypePatterns.Key))
                            {
                                confidence = Math.Min(0.99, confidence + 0.15);
                            }

                            if (confidence > highestConfidence)
                            {
                                highestConfidence = confidence;
                                bestMatch = new PIIDetectionResult
                                {
                                    IsPII = true,
                                    PIIType = piiTypePatterns.Key,
                                    Confidence = confidence,
                                    DetectionRule = pattern.Description
                                };
                            }
                        }
                    }
                }

                activity?.SetTag("detection.is_pii", bestMatch.IsPII);
                activity?.SetTag("detection.confidence", bestMatch.Confidence);
                activity?.SetTag("detection.type", bestMatch.PIIType.ToString());

                return bestMatch;
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PII detection failed for column {ColumnName}", columnName);
            activity?.SetTag("detection.error", ex.Message);
            return new PIIDetectionResult { IsPII = false };
        }
    }

    /// <summary>
    /// Determines if a column name suggests PII content.
    /// </summary>
    public bool IsColumnPII(string columnName, string dataType)
    {
        var lowerColumnName = columnName.ToLowerInvariant();
        
        return _piiColumnPatterns.Values
            .SelectMany(patterns => patterns)
            .Any(pattern => lowerColumnName.Contains(pattern));
    }

    /// <summary>
    /// Classifies column data classification level.
    /// </summary>
    public DataClassification ClassifyColumn(string columnName, string dataType)
    {
        if (IsColumnPII(columnName, dataType))
        {
            var lowerColumnName = columnName.ToLowerInvariant();
            
            // High-sensitivity PII
            if (lowerColumnName.Contains("ssn") || 
                lowerColumnName.Contains("social_security") ||
                lowerColumnName.Contains("credit") ||
                lowerColumnName.Contains("card"))
            {
                return DataClassification.Restricted;
            }
            
            // Medium-sensitivity PII
            if (lowerColumnName.Contains("email") ||
                lowerColumnName.Contains("phone") ||
                lowerColumnName.Contains("address"))
            {
                return DataClassification.Confidential;
            }
            
            // Low-sensitivity PII
            return DataClassification.Internal;
        }
        
        return DataClassification.Public;
    }

    /// <summary>
    /// Checks if column name suggests specific PII type.
    /// </summary>
    private bool IsColumnNameSuggestivePII(string columnName, PIIType piiType)
    {
        var lowerColumnName = columnName.ToLowerInvariant();
        
        if (_piiColumnPatterns.TryGetValue(piiType, out var patterns))
        {
            return patterns.Any(pattern => lowerColumnName.Contains(pattern));
        }
        
        return false;
    }

    public void Dispose()
    {
        _activitySource?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// PII detection pattern with confidence scoring.
/// </summary>
public record PIIPattern
{
    public Regex Regex { get; }
    public double BaseConfidence { get; }
    public string Description { get; }

    public PIIPattern(string pattern, double baseConfidence, string description)
    {
        Regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        BaseConfidence = baseConfidence;
        Description = description;
    }
}

/// <summary>
/// Result of PII detection operation.
/// </summary>
public record PIIDetectionResult
{
    /// <summary>Whether PII was detected</summary>
    public bool IsPII { get; init; }
    
    /// <summary>Type of PII detected</summary>
    public PIIType PIIType { get; init; }
    
    /// <summary>Confidence score (0.0 to 1.0)</summary>
    public double Confidence { get; init; }
    
    /// <summary>Detection rule that triggered</summary>
    public string DetectionRule { get; init; } = string.Empty;
}
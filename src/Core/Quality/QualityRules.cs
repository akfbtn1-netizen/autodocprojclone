namespace Core.Quality;

/// <summary>
/// Configuration rules for code quality validation
/// Defines thresholds and standards for enterprise code quality
/// </summary>
public class QualityRules
{
    /// <summary>
    /// Maximum allowed cyclomatic complexity per method
    /// </summary>
    public int MaxCyclomaticComplexity { get; } = 6;
    
    /// <summary>
    /// Maximum allowed lines per method
    /// </summary>
    public int MaxMethodLines { get; } = 20;
    
    /// <summary>
    /// Maximum allowed lines per class
    /// </summary>
    public int MaxClassLines { get; } = 200;
    
    /// <summary>
    /// Minimum quality score required for validation
    /// </summary>
    public double MinimumQualityScore { get; } = 85.0;
    
    /// <summary>
    /// Whether XML documentation is required for public APIs
    /// </summary>
    public bool RequireDocumentation { get; } = true;
    
    /// <summary>
    /// Whether naming conventions should be enforced
    /// </summary>
    public bool EnforceNamingConventions { get; } = true;
}
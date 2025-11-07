namespace Core.Quality;

/// <summary>
/// Represents a specific quality violation found during code analysis
/// </summary>
public class QualityViolation
{
    /// <summary>
    /// Type of violation (e.g., "MethodLength", "CyclomaticComplexity")
    /// </summary>
    public string Type { get; set; } = "";
    
    /// <summary>
    /// Human-readable description of the violation
    /// </summary>
    public string Description { get; set; } = "";
    
    /// <summary>
    /// Location where the violation was found (method name, class name, etc.)
    /// </summary>
    public string Location { get; set; } = "";
    
    /// <summary>
    /// Line number where the violation occurs
    /// </summary>
    public int LineNumber { get; set; }
    
    /// <summary>
    /// Severity level of the violation
    /// </summary>
    public string Severity { get; set; } = "Warning";
}
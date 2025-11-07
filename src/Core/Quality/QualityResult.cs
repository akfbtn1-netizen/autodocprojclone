namespace Core.Quality;

/// <summary>
/// Results of quality validation for a single file
/// Contains all violations and quality metrics
/// </summary>
public class QualityResult
{
    /// <summary>
    /// Path to the analyzed file
    /// </summary>
    public string FilePath { get; set; } = "";
    
    /// <summary>
    /// Calculated quality score (0-100)
    /// </summary>
    public double OverallScore { get; set; }
    
    /// <summary>
    /// Whether the file passes quality validation
    /// </summary>
    public bool IsValid { get; set; }
    
    /// <summary>
    /// Violations related to cyclomatic complexity
    /// </summary>
    public List<QualityViolation> ComplexityViolations { get; } = new();
    
    /// <summary>
    /// Violations related to method length
    /// </summary>
    public List<QualityViolation> MethodLengthViolations { get; } = new();
    
    /// <summary>
    /// Violations related to class size
    /// </summary>
    public List<QualityViolation> ClassLengthViolations { get; } = new();
    
    /// <summary>
    /// Violations related to missing documentation
    /// </summary>
    public List<QualityViolation> DocumentationViolations { get; } = new();
    
    /// <summary>
    /// Violations related to naming conventions
    /// </summary>
    public List<QualityViolation> NamingViolations { get; } = new();
    
    /// <summary>
    /// Critical errors encountered during analysis
    /// </summary>
    public List<string> Errors { get; } = new();
    
    /// <summary>
    /// Adds a critical error to the result
    /// </summary>
    /// <param name="error">Error message</param>
    public void AddError(string error) => Errors.Add(error);
    
    /// <summary>
    /// Gets the total number of violations across all categories
    /// </summary>
    public int TotalViolations => 
        ComplexityViolations.Count + 
        MethodLengthViolations.Count + 
        ClassLengthViolations.Count + 
        DocumentationViolations.Count + 
        NamingViolations.Count;
}
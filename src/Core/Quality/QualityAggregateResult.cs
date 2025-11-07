namespace Core.Quality;

/// <summary>
/// Aggregated quality validation results for multiple files
/// Provides project-level quality metrics and analysis
/// </summary>
public class QualityAggregateResult
{
    /// <summary>
    /// Individual file validation results
    /// </summary>
    public List<QualityResult> FileResults { get; set; } = new();
    
    /// <summary>
    /// Total number of files analyzed
    /// </summary>
    public int TotalFiles { get; set; }
    
    /// <summary>
    /// Number of files that passed validation
    /// </summary>
    public int ValidFiles { get; set; }
    
    /// <summary>
    /// Number of files that failed validation
    /// </summary>
    public int InvalidFiles { get; set; }
    
    /// <summary>
    /// Average quality score across all files
    /// </summary>
    public double AverageScore { get; set; }
    
    /// <summary>
    /// Whether the entire project passes quality validation
    /// </summary>
    public bool IsProjectValid { get; set; }
    
    /// <summary>
    /// Gets all files that failed quality validation
    /// </summary>
    /// <returns>List of failed quality results</returns>
    public List<QualityResult> GetFailedFiles() => 
        FileResults.Where(f => !f.IsValid).ToList();
    
    /// <summary>
    /// Gets the total number of violations across all files
    /// </summary>
    public int TotalViolations => 
        FileResults.Sum(f => f.TotalViolations);
    
    /// <summary>
    /// Gets violations grouped by type for reporting
    /// </summary>
    public Dictionary<string, int> GetViolationsByType()
    {
        var violations = new Dictionary<string, int>();
        
        foreach (var file in FileResults)
        {
            AddViolationCounts(violations, file);
        }
        
        return violations;
    }
    
    /// <summary>
    /// Adds violation counts from a file to the violations dictionary
    /// </summary>
    private void AddViolationCounts(Dictionary<string, int> violations, QualityResult file)
    {
        AddViolationsToDict(violations, file.ComplexityViolations, "Complexity");
        AddViolationsToDict(violations, file.MethodLengthViolations, "MethodLength");
        AddViolationsToDict(violations, file.ClassLengthViolations, "ClassLength");
        AddViolationsToDict(violations, file.DocumentationViolations, "Documentation");
        AddViolationsToDict(violations, file.NamingViolations, "Naming");
    }
    
    private static void AddViolationsToDict(Dictionary<string, int> dict, 
        List<QualityViolation> violations, string type)
    {
        if (violations.Count > 0)
        {
            dict[type] = dict.GetValueOrDefault(type, 0) + violations.Count;
        }
    }
}
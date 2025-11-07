namespace Core.Quality;

/// <summary>
/// Enterprise AI Quality System orchestrator for automated code quality validation
/// Coordinates quality validation using focused, single-responsibility components
/// </summary>
public class EnterpriseAIQualitySystem
{
    private readonly QualityRules _rules;
    private readonly QualityValidator _validator;
    private readonly QualityReporter _reporter;
    
    /// <summary>
    /// Initializes the quality system with enterprise standards
    /// </summary>
    public EnterpriseAIQualitySystem()
    {
        _rules = new QualityRules();
        _validator = new QualityValidator(_rules);
        _reporter = new QualityReporter();
    }

    /// <summary>
    /// Validates a single file against enterprise quality standards
    /// </summary>
    /// <param name="filePath">Path to the file being validated</param>
    /// <param name="content">Source code content to validate</param>
    /// <returns>Quality validation result with detailed metrics</returns>
    public QualityResult ValidateFile(string filePath, string content)
    {
        return _validator.ValidateSourceCode(content, filePath);
    }

    /// <summary>
    /// Validates multiple files and returns project-wide quality metrics
    /// </summary>
    /// <param name="filePaths">Collection of file paths to validate</param>
    /// <returns>Aggregate quality results for the entire project</returns>
    public QualityAggregateResult ValidateProject(IEnumerable<string> filePaths)
    {
        var results = new List<QualityResult>();
        
        foreach (var filePath in filePaths.Where(IsValidCSharpFile))
        {
            var content = File.ReadAllText(filePath);
            var result = ValidateFile(filePath, content);
            results.Add(result);
        }
        
        return CreateAggregateResult(results);
    }

    /// <summary>
    /// Generates a comprehensive quality report for a single file
    /// </summary>
    /// <param name="result">Quality validation result</param>
    /// <returns>Formatted quality report</returns>
    public string GenerateFileReport(QualityResult result)
    {
        return _reporter.GenerateFileReport(result);
    }

    /// <summary>
    /// Generates a project-wide quality summary report
    /// </summary>
    /// <param name="aggregateResult">Project quality metrics</param>
    /// <returns>Formatted project summary</returns>
    public string GenerateProjectReport(QualityAggregateResult aggregateResult)
    {
        return _reporter.GenerateProjectSummary(aggregateResult);
    }

    /// <summary>
    /// Generates a CI/CD friendly quality report
    /// </summary>
    /// <param name="aggregateResult">Project quality metrics</param>
    /// <returns>Concise CI/CD report format</returns>
    public string GenerateCiReport(QualityAggregateResult aggregateResult)
    {
        return _reporter.GenerateCiReport(aggregateResult);
    }

    /// <summary>
    /// Validates that a file path represents a valid C# source file
    /// </summary>
    private static bool IsValidCSharpFile(string filePath)
    {
        return File.Exists(filePath) && 
               filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
               !filePath.Contains("bin") && 
               !filePath.Contains("obj");
    }

    /// <summary>
    /// Creates aggregate results from individual file validation results
    /// </summary>
    private QualityAggregateResult CreateAggregateResult(List<QualityResult> results)
    {
        return new QualityAggregateResult
        {
            FileResults = results,
            TotalFiles = results.Count,
            ValidFiles = results.Count(r => r.IsValid),
            InvalidFiles = results.Count(r => !r.IsValid),
            AverageScore = results.Count > 0 ? results.Average(r => r.QualityScore) : 0,
            IsProjectValid = results.All(r => r.IsValid)
        };
    }
}


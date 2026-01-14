using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Enterprise.Documentation.Core.Application.Services.Quality;

public interface IEnterpriseCodeQualityAuditService
{
    Task<CodeQualityResult> AuditCodeQualityAsync(
        string code,
        CancellationToken cancellationToken = default);
}

public class CodeQualityResult
{
    public int Score { get; set; }              // 0-100
    public string Grade { get; set; } = string.Empty;           // A+, A, B+, B, C+, C, D, F
    public string Category { get; set; } = string.Empty;        // "Excellent", "Good", "Fair", "Poor", "Critical"
    public List<string> Issues { get; set; } = new();    // List of detected issues
    public List<string> Recommendations { get; set; } = new();  // Improvement suggestions
    public Dictionary<string, int> Metrics { get; set; } = new();  // Detailed metrics
}

public class EnterpriseCodeQualityAuditService : IEnterpriseCodeQualityAuditService
{
    private readonly ILogger<EnterpriseCodeQualityAuditService> _logger;

    public EnterpriseCodeQualityAuditService(ILogger<EnterpriseCodeQualityAuditService> logger)
    {
        _logger = logger;
    }

    public async Task<CodeQualityResult> AuditCodeQualityAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting code complexity analysis for {CodeLength} characters", code.Length);

        await Task.Delay(100, cancellationToken); // Simulate async operation

        var result = new CodeQualityResult();
        var score = 100; // Start with perfect score

        // Analyze code and deduct points for issues
        score = AnalyzeCodeStructure(code, result, score);
        score = AnalyzeNaming(code, result, score);
        score = AnalyzeComplexity(code, result, score);
        score = AnalyzeSecurity(code, result, score);
        score = AnalyzePerformance(code, result, score);
        score = AnalyzeFormatting(code, result, score);
        score = AnalyzeDocumentation(code, result, score);
        score = AnalyzeBestPractices(code, result, score);

        // Ensure score is within bounds
        result.Score = Math.Max(0, Math.Min(100, score));
        result.Grade = CalculateGrade(result.Score);
        result.Category = CalculateCategory(result.Score);

        // Add metrics
        result.Metrics["LinesOfCode"] = code.Split('\n').Length;
        result.Metrics["CharacterCount"] = code.Length;
        result.Metrics["IssuesFound"] = result.Issues.Count;

        _logger.LogInformation("Code complexity analysis complete: Score={Score}, Grade={Grade}, Issues={Issues}",
            result.Score, result.Grade, result.Issues.Count);

        return result;
    }

    private int AnalyzeCodeStructure(string code, CodeQualityResult result, int currentScore)
    {
        var score = currentScore;

        // Check for proper SQL structure
        if (string.IsNullOrWhiteSpace(code))
        {
            result.Issues.Add("Code is empty or whitespace only");
            score -= 30;
        }

        // Check for basic SQL keywords
        var hasSelect = code.Contains("SELECT", StringComparison.OrdinalIgnoreCase);
        var hasUpdate = code.Contains("UPDATE", StringComparison.OrdinalIgnoreCase);
        var hasInsert = code.Contains("INSERT", StringComparison.OrdinalIgnoreCase);
        var hasDelete = code.Contains("DELETE", StringComparison.OrdinalIgnoreCase);

        if (!hasSelect && !hasUpdate && !hasInsert && !hasDelete)
        {
            result.Issues.Add("No recognizable SQL statements found");
            score -= 20;
        }

        // Check for dangerous patterns
        if (Regex.IsMatch(code, @"DROP\s+TABLE", RegexOptions.IgnoreCase))
        {
            result.Issues.Add("Contains potentially dangerous DROP TABLE statement");
            score -= 15;
        }

        return score;
    }

    private int AnalyzeNaming(string code, CodeQualityResult result, int currentScore)
    {
        var score = currentScore;

        // Check for single character variables
        var singleCharVariables = Regex.Matches(code, @"@[a-zA-Z]\s", RegexOptions.IgnoreCase);
        if (singleCharVariables.Count > 0)
        {
            result.Issues.Add($"Found {singleCharVariables.Count} single-character variable(s)");
            result.Recommendations.Add("Use descriptive variable names instead of single characters");
            score -= Math.Min(10, singleCharVariables.Count * 2);
        }

        return score;
    }

    private int AnalyzeComplexity(string code, CodeQualityResult result, int currentScore)
    {
        var score = currentScore;
        var lines = code.Split('\n');

        // Check line length
        var longLines = lines.Where(line => line.Length > 120).ToList();
        if (longLines.Any())
        {
            result.Issues.Add($"Found {longLines.Count} line(s) longer than 120 characters");
            result.Recommendations.Add("Break long lines for better readability");
            score -= Math.Min(5, longLines.Count);
        }

        // Check nesting depth (approximate)
        var maxIndentation = lines.Where(l => !string.IsNullOrWhiteSpace(l))
                                 .Max(line => line.Length - line.TrimStart().Length);
        if (maxIndentation > 32)
        {
            result.Issues.Add("Code appears to be deeply nested");
            result.Recommendations.Add("Consider refactoring to reduce nesting depth");
            score -= 8;
        }

        return score;
    }

    private int AnalyzeSecurity(string code, CodeQualityResult result, int currentScore)
    {
        var score = currentScore;

        // Check for SQL injection patterns
        if (Regex.IsMatch(code, @"'\s*\+\s*@", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(code, @"EXEC\s*\(\s*@", RegexOptions.IgnoreCase))
        {
            result.Issues.Add("Potential SQL injection vulnerability detected");
            result.Recommendations.Add("Use parameterized queries instead of string concatenation");
            score -= 25;
        }

        // Check for hardcoded passwords or sensitive data
        if (Regex.IsMatch(code, @"password\s*=\s*['\""][^'\""]+ ['\""]", RegexOptions.IgnoreCase))
        {
            result.Issues.Add("Hardcoded password or sensitive data detected");
            result.Recommendations.Add("Use secure configuration management for sensitive data");
            score -= 20;
        }

        return score;
    }

    private int AnalyzePerformance(string code, CodeQualityResult result, int currentScore)
    {
        var score = currentScore;

        // Check for SELECT *
        if (Regex.IsMatch(code, @"SELECT\s+\*", RegexOptions.IgnoreCase))
        {
            result.Issues.Add("Use of SELECT * found");
            result.Recommendations.Add("Specify column names explicitly instead of using SELECT *");
            score -= 5;
        }

        // Check for missing WHERE clauses in UPDATE/DELETE
        if (Regex.IsMatch(code, @"UPDATE\s+\w+\s+SET\s+[^W]*(?!WHERE)", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(code, @"DELETE\s+FROM\s+\w+\s*(?!WHERE)", RegexOptions.IgnoreCase))
        {
            result.Issues.Add("UPDATE or DELETE without WHERE clause detected");
            result.Recommendations.Add("Always use WHERE clauses with UPDATE and DELETE statements");
            score -= 15;
        }

        return score;
    }

    private int AnalyzeFormatting(string code, CodeQualityResult result, int currentScore)
    {
        var score = currentScore;
        var lines = code.Split('\n');

        // Check for consistent indentation (basic check)
        var hasInconsistentIndentation = lines.Where(l => !string.IsNullOrWhiteSpace(l))
                                            .Take(10)
                                            .Select(line => line.Length - line.TrimStart().Length)
                                            .Distinct()
                                            .Count() > 3;

        if (hasInconsistentIndentation)
        {
            result.Issues.Add("Inconsistent indentation detected");
            result.Recommendations.Add("Use consistent indentation throughout the code");
            score -= 5;
        }

        // Check for trailing whitespace
        var trailingWhitespaceLines = lines.Count(line => line.EndsWith(" ") || line.EndsWith("\t"));
        if (trailingWhitespaceLines > 0)
        {
            result.Issues.Add($"Found {trailingWhitespaceLines} line(s) with trailing whitespace");
            result.Recommendations.Add("Remove trailing whitespace");
            score -= Math.Min(3, trailingWhitespaceLines);
        }

        return score;
    }

    private int AnalyzeDocumentation(string code, CodeQualityResult result, int currentScore)
    {
        var score = currentScore;

        // Check for comments
        var commentLines = code.Split('\n').Count(line => line.TrimStart().StartsWith("--"));
        var totalLines = code.Split('\n').Length;
        
        if (totalLines > 10 && commentLines == 0)
        {
            result.Issues.Add("No comments found in code longer than 10 lines");
            result.Recommendations.Add("Add comments to explain complex logic");
            score -= 8;
        }

        return score;
    }

    private int AnalyzeBestPractices(string code, CodeQualityResult result, int currentScore)
    {
        var score = currentScore;

        // Check for proper transaction handling
        var hasBeginTransaction = code.Contains("BEGIN TRANSACTION", StringComparison.OrdinalIgnoreCase) ||
                                code.Contains("BEGIN TRAN", StringComparison.OrdinalIgnoreCase);
        var hasCommitRollback = code.Contains("COMMIT", StringComparison.OrdinalIgnoreCase) ||
                              code.Contains("ROLLBACK", StringComparison.OrdinalIgnoreCase);

        if (hasBeginTransaction && !hasCommitRollback)
        {
            result.Issues.Add("Transaction started but no COMMIT or ROLLBACK found");
            result.Recommendations.Add("Always include proper transaction handling with COMMIT/ROLLBACK");
            score -= 10;
        }

        // Check for proper error handling
        if (code.Contains("RAISERROR", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("THROW", StringComparison.OrdinalIgnoreCase))
        {
            result.Recommendations.Add("Good: Error handling detected");
        }
        else if (code.Length > 500)
        {
            result.Issues.Add("No error handling found in substantial code block");
            result.Recommendations.Add("Consider adding error handling for robust code");
            score -= 5;
        }

        return score;
    }

    private string CalculateGrade(int score)
    {
        return score switch
        {
            >= 95 => "A+",
            >= 90 => "A",
            >= 85 => "B+",
            >= 80 => "B",
            >= 75 => "C+",
            >= 70 => "C",
            >= 60 => "D",
            _ => "F"
        };
    }

    private string CalculateCategory(int score)
    {
        return score switch
        {
            >= 90 => "Excellent",
            >= 80 => "Good",
            >= 70 => "Fair",
            >= 60 => "Poor",
            _ => "Critical"
        };
    }
}
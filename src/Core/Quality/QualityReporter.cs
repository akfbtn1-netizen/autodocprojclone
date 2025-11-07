using System.Text;

namespace Core.Quality;

/// <summary>
/// Generates detailed reports from quality validation results
/// Provides formatted output for various reporting scenarios
/// </summary>
public class QualityReporter
{
    /// <summary>
    /// Generates a comprehensive quality report for a single file
    /// </summary>
    /// <param name="result">Quality validation result to report</param>
    /// <returns>Formatted quality report text</returns>
    public string GenerateFileReport(QualityResult result)
    {
        var report = new StringBuilder();
        report.AppendLine($"Quality Report for: {result.FileName}");
        report.AppendLine($"Score: {result.QualityScore:F1}/100");
        report.AppendLine($"Lines of Code: {result.LinesOfCode}");
        report.AppendLine($"Valid: {(result.IsValid ? "Yes" : "No")}");
        report.AppendLine();
        
        AddViolationSection(report, "Complexity Violations", result.ComplexityViolations);
        AddViolationSection(report, "Method Length Violations", result.MethodLengthViolations);
        AddViolationSection(report, "Class Length Violations", result.ClassLengthViolations);
        AddViolationSection(report, "Documentation Violations", result.DocumentationViolations);
        AddViolationSection(report, "Naming Violations", result.NamingViolations);
        
        return report.ToString();
    }
    
    /// <summary>
    /// Generates a project-wide quality summary report
    /// </summary>
    /// <param name="aggregateResult">Aggregate quality results for project</param>
    /// <returns>Formatted project summary report</returns>
    public string GenerateProjectSummary(QualityAggregateResult aggregateResult)
    {
        var report = new StringBuilder();
        report.AppendLine("=== PROJECT QUALITY SUMMARY ===");
        report.AppendLine($"Total Files Analyzed: {aggregateResult.TotalFiles}");
        report.AppendLine($"Valid Files: {aggregateResult.ValidFiles}");
        report.AppendLine($"Invalid Files: {aggregateResult.InvalidFiles}");
        report.AppendLine($"Average Score: {aggregateResult.AverageScore:F1}/100");
        report.AppendLine($"Project Status: {(aggregateResult.IsProjectValid ? "PASS" : "FAIL")}");
        report.AppendLine($"Total Violations: {aggregateResult.TotalViolations}");
        report.AppendLine();
        
        // Violation breakdown by type
        var violationsByType = aggregateResult.GetViolationsByType();
        if (violationsByType.Any())
        {
            report.AppendLine("Violation Breakdown:");
            foreach (var kvp in violationsByType.OrderByDescending(x => x.Value))
            {
                report.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }
            report.AppendLine();
        }
        
        // Worst performing files
        var failedFiles = aggregateResult.GetFailedFiles()
            .OrderBy(f => f.QualityScore)
            .Take(10);
            
        if (failedFiles.Any())
        {
            report.AppendLine("Files Requiring Attention (Lowest Scores):");
            foreach (var file in failedFiles)
            {
                report.AppendLine($"  {file.FileName}: {file.QualityScore:F1}/100 ({file.TotalViolations} violations)");
            }
        }
        
        return report.ToString();
    }
    
    /// <summary>
    /// Generates a CSV export of quality results for analysis
    /// </summary>
    /// <param name="results">Quality results to export</param>
    /// <returns>CSV formatted data</returns>
    public string GenerateCsvExport(List<QualityResult> results)
    {
        var csv = new StringBuilder();
        csv.AppendLine("FileName,Score,LinesOfCode,IsValid,ComplexityViolations,MethodLengthViolations,ClassLengthViolations,DocumentationViolations,NamingViolations,TotalViolations");
        
        foreach (var result in results)
        {
            csv.AppendLine($"{EscapeCsvField(result.FileName)}," +
                          $"{result.QualityScore:F1}," +
                          $"{result.LinesOfCode}," +
                          $"{result.IsValid}," +
                          $"{result.ComplexityViolations.Count}," +
                          $"{result.MethodLengthViolations.Count}," +
                          $"{result.ClassLengthViolations.Count}," +
                          $"{result.DocumentationViolations.Count}," +
                          $"{result.NamingViolations.Count}," +
                          $"{result.TotalViolations}");
        }
        
        return csv.ToString();
    }
    
    /// <summary>
    /// Generates a focused report for CI/CD pipeline consumption
    /// </summary>
    /// <param name="aggregateResult">Project quality results</param>
    /// <returns>Concise CI/CD report format</returns>
    public string GenerateCiReport(QualityAggregateResult aggregateResult)
    {
        var report = new StringBuilder();
        
        if (aggregateResult.IsProjectValid)
        {
            report.AppendLine($"✅ QUALITY CHECK PASSED");
            report.AppendLine($"Score: {aggregateResult.AverageScore:F1}/100");
            report.AppendLine($"Files: {aggregateResult.ValidFiles}/{aggregateResult.TotalFiles} passed");
        }
        else
        {
            report.AppendLine($"❌ QUALITY CHECK FAILED");
            report.AppendLine($"Score: {aggregateResult.AverageScore:F1}/100");
            report.AppendLine($"Files: {aggregateResult.InvalidFiles}/{aggregateResult.TotalFiles} failed");
            report.AppendLine($"Total Violations: {aggregateResult.TotalViolations}");
            
            var criticalFiles = aggregateResult.GetFailedFiles()
                .Where(f => f.QualityScore < 50)
                .Take(5);
                
            if (criticalFiles.Any())
            {
                report.AppendLine("\nCritical Files:");
                foreach (var file in criticalFiles)
                {
                    report.AppendLine($"  - {file.FileName}: {file.QualityScore:F1}/100");
                }
            }
        }
        
        return report.ToString();
    }
    
    /// <summary>
    /// Adds a violation section to the report
    /// </summary>
    private static void AddViolationSection(StringBuilder report, string title, List<QualityViolation> violations)
    {
        if (violations.Count == 0) return;
        
        report.AppendLine($"{title} ({violations.Count}):");
        foreach (var violation in violations.Take(10)) // Limit to top 10
        {
            report.AppendLine($"  Line {violation.LineNumber}: {violation.Message}");
        }
        
        if (violations.Count > 10)
        {
            report.AppendLine($"  ... and {violations.Count - 10} more violations");
        }
        
        report.AppendLine();
    }
    
    /// <summary>
    /// Escapes CSV field content to handle commas and quotes
    /// </summary>
    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return "";
        
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        
        return field;
    }
}
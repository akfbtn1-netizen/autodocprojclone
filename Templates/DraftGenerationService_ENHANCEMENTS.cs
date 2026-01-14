// ═══════════════════════════════════════════════════════════════════════════
// DRAFTGENERATIONSERVICE - PRODUCTION QUALITY ENHANCEMENTS
// Add these methods to your existing DraftGenerationService.cs
// ═══════════════════════════════════════════════════════════════════════════

// ═══════════════════════════════════════════════════════════════════════════
// HELPER METHODS FOR DATA QUALITY
// ═══════════════════════════════════════════════════════════════════════════

private string DetermineColumnDataType(DocumentChangeEntry changeEntry, SqlAnalysisResult? sqlAnalysis)
{
    // Priority 1: Check if we have column metadata
    if (!string.IsNullOrEmpty(changeEntry.ColumnName))
    {
        // Look for column in SQL analysis
        var columnName = changeEntry.ColumnName;
        
        // Check parameters for matching column
        var matchingParam = sqlAnalysis?.Parameters?
            .FirstOrDefault(p => p.Name.Contains(columnName, StringComparison.OrdinalIgnoreCase));
        
        if (matchingParam != null && !string.IsNullOrEmpty(matchingParam.Type))
        {
            return matchingParam.Type;
        }
        
        // Common column type inference
        if (columnName.Contains("_ind", StringComparison.OrdinalIgnoreCase))
            return "CHAR(1)";
        if (columnName.Contains("_cd", StringComparison.OrdinalIgnoreCase))
            return "VARCHAR(20)";
        if (columnName.Contains("_dt", StringComparison.OrdinalIgnoreCase) || 
            columnName.Contains("_date", StringComparison.OrdinalIgnoreCase))
            return "DATE";
        if (columnName.Contains("_ts", StringComparison.OrdinalIgnoreCase) ||
            columnName.Contains("timestamp", StringComparison.OrdinalIgnoreCase))
            return "DATETIME";
        if (columnName.Contains("_amt", StringComparison.OrdinalIgnoreCase) ||
            columnName.Contains("_amount", StringComparison.OrdinalIgnoreCase))
            return "DECIMAL(18,2)";
        if (columnName.Contains("_id", StringComparison.OrdinalIgnoreCase))
            return "INT";
    }
    
    // Priority 2: Default to VARCHAR if unknown
    return "VARCHAR(255)";
}

private string ExtractPossibleValues(DocumentChangeEntry changeEntry, SqlAnalysisResult? sqlAnalysis)
{
    var columnName = changeEntry.ColumnName ?? "";
    
    // Check if it's an indicator column (Y/N)
    if (columnName.Contains("_ind", StringComparison.OrdinalIgnoreCase))
    {
        return "Y = Yes/True/Active\nN = No/False/Inactive";
    }
    
    // Check if it's a code column - look in bracketed change for CASE statement or CHECK constraint
    if (sqlAnalysis?.BracketedChange != null)
    {
        var code = sqlAnalysis.BracketedChange.Code;
        
        // Look for CASE WHEN patterns
        var casePattern = new System.Text.RegularExpressions.Regex(
            @$"{columnName}\s*=\s*(?:CASE.*?END|'[^']*')",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | 
            System.Text.RegularExpressions.RegexOptions.Singleline);
        
        var match = casePattern.Match(code);
        if (match.Success)
        {
            // Extract values from THEN clauses
            var thenPattern = new System.Text.RegularExpressions.Regex(@"THEN\s+'([^']*)'");
            var values = thenPattern.Matches(match.Value)
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .ToList();
            
            if (values.Any())
            {
                return string.Join("\n", values.Select(v => $"{v} - See code for usage"));
            }
        }
        
        // Look for IN (...) patterns
        var inPattern = new System.Text.RegularExpressions.Regex(
            @$"{columnName}\s+IN\s*\(([^)]+)\)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        match = inPattern.Match(code);
        if (match.Success)
        {
            var values = match.Groups[1].Value
                .Split(',')
                .Select(v => v.Trim().Trim('\''))
                .ToList();
            
            return string.Join("\n", values);
        }
    }
    
    return "See code implementation for valid values";
}

private string BuildExecutiveSummary(
    DocumentChangeEntry changeEntry, 
    SqlAnalysisResult? sqlAnalysis, 
    Dictionary<string, object> aiEnhanced)
{
    // Get AI-generated purpose or build from parts
    var aiPurpose = aiEnhanced.GetValueOrDefault("Purpose", "") as string;
    
    if (!string.IsNullOrEmpty(aiPurpose) && aiPurpose.Length > 100)
    {
        return aiPurpose;
    }
    
    // Build comprehensive summary
    var parts = new List<string>();
    
    // What changed
    var changeType = changeEntry.ChangeType ?? "update";
    var objectType = string.IsNullOrEmpty(changeEntry.ColumnName) ? "stored procedure" : "column";
    var objectName = !string.IsNullOrEmpty(changeEntry.ColumnName) 
        ? $"{changeEntry.TableName}.{changeEntry.ColumnName}"
        : changeEntry.StoredProcedureName ?? "database object";
    
    parts.Add($"This {changeType.ToLower()} modifies the {objectName} {objectType} to improve {GetImprovementArea(changeEntry, aiEnhanced)}.");
    
    // Add complexity context if available
    if (sqlAnalysis?.Complexity != null)
    {
        var complexity = sqlAnalysis.Complexity.GetValueOrDefault("level", "");
        if (!string.IsNullOrEmpty(complexity))
        {
            parts.Add($"The implementation involves {complexity.ToLower()} complexity processing with {GetComplexityMetrics(sqlAnalysis)}.");
        }
    }
    
    // Add business impact
    var impact = aiEnhanced.GetValueOrDefault("BusinessImpactLevel", "") as string;
    if (!string.IsNullOrEmpty(impact))
    {
        parts.Add($"This change has {impact} business impact on {GetImpactedSystems(sqlAnalysis, aiEnhanced)}.");
    }
    
    return string.Join(" ", parts);
}

private string BuildEnhancementDescription(
    DocumentChangeEntry changeEntry,
    SqlAnalysisResult? sqlAnalysis,
    Dictionary<string, object> aiEnhanced)
{
    var whatsNew = aiEnhanced.GetValueOrDefault("WhatsNew", "") as string;
    
    if (!string.IsNullOrEmpty(whatsNew) && whatsNew.Length > 50)
    {
        return FormatEnhancementText(whatsNew, sqlAnalysis);
    }
    
    // Build from parts
    var desc = new System.Text.StringBuilder();
    
    desc.AppendLine($"**Primary Enhancement:**");
    desc.AppendLine(changeEntry.Description ?? "Enhancement to improve system functionality.");
    desc.AppendLine();
    
    if (sqlAnalysis?.BracketedChange != null)
    {
        desc.AppendLine("**Key Changes:**");
        desc.AppendLine($"- Modified {sqlAnalysis.BracketedChange.Lines} lines of code");
        desc.AppendLine($"- JIRA Reference: {changeEntry.JiraNumber}");
        
        if (sqlAnalysis.Parameters != null && sqlAnalysis.Parameters.Any())
        {
            desc.AppendLine($"- Affects {sqlAnalysis.Parameters.Count} parameters");
        }
        
        if (sqlAnalysis.Dependencies?.ContainsKey("source_tables") == true)
        {
            var tables = sqlAnalysis.Dependencies["source_tables"];
            desc.AppendLine($"- Integrates with {tables.Count} database tables");
        }
    }
    
    desc.AppendLine();
    desc.AppendLine("**Technical Approach:**");
    desc.AppendLine("- Code changes implement improved business logic");
    desc.AppendLine("- Maintains backward compatibility");
    desc.AppendLine("- Includes comprehensive error handling");
    
    return desc.ToString();
}

private string BuildBusinessBenefits(
    DocumentChangeEntry changeEntry,
    SqlAnalysisResult? sqlAnalysis,
    Dictionary<string, object> aiEnhanced)
{
    var aiImpact = aiEnhanced.GetValueOrDefault("BusinessImpact", "") as string;
    
    if (!string.IsNullOrEmpty(aiImpact) && aiImpact.Length > 50)
    {
        return FormatBenefitsText(aiImpact, sqlAnalysis);
    }
    
    // Build structured benefits
    var benefits = new System.Text.StringBuilder();
    
    benefits.AppendLine("**Operational Benefits:**");
    benefits.AppendLine("- Improved data accuracy and reliability");
    benefits.AppendLine("- Enhanced system performance");
    benefits.AppendLine("- Better error handling and logging");
    benefits.AppendLine();
    
    benefits.AppendLine("**Business Benefits:**");
    benefits.AppendLine("- More accurate reporting and analytics");
    benefits.AppendLine("- Reduced manual intervention required");
    benefits.AppendLine("- Better compliance with business rules");
    benefits.AppendLine();
    
    if (sqlAnalysis?.Complexity != null)
    {
        var complexity = sqlAnalysis.Complexity.GetValueOrDefault("level", "");
        if (complexity == "HIGH")
        {
            benefits.AppendLine("**Technical Benefits:**");
            benefits.AppendLine("- Robust processing of complex scenarios");
            benefits.AppendLine("- Comprehensive validation logic");
            benefits.AppendLine("- Scalable architecture for future enhancements");
        }
    }
    
    return benefits.ToString();
}

private string BuildCodeExplanation(
    DocumentChangeEntry changeEntry,
    SqlAnalysisResult? sqlAnalysis,
    Dictionary<string, object> aiEnhanced)
{
    var aiTechnical = aiEnhanced.GetValueOrDefault("TechnicalSummary", "") as string;
    
    if (!string.IsNullOrEmpty(aiTechnical) && aiTechnical.Length > 100)
    {
        return FormatCodeExplanation(aiTechnical, sqlAnalysis);
    }
    
    // Build structured explanation
    var explanation = new System.Text.StringBuilder();
    
    if (sqlAnalysis?.LogicSteps != null && sqlAnalysis.LogicSteps.Any())
    {
        explanation.AppendLine("**Implementation Steps:**");
        explanation.AppendLine();
        
        for (int i = 0; i < sqlAnalysis.LogicSteps.Count; i++)
        {
            explanation.AppendLine($"**Step {i + 1}:** {sqlAnalysis.LogicSteps[i]}");
        }
        
        explanation.AppendLine();
    }
    
    if (sqlAnalysis?.Complexity != null)
    {
        explanation.AppendLine("**Complexity Analysis:**");
        foreach (var kvp in sqlAnalysis.Complexity)
        {
            explanation.AppendLine($"- {FormatComplexityKey(kvp.Key)}: {kvp.Value}");
        }
        explanation.AppendLine();
    }
    
    if (sqlAnalysis?.PerformanceNotes != null && sqlAnalysis.PerformanceNotes.Any())
    {
        explanation.AppendLine("**Performance Considerations:**");
        foreach (var note in sqlAnalysis.PerformanceNotes)
        {
            explanation.AppendLine($"- {note}");
        }
    }
    
    return explanation.ToString();
}

private string ExtractRootCause(DocumentChangeEntry changeEntry, Dictionary<string, object> aiEnhanced)
{
    // For defect fixes, try to extract root cause from description
    var description = changeEntry.Description ?? "";
    
    if (description.Contains("fix", StringComparison.OrdinalIgnoreCase) ||
        description.Contains("bug", StringComparison.OrdinalIgnoreCase) ||
        description.Contains("issue", StringComparison.OrdinalIgnoreCase))
    {
        return $"Root cause identified in {changeEntry.JiraNumber}: {description}";
    }
    
    return aiEnhanced.GetValueOrDefault("ErrorHandling", "Technical analysis in progress") as string ?? "Analysis pending";
}

private string FormatCodeBlock(SqlAnalysisResult? sqlAnalysis)
{
    if (sqlAnalysis?.BracketedChange != null)
    {
        var code = sqlAnalysis.BracketedChange.Code;
        
        // Add header comment if not present
        if (!code.Contains("BEGIN") && !code.Contains("--"))
        {
            var header = $"-- Extracted from {sqlAnalysis.SchemaName}.{sqlAnalysis.ProcedureName}\n";
            header += $"-- {sqlAnalysis.BracketedChange.Lines} lines of code\n\n";
            return header + code;
        }
        
        return code;
    }
    
    return "-- No code changes detected\n-- See full procedure for implementation details";
}

private string FormatParameters(List<ParameterInfo>? parameters)
{
    if (parameters == null || !parameters.Any())
        return "No parameters";
    
    var formatted = new System.Text.StringBuilder();
    foreach (var param in parameters)
    {
        formatted.AppendLine($"{param.Name} ({param.Type}) - {param.Description ?? "Parameter description"}");
    }
    
    return formatted.ToString();
}

private string FormatLogicFlow(List<string>? logicSteps)
{
    if (logicSteps == null || !logicSteps.Any())
        return "See code for implementation details";
    
    return string.Join("\n", logicSteps.Select((step, i) => $"{i + 1}. {step}"));
}

private string FormatPerformanceNotes(List<string>? notes)
{
    if (notes == null || !notes.Any())
        return "Standard performance characteristics";
    
    return string.Join("\n", notes.Select(note => $"- {note}"));
}

private string FormatComplexity(Dictionary<string, string>? complexity)
{
    if (complexity == null || !complexity.Any())
        return "Standard complexity";
    
    var formatted = new System.Text.StringBuilder();
    foreach (var kvp in complexity)
    {
        formatted.AppendLine($"{FormatComplexityKey(kvp.Key)}: {kvp.Value}");
    }
    
    return formatted.ToString();
}

private string FormatComplexityKey(string key)
{
    return key switch
    {
        "lines" => "Lines of Code",
        "joins" => "Join Count",
        "ctes" => "CTEs",
        "temp_tables" => "Temp Tables",
        "level" => "Complexity Level",
        _ => key.Replace("_", " ").ToTitleCase()
    };
}

private string GetImprovementArea(DocumentChangeEntry changeEntry, Dictionary<string, object> aiEnhanced)
{
    var changeType = changeEntry.ChangeType?.ToLower() ?? "";
    
    return changeType switch
    {
        "defect" => "data accuracy and fix identified issues",
        "enhancement" => "functionality and system capabilities",
        "business request" => "business process alignment",
        _ => "system functionality"
    };
}

private string GetComplexityMetrics(SqlAnalysisResult sqlAnalysis)
{
    var metrics = new List<string>();
    
    if (sqlAnalysis.Complexity?.TryGetValue("lines", out var lines) == true)
        metrics.Add($"{lines} lines");
    
    if (sqlAnalysis.Complexity?.TryGetValue("joins", out var joins) == true)
        metrics.Add($"{joins} joins");
    
    if (sqlAnalysis.Complexity?.TryGetValue("ctes", out var ctes) == true && ctes != "0")
        metrics.Add($"{ctes} CTEs");
    
    return metrics.Any() ? string.Join(", ", metrics) : "standard processing";
}

private string GetImpactedSystems(SqlAnalysisResult? sqlAnalysis, Dictionary<string, object> aiEnhanced)
{
    var systems = new List<string>();
    
    if (sqlAnalysis?.Dependencies?.ContainsKey("source_tables") == true)
    {
        var tables = sqlAnalysis.Dependencies["source_tables"];
        if (tables.Any(t => t.Contains("policy", StringComparison.OrdinalIgnoreCase)))
            systems.Add("policy management");
        if (tables.Any(t => t.Contains("financial", StringComparison.OrdinalIgnoreCase) || 
                           t.Contains("premium", StringComparison.OrdinalIgnoreCase)))
            systems.Add("financial reporting");
        if (tables.Any(t => t.Contains("agent", StringComparison.OrdinalIgnoreCase)))
            systems.Add("agent management");
    }
    
    if (!systems.Any())
        systems.Add("core business processes");
    
    return string.Join(", ", systems);
}

private string FormatEnhancementText(string text, SqlAnalysisResult? sqlAnalysis)
{
    // Add bold formatting for key terms
    text = System.Text.RegularExpressions.Regex.Replace(
        text,
        @"\b(Primary Enhancement|Key Features|Technical Approach|Implementation)\b",
        "**$1**"
    );
    
    return text;
}

private string FormatBenefitsText(string text, SqlAnalysisResult? sqlAnalysis)
{
    // Add bold formatting for benefit categories
    text = System.Text.RegularExpressions.Regex.Replace(
        text,
        @"\b(Operational Benefits|Business Benefits|Technical Benefits)\b",
        "**$1**"
    );
    
    return text;
}

private string FormatCodeExplanation(string text, SqlAnalysisResult? sqlAnalysis)
{
    // Add bold formatting for steps
    text = System.Text.RegularExpressions.Regex.Replace(
        text,
        @"\b(Step \d+|Implementation Steps|Performance Considerations)\b",
        "**$1**"
    );
    
    return text;
}

// ═══════════════════════════════════════════════════════════════════════════
// UPDATED MERGETEMPLATEDATA METHOD
// Replace your existing MergeTemplateData with this version
// ═══════════════════════════════════════════════════════════════════════════

private Dictionary<string, object> MergeTemplateData(
    DocumentChangeEntry changeEntry,
    SqlAnalysisResult? sqlAnalysis,
    Dictionary<string, object> aiEnhanced)
{
    // ═══ FIX 1: Proper schema/table separation ═══
    string schemaName = changeEntry.SchemaName ?? "";
    string tableName = changeEntry.TableName ?? "";
    
    // Handle tables that already include schema (e.g., "gwpcDaily.irf_policy")
    if (tableName.Contains('.'))
    {
        var parts = tableName.Split('.');
        if (string.IsNullOrEmpty(schemaName))
        {
            schemaName = parts[0];
        }
        tableName = parts[1];
    }
    
    // ═══ FIX 2: Determine actual column data type ═══
    string dataType = DetermineColumnDataType(changeEntry, sqlAnalysis);
    
    // ═══ FIX 3: Extract possible values ═══
    string possibleValues = ExtractPossibleValues(changeEntry, sqlAnalysis);
    
    // ═══ FIX 4: Build comprehensive descriptions ═══
    string executiveSummary = BuildExecutiveSummary(changeEntry, sqlAnalysis, aiEnhanced);
    string enhancementDesc = BuildEnhancementDescription(changeEntry, sqlAnalysis, aiEnhanced);
    string businessBenefits = BuildBusinessBenefits(changeEntry, sqlAnalysis, aiEnhanced);
    string codeExplanation = BuildCodeExplanation(changeEntry, sqlAnalysis, aiEnhanced);
    
    var templateData = new Dictionary<string, object>
    {
        // ═══ CORE IDENTIFIERS ═══
        ["doc_id"] = changeEntry.DocId ?? "UNKNOWN",
        ["jira"] = changeEntry.JiraNumber ?? "",
        ["version"] = DetermineVersion(changeEntry),
        ["author"] = changeEntry.ReportedBy ?? "System",
        ["date"] = changeEntry.Date?.ToString("MM/dd/yyyy") ?? DateTime.Now.ToString("MM/dd/yyyy"),
        
        // ═══ DATABASE OBJECT INFO (FIXED) ═══
        ["database"] = changeEntry.DatabaseName ?? "IRFS1",
        ["schema"] = schemaName,
        ["table"] = tableName,
        ["sp_name"] = changeEntry.StoredProcedureName ?? $"{schemaName}.{tableName}",
        ["column"] = changeEntry.ColumnName ?? "",
        ["change_type"] = changeEntry.ChangeType ?? "",
        
        // ═══ ENHANCED FIELDS (FIXED) ═══
        ["data_type"] = dataType,
        ["values"] = possibleValues,
        ["purpose"] = aiEnhanced.GetValueOrDefault("Purpose", changeEntry.Description ?? "") ?? "",
        
        // ═══ DOCUMENT METADATA ═══
        ["doc_type"] = DetermineDocumentType(changeEntry),
        ["status"] = "Active",
        ["priority"] = changeEntry.Priority ?? "Medium",
        ["severity"] = changeEntry.Severity ?? "",
        
        // ═══ CHANGE TRACKING ═══
        ["description"] = changeEntry.Description ?? "",
        ["whats_new"] = DetermineWhatsNew(changeEntry, aiEnhanced),
        ["change_applied"] = changeEntry.ChangeApplied ?? "",
        ["location"] = changeEntry.LocationOfCodeChange ?? "",
        
        // ═══ STAKEHOLDER INFO ═══
        ["reported_by"] = changeEntry.ReportedBy ?? "System",
        ["assigned_to"] = changeEntry.AssignedTo ?? "",
        ["cab_number"] = "",
        ["sprint_number"] = "",
        
        // ═══ COMPREHENSIVE DESCRIPTIONS (QUALITY IMPROVED) ═══
        ["summary"] = executiveSummary,
        ["enhancement"] = enhancementDesc,
        ["benefits"] = businessBenefits,
        ["code_explain"] = codeExplanation,
        
        // ═══ CODE CONTENT ═══
        ["code"] = FormatCodeBlock(sqlAnalysis),
        
        // ═══ DEFECT-SPECIFIC FIELDS ═══
        ["defect_desc"] = changeEntry.Description ?? "",
        ["impact"] = aiEnhanced.GetValueOrDefault("BusinessImpact", "") ?? "",
        ["root_cause"] = ExtractRootCause(changeEntry, aiEnhanced),
        
        // ═══ BUSINESS REQUEST FIELDS ═══
        ["business_need"] = aiEnhanced.GetValueOrDefault("BusinessImpact", changeEntry.Description ?? "") ?? "",
        ["proposed_solution"] = aiEnhanced.GetValueOrDefault("TechnicalSummary", "") ?? "",
        ["acceptance_criteria"] = "To be defined",
        
        // ═══ STORED PROCEDURE FIELDS ═══
        ["object_type"] = "Stored Procedure",
        ["recent_changes"] = aiEnhanced.GetValueOrDefault("WhatsNew", "") ?? "",
        ["created_date"] = changeEntry.Date?.ToString("MM/dd/yyyy") ?? DateTime.Now.ToString("MM/dd/yyyy"),
        ["created_by"] = changeEntry.ReportedBy ?? "System"
    };

    // Add SQL analysis data if available
    if (sqlAnalysis != null)
    {
        templateData["parameters"] = FormatParameters(sqlAnalysis.Parameters);
        templateData["logic"] = sqlAnalysis.LogicSteps ?? new List<string>();
        templateData["logic_flow"] = FormatLogicFlow(sqlAnalysis.LogicSteps);
        templateData["dependencies"] = sqlAnalysis.Dependencies ?? new Dictionary<string, List<string>>();
        templateData["performance"] = FormatPerformanceNotes(sqlAnalysis.PerformanceNotes);
        templateData["error_handling"] = sqlAnalysis.ErrorHandling ?? "";
        templateData["complexity"] = FormatComplexity(sqlAnalysis.Complexity);
        templateData["related_tables"] = sqlAnalysis.Dependencies?.GetValueOrDefault("source_tables", new List<string>()) ?? new List<string>();
        templateData["usage"] = GenerateUsageExample(sqlAnalysis, changeEntry);
    }

    // Merge remaining AI-enhanced data
    foreach (var kvp in aiEnhanced)
    {
        if (!templateData.ContainsKey(kvp.Key))
        {
            templateData[kvp.Key] = kvp.Value;
        }
    }

    return templateData;
}

// ═══════════════════════════════════════════════════════════════════════════
// HELPER EXTENSION METHOD
// Add this to your project (create a new StringExtensions.cs file)
// ═══════════════════════════════════════════════════════════════════════════

public static class StringExtensions
{
    public static string ToTitleCase(this string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;
        
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(str.ToLower());
    }
}

// <copyright file="StoredProcedureTemplate.cs" company="Enterprise Documentation Platform">
// Copyright (c) Enterprise Documentation Platform. All rights reserved.
// </copyright>

namespace Enterprise.Documentation.Core.Application.Services.DocumentGeneration.Templates;

/// <summary>
/// Template for generating stored procedure documentation.
/// </summary>
public class StoredProcedureTemplate : IDocumentTemplate
{
    /// <summary>
    /// Gets or sets the procedure name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the procedure definition.
    /// </summary>
    public string Definition { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the procedure description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parameters.
    /// </summary>
    public List<string> Parameters { get; set; } = new();

    /// <summary>
    /// Gets or sets the return information.
    /// </summary>
    public string Returns { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets performance notes.
    /// </summary>
    public string PerformanceNotes { get; set; } = string.Empty;

    // Nested classes for compatibility
    public class VersionHistoryEntry
    {
        public string Version { get; set; } = string.Empty;
        public string Changes { get; set; } = string.Empty;
        public DateTime Date { get; set; }
    }

    public class StoredProcedureData
    {
        public string Name { get; set; } = string.Empty;
        public string SpName { get; set; } = string.Empty;
        public string Definition { get; set; } = string.Empty;
    }

    public class ParameterInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class ChangeEntry
    {
        public string Description { get; set; } = string.Empty;
        public DateTime Date { get; set; }
    }

    public class DependencyInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    public class UsageExample
    {
        public string Title { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    public class LogicStep
    {
        public string Description { get; set; } = string.Empty;
        public int Order { get; set; }
    }

    /// <summary>
    /// Generates a document stream from stored procedure data.
    /// </summary>
    public MemoryStream GenerateDocument(StoredProcedureData data)
    {
        var content = GenerateDocumentContent(data);
        var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, leaveOpen: true))
        {
            writer.Write(content);
        }
        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// Creates sample data for testing.
    /// </summary>
    public static StoredProcedureData CreateSampleData(string procedureName, string definition)
    {
        return new StoredProcedureData
        {
            Name = procedureName,
            Definition = definition
        };
    }

    /// <summary>
    /// Implements IDocumentTemplate.GenerateAsync for the draft generation service.
    /// </summary>
    public async Task<string> GenerateAsync(Dictionary<string, object> templateData, CancellationToken ct = default)
    {
        // Convert dictionary data to template format
        var content = $@"# Documentation Draft - {templateData.GetValueOrDefault("DocId", "Unknown")}

## Change Information
- **JIRA Number**: {templateData.GetValueOrDefault("JiraNumber", "N/A")}
- **Description**: {templateData.GetValueOrDefault("Description", "No description provided")}
- **Change Type**: {templateData.GetValueOrDefault("ChangeType", "Unknown")}
- **Priority**: {templateData.GetValueOrDefault("Priority", "Medium")}
- **Assigned To**: {templateData.GetValueOrDefault("AssignedTo", "Unassigned")}

## Technical Details
- **Table**: {templateData.GetValueOrDefault("TableName", "N/A")}
- **Column**: {templateData.GetValueOrDefault("ColumnName", "N/A")}
- **Stored Procedure**: {templateData.GetValueOrDefault("StoredProcedureName", "N/A")}
";

        // Add code extraction section if available
        if ((bool)templateData.GetValueOrDefault("HasCodeExtraction", false))
        {
            content += $@"
## Code Analysis
**Extracted Code:**
```sql
{templateData.GetValueOrDefault("ExtractedCode", "No code extracted")}
```
";
        }

        // Add quality analysis section if available
        if ((bool)templateData.GetValueOrDefault("HasQualityAnalysis", false))
        {
            content += $@"
## Quality Assessment
- **Score**: {templateData.GetValueOrDefault("QualityScore", "N/A")}/100
- **Grade**: {templateData.GetValueOrDefault("QualityGrade", "Not Analyzed")}
- **Category**: {templateData.GetValueOrDefault("QualityCategory", "N/A")}
";
            
            if (templateData.ContainsKey("QualityIssues"))
            {
                content += $@"
**Quality Issues:**
{templateData["QualityIssues"]}
";
            }
        }

        content += $@"

## Metadata
- **Generated**: {templateData.GetValueOrDefault("GeneratedDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"))}
- **Version**: {templateData.GetValueOrDefault("DocumentVersion", "1.0")}

---
*This document was automatically generated by the Enterprise Documentation Platform.*
";

        return await Task.FromResult(content);
    }

    private string GenerateDocumentContent(StoredProcedureData data)
    {
        return $@"# Stored Procedure Documentation

## {data.Name}

### Definition
```sql
{data.Definition}
```

### Generated
{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC

---
*Auto-generated by Enterprise Documentation Platform*
";
    }
}
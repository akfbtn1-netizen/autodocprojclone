// src/Core/Application/Helpers/CustomPropertiesHelper.cs

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.CustomProperties;
using DocumentFormat.OpenXml.Packaging;

namespace Enterprise.Documentation.Core.Application.Helpers;

/// <summary>
/// Helper for adding hidden metadata (CustomProperties) to Word documents
/// </summary>
public static class CustomPropertiesHelper
{
    /// <summary>
    /// Adds custom properties to a Word document. These are invisible to users
    /// but embedded in the .docx file for tracking and compliance.
    /// </summary>
    /// <param name="filePath">Path to the Word document</param>
    /// <param name="properties">Dictionary of property name/value pairs to embed</param>
    public static void AddCustomProperties(string filePath, Dictionary<string, string> properties)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Document not found: {filePath}");

        using var doc = WordprocessingDocument.Open(filePath, true);
        AddCustomProperties(doc, properties);
    }

    /// <summary>
    /// Adds custom properties to an open WordprocessingDocument
    /// </summary>
    public static void AddCustomProperties(WordprocessingDocument doc, Dictionary<string, string> properties)
    {
        if (properties == null || !properties.Any())
            return;

        var customFilePropertiesPart = doc.CustomFilePropertiesPart;

        // Create CustomFilePropertiesPart if it doesn't exist
        if (customFilePropertiesPart == null)
        {
            customFilePropertiesPart = doc.AddCustomFilePropertiesPart();
            customFilePropertiesPart.Properties = new Properties();
        }

        var props = customFilePropertiesPart.Properties;

        // Get the next available property ID
        int propertyId = GetNextPropertyId(props);

        // Add each property
        foreach (var kvp in properties)
        {
            // Remove existing property with same name if it exists
            RemovePropertyIfExists(props, kvp.Key);

            // Create new custom property  
            var customProp = new CustomDocumentProperty()
            {
                FormatId = "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}", // Required GUID for custom properties
                PropertyId = propertyId++,
                Name = kvp.Key
            };
            
            // Set the text value directly  
            customProp.InnerXml = $"<vt:lpwstr xmlns:vt=\"http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes\">{System.Security.SecurityElement.Escape(kvp.Value)}</vt:lpwstr>";

            // Add to properties collection
            props.AppendChild(customProp);
        }

        // Save changes
        customFilePropertiesPart.Properties.Save();
    }

    /// <summary>
    /// Reads custom properties from a Word document
    /// </summary>
    public static Dictionary<string, string> ReadCustomProperties(string filePath)
    {
        var result = new Dictionary<string, string>();

        if (!File.Exists(filePath))
            return result;

        using var doc = WordprocessingDocument.Open(filePath, false);
        
        var customFilePropertiesPart = doc.CustomFilePropertiesPart;
        if (customFilePropertiesPart?.Properties == null)
            return result;

        foreach (var prop in customFilePropertiesPart.Properties.Elements<CustomDocumentProperty>())
        {
            var name = prop.Name?.Value;
            var value = prop.InnerText;

            if (!string.IsNullOrEmpty(name))
            {
                result[name] = value;
            }
        }

        return result;
    }

    /// <summary>
    /// Removes a specific custom property from a Word document
    /// </summary>
    public static bool RemoveCustomProperty(string filePath, string propertyName)
    {
        if (!File.Exists(filePath))
            return false;

        using var doc = WordprocessingDocument.Open(filePath, true);
        
        var customFilePropertiesPart = doc.CustomFilePropertiesPart;
        if (customFilePropertiesPart?.Properties == null)
            return false;

        var removed = RemovePropertyIfExists(customFilePropertiesPart.Properties, propertyName);
        
        if (removed)
        {
            customFilePropertiesPart.Properties.Save();
        }

        return removed;
    }

    // ═══════════════════════════════════════════════════════════════════
    // PRIVATE HELPER METHODS
    // ═══════════════════════════════════════════════════════════════════

    private static int GetNextPropertyId(Properties props)
    {
        // Property IDs must start at 2 (1 is reserved)
        int maxId = 2;

        foreach (var prop in props.Elements<CustomDocumentProperty>())
        {
            if (prop.PropertyId?.Value > maxId)
            {
                maxId = prop.PropertyId.Value;
            }
        }

        return maxId + 1;
    }

    private static bool RemovePropertyIfExists(Properties props, string propertyName)
    {
        var existingProp = props.Elements<CustomDocumentProperty>()
            .FirstOrDefault(p => p.Name?.Value == propertyName);

        if (existingProp != null)
        {
            existingProp.Remove();
            return true;
        }

        return false;
    }
}

/// <summary>
/// Extension methods for easy metadata embedding
/// </summary>
public static class CustomPropertiesExtensions
{
    /// <summary>
    /// Adds comprehensive metadata to a document based on approval data
    /// </summary>
    public static void EmbedApprovalMetadata(
        this WordprocessingDocument doc,
        string docId,
        string jiraNumber,
        string indexId,
        ApprovalMetadataDto approval)
    {
        var metadata = new Dictionary<string, string>
        {
            // Identity
            { "DocId", docId },
            { "JiraNumber", jiraNumber },
            { "MasterIndexId", indexId },
            { "DocumentType", DetermineDocumentType(docId) },
            { "Version", "1.0" },
            
            // Database Objects
            { "SchemaName", approval.SchemaName ?? "N/A" },
            { "TableName", approval.TableName ?? "N/A" },
            { "ColumnName", approval.ColumnName ?? "N/A" },
            
            // Quality
            { "CodeQualityScore", approval.CodeQualityScore?.ToString() ?? "N/A" },
            { "CodeQualityGrade", approval.CodeQualityGrade ?? "N/A" },
            
            // People
            { "ReportedBy", approval.ReportedBy ?? "N/A" },
            { "AssignedTo", approval.AssignedTo ?? "N/A" },
            { "ApprovedBy", approval.ApprovedBy ?? "N/A" },
            
            // Dates
            { "DateRequested", approval.DateRequested?.ToString("yyyy-MM-dd") ?? "N/A" },
            { "DateApproved", DateTime.UtcNow.ToString("yyyy-MM-dd") },
            
            // Status
            { "ApprovalStatus", "Approved" },
            { "WorkflowStatus", "Completed" },
            
            // System
            { "GeneratedBy", "DocumentationAutomation" },
            { "GeneratedDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") }
        };

        CustomPropertiesHelper.AddCustomProperties(doc, metadata);
    }

    /// <summary>
    /// Adds comprehensive metadata to a document file based on approval data
    /// </summary>
    public static void EmbedApprovalMetadata(
        string filePath,
        string docId,
        string jiraNumber,
        string indexId,
        ApprovalMetadataDto approval)
    {
        using var doc = WordprocessingDocument.Open(filePath, true);
        doc.EmbedApprovalMetadata(docId, jiraNumber, indexId, approval);
    }

    private static string DetermineDocumentType(string docId)
    {
        if (docId.StartsWith("BR-")) return "BusinessRequest";
        if (docId.StartsWith("EN-")) return "Enhancement";
        if (docId.StartsWith("DF-")) return "DefectFix";
        if (docId.StartsWith("SP-")) return "StoredProcedure";
        return "Unknown";
    }
}

/// <summary>
/// DTO for approval metadata
/// </summary>
public class ApprovalMetadataDto
{
    public string? SchemaName { get; set; }
    public string? TableName { get; set; }
    public string? ColumnName { get; set; }
    public int? CodeQualityScore { get; set; }
    public string? CodeQualityGrade { get; set; }
    public string? ReportedBy { get; set; }
    public string? AssignedTo { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? DateRequested { get; set; }
}
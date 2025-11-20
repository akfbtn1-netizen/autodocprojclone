using System;
using System.Linq;

namespace Enterprise.Documentation.Core.Domain.Models;

/// <summary>
/// Represents a change document entry from the BI Analytics Change Spreadsheet.
/// Maps directly to Excel columns: Date, JIRA #, CAB #, Sprint #, Status, Priority,
/// Severity, Table, Column, Change Type, Description, Reported By, Assigned to,
/// Documentation, Documentation Link
/// </summary>
public class DocumentChangeEntry
{
    public int Id { get; set; }

    // Excel Columns - Direct Mapping
    public DateTime? Date { get; set; }              // Date
    public string? JiraNumber { get; set; }          // JIRA #
    public string? CABNumber { get; set; }           // CAB #
    public string? SprintNumber { get; set; }        // Sprint #
    public string? Status { get; set; }              // Status
    public string? Priority { get; set; }            // Priority
    public string? Severity { get; set; }            // Severity
    public string? TableName { get; set; }           // Table
    public string? ColumnName { get; set; }          // Column
    public string? ChangeType { get; set; }          // Change Type
    public string? Description { get; set; }         // Description
    public string? ReportedBy { get; set; }          // Reported By
    public string? AssignedTo { get; set; }          // Assigned to
    public string? Documentation { get; set; }       // Documentation
    public string? DocumentationLink { get; set; }   // Documentation Link

    // Sync Metadata
    public int ExcelRowNumber { get; set; }
    public DateTime LastSyncedFromExcel { get; set; }
    public string? SyncStatus { get; set; }
    public string? SyncErrors { get; set; }

    // Deduplication
    public string? ContentHash { get; set; }  // SHA256 of key fields to detect duplicates
    public string? UniqueKey { get; set; }    // CABNumber + TableName + ColumnName

    // Audit
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Generates a unique key for deduplication based on CAB #, Table, and Column.
    /// </summary>
    public string GenerateUniqueKey()
    {
        var parts = new[] { CABNumber, TableName, ColumnName }
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => p!.Trim().ToUpperInvariant());
        return string.Join("|", parts);
    }

    /// <summary>
    /// Generates a content hash for detecting similar/duplicate documents.
    /// </summary>
    public string GenerateContentHash()
    {
        var content = $"{CABNumber}|{JiraNumber}|{TableName}|{ColumnName}|{ChangeType}|{Description}";
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}

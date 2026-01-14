using System.Security.Cryptography;
using System.Text;

namespace Enterprise.Documentation.Core.Domain.Models;

/// <summary>
/// Represents a change document entry from the BI Analytics Change Spreadsheet.
/// Maps directly to Excel columns: Date, JIRA #, CAB #, Sprint #, Status, Priority,
/// Severity, Table, Column, Change Type, Description, Reported By, Assigned to,
/// Change Applied, Location of Changed Code, DocId
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
    public string? ChangeApplied { get; set; }       // Change Applied
    public string? LocationOfCodeChange { get; set; } // Location of Changed Code
    public string? DocId { get; set; }               // DocId (populated after approval)
    public string? ModifiedStoredProcedures { get; set; }  // Modified Stored Procedures (comma-separated)

    // Sync Metadata
    public int ExcelRowNumber { get; set; }
    public DateTime LastSyncedFromExcel { get; set; }
    public string? SyncStatus { get; set; }
    public string? SyncErrors { get; set; }

    // Deduplication
    public string? ContentHash { get; set; }
    public string? UniqueKey { get; set; }           // CABNumber + TableName + ColumnName

    /// <summary>
    /// Generates a unique key for deduplication based on CAB, Table, and Column.
    /// </summary>
    public string GenerateUniqueKey()
    {
        var parts = new[] { CABNumber, TableName, ColumnName }
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => p!.Trim().ToUpperInvariant());
        return string.Join("|", parts);
    }

    /// <summary>
    /// Generates a content hash to detect changes in the entry.
    /// </summary>
    public string GenerateContentHash()
    {
        var content = $"{CABNumber}|{JiraNumber}|{TableName}|{ColumnName}|{ChangeType}|{Description}|{DocId}";
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}

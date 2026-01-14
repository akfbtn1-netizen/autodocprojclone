// DocumentChangeDetails.cs
// DTO for retrieving document details from DocumentChanges table

namespace Enterprise.Documentation.Core.Application.DTOs;

/// <summary>
/// Document change details retrieved from DocumentChanges table
/// Used during post-approval workflow to get metadata for MasterIndex population
/// </summary>
public class DocumentChangeDetails
{
    /// <summary>
    /// Document identifier (BR-0001, EN-0042, etc.)
    /// </summary>
    public string DocId { get; set; } = string.Empty;

    /// <summary>
    /// Table name if this change involves a table
    /// </summary>
    public string? TableName { get; set; }

    /// <summary>
    /// Schema name (dbo, etc.)
    /// </summary>
    public string? SchemaName { get; set; }

    /// <summary>
    /// Column name if this is a column-level change
    /// </summary>
    public string? ColumnName { get; set; }

    /// <summary>
    /// Type of change (Enhancement, Defect, Business Request, ADD_COLUMN, etc.)
    /// </summary>
    public string? ChangeType { get; set; }

    /// <summary>
    /// Description of the change from Excel
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Person assigned to this change
    /// </summary>
    public string? AssignedTo { get; set; }

    /// <summary>
    /// Stored procedure name if this change involves a stored procedure
    /// </summary>
    public string? StoredProcedureName { get; set; }

    /// <summary>
    /// JIRA ticket number
    /// </summary>
    public string? JiraNumber { get; set; }

    /// <summary>
    /// Change applied description
    /// </summary>
    public string? ChangeApplied { get; set; }

    /// <summary>
    /// Location of code change
    /// </summary>
    public string? LocationOfCodeChange { get; set; }
}
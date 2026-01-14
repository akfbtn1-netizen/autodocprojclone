namespace Enterprise.Documentation.Shared.Contracts.DTOs;

/// <summary>
/// Summary DTO for MasterIndex records displayed in lists and grids.
/// Contains essential fields for document catalog overview.
/// </summary>
public record MasterIndexSummaryDto
{
    /// <summary>Primary key identifier</summary>
    public int IndexId { get; init; }

    /// <summary>Database containing the object</summary>
    public string? DatabaseName { get; init; }

    /// <summary>Schema containing the object</summary>
    public string? SchemaName { get; init; }

    /// <summary>Physical name of the table/view/procedure</summary>
    public string? TableName { get; init; }

    /// <summary>Column name (for column-level documentation)</summary>
    public string? ColumnName { get; init; }

    /// <summary>Type of database object (Table, View, StoredProcedure, etc.)</summary>
    public string? ObjectType { get; init; }

    /// <summary>Current approval status (Draft, Pending, Approved, Rejected)</summary>
    public string? ApprovalStatus { get; init; }

    /// <summary>Current workflow status</summary>
    public string? WorkflowStatus { get; init; }

    /// <summary>Date of last modification</summary>
    public DateTime? LastModifiedDate { get; init; }

    /// <summary>Business domain category</summary>
    public string? Category { get; init; }

    /// <summary>Documentation tier (1=Complex, 2=Standard, 3=Simple)</summary>
    public string? Tier { get; init; }

    /// <summary>Full object path (Database.Schema.Object)</summary>
    public string? ObjectPath { get; init; }
}

/// <summary>
/// Generic paginated response wrapper for list endpoints.
/// </summary>
/// <typeparam name="T">Type of items in the response</typeparam>
public record PaginatedResponse<T>
{
    /// <summary>Collection of items for the current page</summary>
    public List<T> Items { get; init; } = new();

    /// <summary>Current page number (1-based)</summary>
    public int PageNumber { get; init; }

    /// <summary>Number of items per page</summary>
    public int PageSize { get; init; }

    /// <summary>Total number of items across all pages</summary>
    public int TotalCount { get; init; }

    /// <summary>Total number of pages</summary>
    public int TotalPages { get; init; }

    /// <summary>Whether there is a previous page</summary>
    public bool HasPreviousPage => PageNumber > 1;

    /// <summary>Whether there is a next page</summary>
    public bool HasNextPage => PageNumber < TotalPages;
}

/// <summary>
/// Statistics DTO for dashboard and reporting endpoints.
/// Provides aggregated counts and breakdowns by category.
/// </summary>
public record MasterIndexStatisticsDto
{
    /// <summary>Total number of documented objects</summary>
    public int TotalDocuments { get; init; }

    /// <summary>Count of documents in Draft status</summary>
    public int DraftCount { get; init; }

    /// <summary>Count of documents pending approval</summary>
    public int PendingCount { get; init; }

    /// <summary>Count of approved documents</summary>
    public int ApprovedCount { get; init; }

    /// <summary>Count of rejected documents</summary>
    public int RejectedCount { get; init; }

    /// <summary>Document counts grouped by business category</summary>
    public Dictionary<string, int> ByCategory { get; init; } = new();

    /// <summary>Document counts grouped by object type</summary>
    public Dictionary<string, int> ByObjectType { get; init; } = new();

    /// <summary>Document counts grouped by database</summary>
    public Dictionary<string, int> ByDatabase { get; init; } = new();

    /// <summary>Document counts grouped by tier</summary>
    public Dictionary<string, int> ByTier { get; init; } = new();

    /// <summary>Timestamp when statistics were computed</summary>
    public DateTime ComputedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Detailed DTO for single MasterIndex record view.
/// Contains all relevant fields for detailed display.
/// </summary>
public record MasterIndexDetailDto
{
    // Identity
    public int IndexId { get; init; }
    public string? PhysicalName { get; init; }
    public string? LogicalName { get; init; }
    public string? SchemaName { get; init; }
    public string? DatabaseName { get; init; }
    public string? ServerName { get; init; }
    public string? ObjectType { get; init; }
    public string? ColumnName { get; init; }

    // Classification
    public string? Category { get; init; }
    public string? SubCategory { get; init; }
    public string? BusinessDomain { get; init; }
    public string? Tier { get; init; }
    public List<string> Tags { get; init; } = new();

    // Documentation
    public string? Description { get; init; }
    public string? TechnicalSummary { get; init; }
    public string? BusinessPurpose { get; init; }
    public string? UsageNotes { get; init; }

    // Document Generation
    public string? GeneratedDocPath { get; init; }
    public DateTime? GeneratedDate { get; init; }
    public string? ApprovalStatus { get; init; }
    public string? ApprovedBy { get; init; }
    public DateTime? ApprovedDate { get; init; }
    public string? DocumentVersion { get; init; }

    // Data Type (for columns)
    public string? DataType { get; init; }
    public int? MaxLength { get; init; }
    public bool? IsNullable { get; init; }
    public string? DefaultValue { get; init; }

    // Compliance
    public bool? PIIIndicator { get; init; }
    public string? DataClassification { get; init; }
    public string? SecurityLevel { get; init; }

    // Audit
    public DateTime? CreatedDate { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime? ModifiedDate { get; init; }
    public string? ModifiedBy { get; init; }
    public bool IsActive { get; init; } = true;
}

/// <summary>
/// Search request parameters for MasterIndex queries.
/// </summary>
public record MasterIndexSearchRequest
{
    /// <summary>Free text search query</summary>
    public string? Query { get; init; }

    /// <summary>Filter by database name</summary>
    public string? DatabaseName { get; init; }

    /// <summary>Filter by schema name</summary>
    public string? SchemaName { get; init; }

    /// <summary>Filter by object type</summary>
    public string? ObjectType { get; init; }

    /// <summary>Filter by approval status</summary>
    public string? ApprovalStatus { get; init; }

    /// <summary>Filter by category</summary>
    public string? Category { get; init; }

    /// <summary>Filter by tier</summary>
    public int? Tier { get; init; }

    /// <summary>Page number (1-based, default: 1)</summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>Items per page (default: 20, max: 100)</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>Sort field</summary>
    public string? SortBy { get; init; }

    /// <summary>Sort direction (asc/desc)</summary>
    public string? SortDirection { get; init; } = "asc";
}

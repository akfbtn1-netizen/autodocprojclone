namespace Core.Domain.Entities;

/// <summary>
/// Document permission entity for access control.
/// Defines who can perform what actions on specific documents.
/// </summary>
public class DocumentPermission : BaseEntity
{
    /// <summary>Document this permission applies to</summary>
    public Guid DocumentId { get; set; }

    /// <summary>Navigation property to the document</summary>
    public Document Document { get; set; } = null!;

    /// <summary>User or role ID this permission is granted to</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Type of permission granted</summary>
    public DocumentPermissionType PermissionType { get; set; }

    /// <summary>When this permission expires (if applicable)</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Whether this permission is currently active</summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Document change tracking entity for audit trail.
/// Records all changes made to documents for compliance and history.
/// </summary>
public class DocumentChange : BaseEntity
{
    /// <summary>Document that was changed</summary>
    public Guid DocumentId { get; set; }

    /// <summary>Navigation property to the document</summary>
    public Document Document { get; set; } = null!;

    /// <summary>Type of change that was made</summary>
    public DocumentChangeType ChangeType { get; set; }

    /// <summary>Description of what changed</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Previous value (if applicable)</summary>
    public string? OldValue { get; set; }

    /// <summary>New value (if applicable)</summary>
    public string? NewValue { get; set; }

    /// <summary>Additional metadata about the change</summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Schema change entity for tracking database schema modifications.
/// Important for maintaining data integrity and migration history.
/// </summary>
public class SchemaChange : BaseEntity
{
    /// <summary>Name of the database table affected</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>Name of the column affected (if applicable)</summary>
    public string? ColumnName { get; set; }

    /// <summary>Type of schema change</summary>
    public SchemaChangeType ChangeType { get; set; }

    /// <summary>Description of the schema change</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>SQL script that was executed</summary>
    public string? SqlScript { get; set; }

    /// <summary>Migration version or identifier</summary>
    public string? MigrationVersion { get; set; }

    /// <summary>Whether the change was successfully applied</summary>
    public bool IsSuccessful { get; set; }

    /// <summary>Error message if the change failed</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>How long the change took to execute</summary>
    public TimeSpan ExecutionDuration { get; set; }
}

/// <summary>
/// Master index entry for centralized document indexing.
/// Provides fast lookup and search capabilities across all documents.
/// </summary>
public class MasterIndexEntry : BaseEntity
{
    /// <summary>Document being indexed</summary>
    public Guid DocumentId { get; set; }

    /// <summary>Navigation property to the document</summary>
    public Document Document { get; set; } = null!;

    /// <summary>Indexed keywords for search</summary>
    public List<string> Keywords { get; set; } = new();

    /// <summary>Full-text content for search indexing</summary>
    public string? IndexedContent { get; set; }

    /// <summary>Search weight/relevance score</summary>
    public double SearchWeight { get; set; } = 1.0;

    /// <summary>When the index was last updated</summary>
    public DateTime LastIndexedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Language of the indexed content</summary>
    public string Language { get; set; } = "en";

    /// <summary>Additional search metadata</summary>
    public Dictionary<string, object> SearchMetadata { get; set; } = new();

    /// <summary>Updates the search index with new content</summary>
    public void UpdateIndex(string content, List<string> keywords, string updatedBy)
    {
        IndexedContent = content;
        Keywords = keywords;
        LastIndexedAt = DateTime.UtcNow;
        UpdateAuditInfo(updatedBy);
    }
}

/// <summary>
/// Document permission types for access control.
/// </summary>
public enum DocumentPermissionType
{
    /// <summary>Can view the document</summary>
    Read = 0,
    /// <summary>Can view and edit the document</summary>
    Write = 1,
    /// <summary>Can view, edit, and delete the document</summary>
    Delete = 2,
    /// <summary>Can perform all actions including permission management</summary>
    FullControl = 3
}

/// <summary>
/// Document change types for audit tracking.
/// </summary>
public enum DocumentChangeType
{
    /// <summary>Document was created</summary>
    Created = 0,
    /// <summary>Document content was updated</summary>
    ContentUpdate = 1,
    /// <summary>Document metadata was updated</summary>
    MetadataUpdate = 2,
    /// <summary>Document was published</summary>
    Published = 3,
    /// <summary>Document was archived</summary>
    Archived = 4,
    /// <summary>Document permissions were changed</summary>
    PermissionChanged = 5,
    /// <summary>Document was moved or renamed</summary>
    Moved = 6,
    /// <summary>Document was soft deleted</summary>
    Deleted = 7,
    /// <summary>Document was restored</summary>
    Restored = 8
}

/// <summary>
/// Schema change types for database modifications.
/// </summary>
public enum SchemaChangeType
{
    /// <summary>Table was created</summary>
    CreateTable = 0,
    /// <summary>Table was dropped</summary>
    DropTable = 1,
    /// <summary>Table was renamed</summary>
    RenameTable = 2,
    /// <summary>Column was added</summary>
    AddColumn = 3,
    /// <summary>Column was dropped</summary>
    DropColumn = 4,
    /// <summary>Column was modified</summary>
    ModifyColumn = 5,
    /// <summary>Column was renamed</summary>
    RenameColumn = 6,
    /// <summary>Index was created</summary>
    CreateIndex = 7,
    /// <summary>Index was dropped</summary>
    DropIndex = 8,
    /// <summary>Constraint was added</summary>
    AddConstraint = 9,
    /// <summary>Constraint was dropped</summary>
    DropConstraint = 10
}
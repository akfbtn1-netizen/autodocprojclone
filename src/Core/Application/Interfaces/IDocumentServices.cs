using Enterprise.Documentation.Core.Application.DTOs;
using Enterprise.Documentation.Core.Domain.Entities;

namespace Enterprise.Documentation.Core.Application.Interfaces;

/// <summary>
/// Interface for setting and retrieving custom properties in DOCX files.
/// </summary>
public interface IDocxCustomPropertiesService
{
    /// <summary>
    /// Set custom properties in a DOCX document.
    /// </summary>
    Task SetPropertiesAsync(
        string filePath,
        DocumentCustomProperties properties,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get custom properties from a DOCX document.
    /// </summary>
    Task<DocumentCustomProperties?> GetPropertiesAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update sync status property in document.
    /// </summary>
    Task UpdateSyncStatusAsync(
        string filePath,
        string status,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for managing approval workflow requests.
/// </summary>
public interface IApprovalService
{
    // Core CRUD operations that exist as wrapper methods
    Task<ApprovalEntity?> GetByIdAsync(int approvalId);
    Task<List<ApprovalEntity>> GetAllPendingAsync();
    Task<ApprovalEntity> CreateAsync(ApprovalEntity approval);
    Task<bool> CancelAsync(int approvalId);
    Task<List<ApprovalEntity>> GetByStatusAsync(string status);
    Task<ApprovalEntity?> GetByDocumentAsync(string jiraNumber, string documentType, string objectName, string schemaName);
    
    // Rich interface methods that actually exist in the service
    Task<ApprovalEntity> CreateAsync(CreateApprovalRequest request, CancellationToken cancellationToken = default);
    Task<Guid> CreateApprovalRequestAsync(CreateApprovalRequest request);
    Task<ApprovalEntity?> GetByIdAsync(Guid approvalId, CancellationToken cancellationToken = default);
    Task<ApprovalResult> ApproveAsync(Guid approvalId, ApprovalDecision decision, CancellationToken cancellationToken = default);
    Task<ApprovalResult> RejectAsync(Guid approvalId, RejectionDecision decision, CancellationToken cancellationToken = default);
    Task<PagedResult<ApprovalSummary>> GetApprovalsAsync(int page, int pageSize, string? status, CancellationToken cancellationToken = default);
    Task<EditResult> EditAsync(Guid approvalId, EditDecision decision, CancellationToken cancellationToken = default);
    Task<ExcelChangeEntry?> GetOriginalEntryAsync(Guid approvalId, CancellationToken cancellationToken = default);
    Task<ApprovalResult> UpdateDocumentAsync(Guid approvalId, UpdateDocumentRequest request, CancellationToken cancellationToken = default);
    Task<SuggestionResult> AddSuggestionAsync(Guid approvalId, Suggestion suggestion, CancellationToken cancellationToken = default);
    Task<ApprovalDetails?> GetDetailsAsync(Guid approvalId, CancellationToken cancellationToken = default);
    Task<ApprovalStats> GetStatsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for schema metadata operations
/// </summary>
public interface ISchemaMetadataService
{
    // Basic queries
    Task<List<string>> GetSchemasAsync();
    Task<List<string>> GetTablesAsync(string schemaName);
    Task<List<string>> GetStoredProceduresAsync(string schemaName);
    Task<List<ColumnInfo>> GetColumnsAsync(string schemaName, string tableName);
    
    // Descriptions
    Task<string?> GetTableDescriptionAsync(string schemaName, string tableName);
    Task<string?> GetProcedureDescriptionAsync(string schemaName, string procedureName);
    
    // Rich interface methods that actually exist in the service
    Task<SchemaMetadata> GetMetadataAsync(string schemaName, string objectName, CancellationToken cancellationToken = default);
    Task<SchemaMetadata> ExtractMetadataAsync(string schemaName, string objectName);
    Task<SchemaStats> GetSchemaStatsAsync(string schemaName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for caching operations
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
    Task RemoveAsync(string key);
    Task<bool> ExistsAsync(string key);
}
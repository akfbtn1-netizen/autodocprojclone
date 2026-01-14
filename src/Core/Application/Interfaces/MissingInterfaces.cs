using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.ValueObjects;
using Enterprise.Documentation.Core.Application.DTOs;
using System.Linq.Expressions;

namespace Enterprise.Documentation.Core.Application.Interfaces;

/// <summary>
/// Interface for document repository operations - aligned with actual entity structure.
/// </summary>
public interface IDocumentRepository
{
    Task<Document?> GetByIdAsync(DocumentId id, CancellationToken cancellationToken = default);
    Task<List<Document>> GetByUserIdAsync(UserId userId);
    Task<List<Document>> GetPendingApprovalsAsync();
    Task<List<Document>> SearchAsync(string query);
    Task<List<Document>> FindAsync(Expression<Func<Document, bool>> predicate);
    Task<List<Document>> FindAsync(Expression<Func<Document, bool>> predicate, int skip, int take, CancellationToken cancellationToken);
    Task<int> CountAsync(Expression<Func<Document, bool>> predicate);
    Task<int> CountAsync(Expression<Func<Document, bool>> predicate, CancellationToken cancellationToken);
    Task<PagedResult<Document>> GetPagedAsync(int pageNumber, int pageSize, string? filter = null);
    Task<PagedResult<Document>> GetPagedAsync(int pageNumber, int pageSize, string? filter, CancellationToken cancellationToken);
    Task<Document> AddAsync(Document document);
    Task<Document> AddAsync(Document document, CancellationToken cancellationToken);
    Task UpdateAsync(Document document);
    Task UpdateAsync(Document document, CancellationToken cancellationToken);
    Task DeleteAsync(DocumentId id);
}

/// <summary>
/// Interface for template repository operations - aligned with actual entity structure.
/// </summary>
public interface ITemplateRepository
{
    Task<Template?> GetByIdAsync(TemplateId id);
    Task<List<Template>> GetByTypeAsync(string documentType);
    Task<Template> AddAsync(Template template);
    Task UpdateAsync(Template template);
    Task DeleteAsync(TemplateId id);
}

/// <summary>
/// Interface for user repository operations - aligned with actual entity structure.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(UserId userId);
    Task<User?> GetByEmailAsync(string email);
    Task<List<User>> GetAllAsync();
    Task<User> AddAsync(User user);
    Task UpdateAsync(User user);
    Task DeleteAsync(UserId userId);
}

// All other missing interfaces that were in the original file
public interface ICurrentUserService
{
    string? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    Task<User?> GetCurrentUserAsync(CancellationToken cancellationToken = default);
}

public interface IAuthorizationService  
{
    Task<bool> CanAccessDocumentAsync(string userId, int documentId);
    Task<bool> CanAccessDocumentAsync(User user, Document document, CancellationToken cancellationToken);
    Task<bool> CanApproveDocumentAsync(string userId, int documentId);
    Task<bool> HasRoleAsync(string userId, string role);
    Task<bool> AuthorizeAsync(string userId, string resource, string action);
    Task<bool> AuthorizeAsync(string userId, string resource, string action, CancellationToken cancellationToken);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}

public interface IExcelSyncService
{
    Task<bool> SyncSchemaMetadataAsync(string schemaName);
    Task<bool> SyncTableMetadataAsync(string schemaName, string tableName);
    Task<List<string>> GetAllSchemasAsync();
}

public interface IAzureOpenAIService
{
    Task<string> GenerateDocumentationAsync(string prompt, Dictionary<string, object> context);
    Task<string> ClassifyTierAsync(string objectName, string documentType, Dictionary<string, object> metadata);
    Task<string> GenerateDescriptionAsync(string objectType, string objectName, Dictionary<string, object> metadata);
}

public interface ITierClassifierService
{
    Task<int> ClassifyTierAsync(string documentType, string objectName, Dictionary<string, object> metadata);
    Task<int> ClassifyAsync(string documentType, string objectName, Dictionary<string, object> metadata);
    Task<(int tier, string reason)> ClassifyWithReasonAsync(string documentType, string objectName, Dictionary<string, object> metadata);
}

public interface ITemplateSelector
{
    Task<string> SelectTemplateAsync(string documentType, int tier, Dictionary<string, object> context);
    Task<List<string>> GetAvailableTemplatesAsync(string documentType);
}

public interface INodeJsTemplateExecutor
{
    Task<string> ExecuteTemplateAsync(string templatePath, Dictionary<string, object> data);
    Task<string> ExecuteAsync(string templatePath, Dictionary<string, object> data);
    Task<bool> ValidateTemplateAsync(string templatePath);
}

public interface IMasterIndexRepository
{
    Task<MasterIndex?> GetByIdAsync(int id);
    Task<MasterIndex?> GetByJiraAndObjectAsync(string jiraNumber, string documentType, string objectName, string schemaName);
    Task<List<MasterIndex>> GetByStatusAsync(string status);
    Task<List<MasterIndex>> GetByTierAsync(int tier);
    Task<MasterIndex> AddAsync(MasterIndex entity);
    Task UpdateAsync(MasterIndex entity);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(string jiraNumber, string documentType, string objectName, string schemaName);
    
    // ===== WRITE OPERATIONS =====
    
    /// <summary>
    /// Adds a new MasterIndex entry.
    /// </summary>
    /// <param name="entity">The entity to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The added entity with generated ID</returns>
    Task<MasterIndex> AddAsync(MasterIndex entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing MasterIndex entry.
    /// Updates all fields of the entity.
    /// </summary>
    /// <param name="entity">The entity to update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateAsync(MasterIndex entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates specific fields of a MasterIndex entry.
    /// More efficient than UpdateAsync when only updating a few fields.
    /// </summary>
    /// <param name="indexId">The ID of the entry to update</param>
    /// <param name="fieldsToUpdate">Dictionary of field names and values to update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateFieldsAsync(
        int indexId, 
        Dictionary<string, object?> fieldsToUpdate, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a MasterIndex entry (soft delete - sets IsActive = false).
    /// </summary>
    /// <param name="indexId">The ID of the entry to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteAsync(int indexId, CancellationToken cancellationToken = default);

    // ===== READ OPERATIONS (Extended) =====

    /// <summary>
    /// Gets all MasterIndex entries with pagination.
    /// </summary>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of entities</returns>
    Task<IReadOnlyList<MasterIndex>> GetAllAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets total count of active MasterIndex entries.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Total count</returns>
    Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets entries by approval status.
    /// </summary>
    /// <param name="approvalStatus">Status to filter by (Draft, Pending, Approved, Rejected)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of entities matching the status</returns>
    Task<IReadOnlyList<MasterIndex>> GetByApprovalStatusAsync(string approvalStatus, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches MasterIndex entries by text across multiple fields.
    /// </summary>
    /// <param name="searchTerm">Search term to match</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated search results</returns>
    Task<IReadOnlyList<MasterIndex>> SearchAsync(string searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets entries by database name.
    /// </summary>
    /// <param name="databaseName">Database name to filter by</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of entities in the specified database</returns>
    Task<IReadOnlyList<MasterIndex>> GetByDatabaseAsync(string databaseName, CancellationToken cancellationToken = default);
}
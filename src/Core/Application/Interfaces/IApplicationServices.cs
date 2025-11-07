
using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.ValueObjects;
using Enterprise.Documentation.Core.Domain.Specifications;

namespace Enterprise.Documentation.Core.Application.Interfaces;

/// <summary>
/// Service for accessing current user context.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Gets the current authenticated user.
    /// </summary>
    Task<User?> GetCurrentUserAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current user's ID.
    /// </summary>
    UserId? GetCurrentUserId();

    /// <summary>
    /// Checks if the current user is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }
}

/// <summary>
/// Service for handling authorization checks.
/// </summary>
public interface IAuthorizationService
{
    /// <summary>
    /// Checks if the user is authorized to perform an action.
    /// </summary>
    Task<AuthorizationResult> AuthorizeAsync(
        User user,
        string[] requiredPermissions,
        object? resource = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the user can access a specific document.
    /// </summary>
    Task<bool> CanAccessDocumentAsync(
        User user,
        Document document,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the user can approve documents.
    /// </summary>
    Task<bool> CanApproveDocumentsAsync(
        User user,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of authorization check.
/// </summary>
public record AuthorizationResult(bool IsAuthorized, string? FailureReason = null)
{
    public static AuthorizationResult Success() => new(true);
    public static AuthorizationResult Failure(string reason) => new(false, reason);
}

/// <summary>
/// Repository interface for documents.
/// </summary>
public interface IDocumentRepository
{
    /// <summary>
    /// Gets a document by ID.
    /// </summary>
    Task<Document?> GetByIdAsync(DocumentId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets documents by specification.
    /// </summary>
    Task<IReadOnlyList<Document>> GetBySpecificationAsync(
        ISpecification<Document> specification,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets documents with pagination.
    /// </summary>
    Task<PagedResult<Document>> GetPagedAsync(
        ISpecification<Document>? specification = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new document.
    /// </summary>
    Task<Document> AddAsync(Document document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing document.
    /// </summary>
    Task<Document> UpdateAsync(Document document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a document.
    /// </summary>
    Task DeleteAsync(Document document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds documents matching a specification with pagination.
    /// </summary>
    Task<IReadOnlyList<Document>> FindAsync(
        ISpecification<Document> specification,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts documents matching a specification.
    /// </summary>
    Task<int> CountAsync(
        ISpecification<Document> specification,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for templates.
/// </summary>
public interface ITemplateRepository
{
    /// <summary>
    /// Gets a template by ID.
    /// </summary>
    Task<Template?> GetByIdAsync(TemplateId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active templates.
    /// </summary>
    Task<IReadOnlyList<Template>> GetActiveTemplatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets templates by category.
    /// </summary>
    Task<IReadOnlyList<Template>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new template.
    /// </summary>
    Task<Template> AddAsync(Template template, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing template.
    /// </summary>
    Task<Template> UpdateAsync(Template template, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for users.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Gets a user by ID.
    /// </summary>
    Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by email.
    /// </summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets users by specification.
    /// </summary>
    Task<IReadOnlyList<User>> GetBySpecificationAsync(
        ISpecification<User> specification,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new user.
    /// </summary>
    Task<User> AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing user.
    /// </summary>
    Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for agents.
/// </summary>
public interface IAgentRepository
{
    /// <summary>
    /// Gets an agent by ID.
    /// </summary>
    Task<Agent?> GetByIdAsync(AgentId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets agents by specification.
    /// </summary>
    Task<IReadOnlyList<Agent>> GetBySpecificationAsync(
        ISpecification<Agent> specification,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets available agents for processing.
    /// </summary>
    Task<IReadOnlyList<Agent>> GetAvailableAgentsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new agent.
    /// </summary>
    Task<Agent> AddAsync(Agent agent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing agent.
    /// </summary>
    Task<Agent> UpdateAsync(Agent agent, CancellationToken cancellationToken = default);
}

/// <summary>
/// Unit of work interface for managing transactions.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Saves all changes to the database.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a database transaction.
    /// </summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the current transaction.
    /// </summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the current transaction.
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Paged result wrapper.
/// </summary>
/// <typeparam name="T">The item type</typeparam>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; }
    public int TotalCount { get; }
    public int PageNumber { get; }
    public int PageSize { get; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => PageNumber < TotalPages;
    public bool HasPreviousPage => PageNumber > 1;

    public PagedResult(IReadOnlyList<T> items, int totalCount, int pageNumber, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }
}
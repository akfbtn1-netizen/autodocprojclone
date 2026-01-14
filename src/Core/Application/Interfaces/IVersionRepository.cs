
using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.ValueObjects;


namespace Enterprise.Documentation.Core.Application.Interfaces;

/// <summary>
/// Repository interface for versions.
/// </summary>
public interface IVersionRepository
{
    /// <summary>
    /// Gets a version by ID.
    /// </summary>
    Task<Enterprise.Documentation.Core.Domain.Entities.Version?> GetByIdAsync(VersionId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all versions for a document.
    /// </summary>
    Task<IReadOnlyList<Enterprise.Documentation.Core.Domain.Entities.Version>> GetByDocumentIdAsync(DocumentId documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current version for a document.
    /// </summary>
    Task<Enterprise.Documentation.Core.Domain.Entities.Version?> GetCurrentVersionAsync(DocumentId documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets approvals for a version.
    /// </summary>
    Task<IReadOnlyList<VersionApproval>> GetApprovalsAsync(VersionId versionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new version.
    /// </summary>
    Task<Enterprise.Documentation.Core.Domain.Entities.Version> AddAsync(Enterprise.Documentation.Core.Domain.Entities.Version version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing version.
    /// </summary>
    Task<Enterprise.Documentation.Core.Domain.Entities.Version> UpdateAsync(Enterprise.Documentation.Core.Domain.Entities.Version version, CancellationToken cancellationToken = default);
}
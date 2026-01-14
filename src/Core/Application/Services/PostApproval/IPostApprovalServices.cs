// =============================================================================
// Agent #5: Post-Approval Pipeline - Service Interfaces
// Defines contracts for deferred embedding, Shadow Metadata, MasterIndex population
// =============================================================================

namespace Enterprise.Documentation.Core.Application.Services.PostApproval;

/// <summary>
/// Finalizes metadata at approval time - generates embeddings and classifications.
/// Called ONLY when a document is approved, not during draft creation.
/// </summary>
public interface IMetadataFinalizationService
{
    /// <summary>
    /// Finalizes metadata after document approval - generates embedding and enriches classification
    /// </summary>
    Task<FinalizedMetadata> FinalizeMetadataAsync(
        int approvalId,
        string documentPath,
        ExtractedMetadata draftMetadata,
        CancellationToken ct = default);

    /// <summary>
    /// Generates semantic embedding for the approved document content (1536 dimensions for ada-002)
    /// </summary>
    Task<float[]> GenerateEmbeddingAsync(string documentContent, CancellationToken ct = default);

    /// <summary>
    /// Enriches classification with AI-generated domain tags and business process classification
    /// </summary>
    Task<EnrichedClassification> EnrichClassificationAsync(
        string documentContent,
        ExtractedMetadata baseMetadata,
        CancellationToken ct = default);
}

/// <summary>
/// Stamps custom properties to .docx files for Shadow Metadata tracking.
/// Enables documents to be self-aware of their synchronization state.
/// </summary>
public interface IMetadataStampingService
{
    /// <summary>
    /// Stamps custom properties to the Word document
    /// </summary>
    Task<StampingResult> StampDocumentAsync(
        string documentPath,
        FinalizedMetadata metadata,
        CancellationToken ct = default);

    /// <summary>
    /// Reads Shadow Metadata from document custom properties
    /// </summary>
    Task<ShadowMetadata?> ReadShadowMetadataAsync(string documentPath, CancellationToken ct = default);

    /// <summary>
    /// Validates document sync status against database
    /// </summary>
    Task<SyncStatus> ValidateSyncStatusAsync(string documentPath, CancellationToken ct = default);
}

/// <summary>
/// Populates MasterIndex with 115-column records after approval.
/// Central catalog of all documented database objects.
/// </summary>
public interface IMasterIndexPopulationService
{
    /// <summary>
    /// Populates MasterIndex with approved document metadata
    /// </summary>
    Task<PopulationResult> PopulateAsync(
        int approvalId,
        FinalizedMetadata metadata,
        CancellationToken ct = default);

    /// <summary>
    /// Updates existing MasterIndex entry
    /// </summary>
    Task<PopulationResult> UpdateAsync(
        int masterIndexId,
        FinalizedMetadata metadata,
        CancellationToken ct = default);

    /// <summary>
    /// Links document to existing MasterIndex entry
    /// </summary>
    Task LinkDocumentAsync(int masterIndexId, string documentId, string documentPath, CancellationToken ct = default);
}

/// <summary>
/// Extracts column-level lineage from stored procedures using ScriptDom.
/// Provides impact analysis for schema changes.
/// </summary>
public interface IColumnLineageService
{
    /// <summary>
    /// Extracts column lineage from a stored procedure
    /// </summary>
    Task<LineageExtractionResult> ExtractLineageAsync(
        string schemaName,
        string procedureName,
        string procedureDefinition,
        CancellationToken ct = default);

    /// <summary>
    /// Gets impact analysis for a column change
    /// </summary>
    Task<ImpactAnalysisResult> AnalyzeImpactAsync(
        string schemaName,
        string tableName,
        string columnName,
        CancellationToken ct = default);

    /// <summary>
    /// Gets all downstream dependencies for an object
    /// </summary>
    Task<List<LineageDependency>> GetDownstreamDependenciesAsync(
        string schemaName,
        string objectName,
        CancellationToken ct = default);
}

/// <summary>
/// Orchestrates the complete post-approval pipeline.
/// Coordinates all post-approval services in sequence.
/// </summary>
public interface IPostApprovalOrchestrator
{
    /// <summary>
    /// Executes the complete post-approval pipeline:
    /// 1. Finalize Metadata (embedding + classification)
    /// 2. Stamp Document (Shadow Metadata)
    /// 3. Populate MasterIndex (115 columns)
    /// 4. Extract Lineage (if stored procedure)
    /// 5. Broadcast Updates (SignalR)
    /// </summary>
    Task<PostApprovalResult> ExecuteAsync(
        int approvalId,
        string approvedBy,
        string? comments = null,
        CancellationToken ct = default);

    /// <summary>
    /// Retries a failed processing step
    /// </summary>
    Task<PostApprovalResult> RetryStepAsync(
        int approvalId,
        string stepName,
        CancellationToken ct = default);
}

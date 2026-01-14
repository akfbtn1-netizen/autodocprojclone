// <copyright file="IStoredProcedureDocumentationService.cs" company="Enterprise Documentation Platform">
// Copyright (c) Enterprise Documentation Platform. All rights reserved.
// </copyright>

namespace Enterprise.Documentation.Core.Application.Interfaces.Services;

/// <summary>
/// Service for managing stored procedure documentation
/// </summary>
public interface IStoredProcedureDocumentationService
{
    /// <summary>
    /// Checks if documentation exists for a stored procedure.
    /// </summary>
    Task<bool> SPDocumentationExistsAsync(string procedureName, string schemaName = "dbo", CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates documentation for a stored procedure.
    /// </summary>
    Task<string> CreateOrUpdateSPDocumentationAsync(string procedureName, string schemaName = "dbo", string? description = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets existing documentation for a stored procedure.
    /// </summary>
    Task<string?> GetSPDocumentationAsync(string procedureName, string schemaName = "dbo", CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates documentation for multiple stored procedures.
    /// </summary>
    Task<List<(string ProcedureName, string DocumentationPath)>> GenerateBulkDocumentationAsync(List<string> procedureNames, string schemaName = "dbo", CancellationToken cancellationToken = default);
}
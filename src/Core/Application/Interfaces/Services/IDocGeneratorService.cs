// <copyright file="IDocGeneratorService.cs" company="Enterprise Documentation Platform">
// Copyright (c) Enterprise Documentation Platform. All rights reserved.
// </copyright>

namespace Enterprise.Documentation.Core.Application.Interfaces.Services;

/// <summary>
/// Service interface for generating documentation.
/// </summary>
public interface IDocGeneratorService
{
    /// <summary>
    /// Generates documentation for a given source.
    /// </summary>
    /// <param name="source">The source to generate documentation for.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The generated documentation content.</returns>
    Task<string> GenerateDocumentationAsync(string source, CancellationToken cancellationToken = default);
}
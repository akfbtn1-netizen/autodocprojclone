// <copyright file="IOpenAIEnhancementService.cs" company="Enterprise Documentation Platform">
// Copyright (c) Enterprise Documentation Platform. All rights reserved.
// </copyright>

using Enterprise.Documentation.Core.Application.DTOs;

namespace Enterprise.Documentation.Core.Application.Interfaces.Services;

/// <summary>
/// Service for AI-powered documentation enhancement using OpenAI
/// </summary>
public interface IOpenAIEnhancementService
{
    /// <summary>
    /// Enhances documentation content using AI.
    /// </summary>
    Task<DocumentationEnhancementResult> EnhanceDocumentationAsync(DocumentationEnhancementRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes documentation quality and suggests improvements.
    /// </summary>
    Task<List<string>> AnalyzeDocumentationQualityAsync(string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates documentation from code or schema information.
    /// </summary>
    Task<string> GenerateDocumentationAsync(string sourceCode, string documentType, CancellationToken cancellationToken = default);
}
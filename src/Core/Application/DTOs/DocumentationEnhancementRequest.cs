// <copyright file="DocumentationEnhancementRequest.cs" company="Enterprise Documentation Platform">
// Copyright (c) Enterprise Documentation Platform. All rights reserved.
// This software is proprietary and confidential.
// </copyright>

namespace Enterprise.Documentation.Core.Application.DTOs;

/// <summary>
/// Request for AI-powered documentation enhancement.
/// </summary>
public class DocumentationEnhancementRequest
{
    /// <summary>
    /// The document or text content to enhance.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Type of enhancement requested (e.g., "improve_clarity", "add_examples", "technical_review").
    /// </summary>
    public string EnhancementType { get; set; } = string.Empty;

    /// <summary>
    /// Context information to help with enhancement.
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    /// Target audience for the documentation.
    /// </summary>
    public string? TargetAudience { get; set; }

    /// <summary>
    /// Specific areas to focus on during enhancement.
    /// </summary>
    public List<string> FocusAreas { get; set; } = new();

    /// <summary>
    /// Maximum length of the enhanced content.
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// Whether to preserve the original structure.
    /// </summary>
    public bool PreserveStructure { get; set; } = true;

    // Additional properties expected by MetadataExtractionService
    public string Description { get; set; } = string.Empty;
    public string Documentation { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
    public string Column { get; set; } = string.Empty;
    public string ModifiedStoredProcedures { get; set; } = string.Empty;
    public string? CABNumber { get; set; }
    public string? JiraNumber { get; set; }
}
// <copyright file="DocumentationEnhancementResult.cs" company="Enterprise Documentation Platform">
// Copyright (c) Enterprise Documentation Platform. All rights reserved.
// </copyright>

namespace Enterprise.Documentation.Core.Application.DTOs;

/// <summary>
/// Result from AI-powered documentation enhancement.
/// </summary>
public class DocumentationEnhancementResult
{
    /// <summary>
    /// Enhanced description content.
    /// </summary>
    public string EnhancedDescription { get; set; } = string.Empty;

    /// <summary>
    /// Enhanced implementation details.
    /// </summary>
    public string EnhancedImplementation { get; set; } = string.Empty;

    /// <summary>
    /// Key points extracted from the content.
    /// </summary>
    public List<string> KeyPoints { get; set; } = new();

    /// <summary>
    /// Overall quality score (0-100).
    /// </summary>
    public int QualityScore { get; set; }

    /// <summary>
    /// Suggested improvements.
    /// </summary>
    public List<string> Improvements { get; set; } = new();

    /// <summary>
    /// Whether the enhancement was successful.
    /// </summary>
    public bool IsSuccessful { get; set; } = true;

    /// <summary>
    /// Error message if enhancement failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
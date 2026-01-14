// <copyright file="AutoDraftResult.cs" company="Enterprise Documentation Platform">
// Copyright (c) Enterprise Documentation Platform. All rights reserved.
// </copyright>

namespace Enterprise.Documentation.Core.Application.DTOs;

/// <summary>
/// Result from automatic draft creation.
/// </summary>
public class AutoDraftResult
{
    /// <summary>
    /// Whether the draft creation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Generated document ID.
    /// </summary>
    public string? DocId { get; set; }

    /// <summary>
    /// File path where the document was created.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Error message if creation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Additional metadata about the created document.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Timestamp when the draft was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Warnings encountered during draft creation.
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}
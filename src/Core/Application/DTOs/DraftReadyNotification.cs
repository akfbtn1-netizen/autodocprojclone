// <copyright file="DraftReadyNotification.cs" company="Enterprise Documentation Platform">
// Copyright (c) Enterprise Documentation Platform. All rights reserved.
// This software is proprietary and confidential.
// </copyright>

namespace Enterprise.Documentation.Core.Application.DTOs;

/// <summary>
/// Notification data for when a document draft is ready for approval.
/// </summary>
public class DraftReadyNotification
{
    /// <summary>
    /// Document ID of the generated draft.
    /// </summary>
    public string DocId { get; set; } = string.Empty;

    /// <summary>
    /// Type of document (SP, EN, etc.).
    /// </summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>
    /// Database table name the document is for.
    /// </summary>
    public string Table { get; set; } = string.Empty;

    /// <summary>
    /// Column name (if applicable).
    /// </summary>
    public string? Column { get; set; }

    /// <summary>
    /// Associated JIRA number.
    /// </summary>
    public string JiraNumber { get; set; } = string.Empty;

    /// <summary>
    /// Description of the document.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// File path where the document was generated.
    /// </summary>
    public string DocumentPath { get; set; } = string.Empty;

    /// <summary>
    /// URL for the approval workflow.
    /// </summary>
    public string ApprovalUrl { get; set; } = string.Empty;

    /// <summary>
    /// Priority level of the approval request.
    /// </summary>
    public string Priority { get; set; } = "Medium";

    /// <summary>
    /// Who requested the document generation.
    /// </summary>
    public string RequestedBy { get; set; } = "System";

    /// <summary>
    /// When the document was generated.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
namespace Enterprise.Documentation.Core.Domain.Entities.Search;

/// <summary>
/// Detailed user interaction tracking for continuous learning.
/// Types: Click, Export, Share, NotHelpful, FollowUp
/// </summary>
public class UserInteraction : BaseEntity
{
    public Guid InteractionId { get; private set; }
    public Guid QueryId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string InteractionType { get; private set; } = string.Empty;
    public string? DocumentId { get; private set; }
    public string? InteractionData { get; private set; } // JSON payload
    public DateTime Timestamp { get; private set; }

    private UserInteraction() { } // EF Core

    public static UserInteraction Create(
        Guid queryId,
        string userId,
        string interactionType,
        string? documentId = null,
        string? interactionData = null)
    {
        return new UserInteraction
        {
            InteractionId = Guid.NewGuid(),
            QueryId = queryId,
            UserId = userId,
            InteractionType = interactionType,
            DocumentId = documentId,
            InteractionData = interactionData,
            Timestamp = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Interaction type constants
/// </summary>
public static class InteractionTypes
{
    public const string Click = "Click";
    public const string Export = "Export";
    public const string Share = "Share";
    public const string NotHelpful = "NotHelpful";
    public const string FollowUp = "FollowUp";
    public const string Feedback = "Feedback";
}

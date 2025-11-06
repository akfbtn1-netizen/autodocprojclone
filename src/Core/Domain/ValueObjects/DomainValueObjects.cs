namespace Core.Domain.ValueObjects;

/// <summary>
/// Value object representing a physical name/identifier.
/// Used for file system storage, external references, and unique naming.
/// Immutable value object with validation and formatting rules.
/// </summary>
public record PhysicalName
{
    /// <summary>The actual physical name value</summary>
    public string Value { get; }

    /// <summary>
    /// Creates a new PhysicalName with validation.
    /// </summary>
    /// <param name="value">Physical name value</param>
    /// <exception cref="ArgumentException">Thrown when value is invalid</exception>
    public PhysicalName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Physical name cannot be null or empty", nameof(value));

        if (value.Length > 255)
            throw new ArgumentException("Physical name cannot exceed 255 characters", nameof(value));

        if (!IsValidPhysicalName(value))
            throw new ArgumentException("Physical name contains invalid characters", nameof(value));

        Value = value.Trim().ToLowerInvariant();
    }

    /// <summary>Empty physical name for default values</summary>
    public static PhysicalName Empty => new("empty");

    /// <summary>
    /// Creates a PhysicalName from a display title.
    /// Converts spaces to dashes and removes invalid characters.
    /// </summary>
    /// <param name="title">Display title to convert</param>
    /// <returns>Valid PhysicalName</returns>
    public static PhysicalName FromTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Empty;

        // Convert to lowercase and replace spaces with dashes
        var cleaned = title.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("_", "-");

        // Remove invalid characters
        var validChars = cleaned.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '.');
        var result = new string(validChars.ToArray());

        // Remove duplicate dashes
        while (result.Contains("--"))
            result = result.Replace("--", "-");

        // Trim dashes from ends
        result = result.Trim('-');

        if (string.IsNullOrEmpty(result))
            result = "unnamed";

        return new PhysicalName(result);
    }

    /// <summary>
    /// Validates if a string is a valid physical name.
    /// </summary>
    /// <param name="value">Value to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    private static bool IsValidPhysicalName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        // Only allow letters, numbers, dashes, dots, and underscores
        return value.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '.' || c == '_');
    }

    /// <summary>Implicit conversion to string</summary>
    public static implicit operator string(PhysicalName physicalName) => physicalName.Value;

    /// <summary>Explicit conversion from string</summary>
    public static explicit operator PhysicalName(string value) => new(value);

    /// <summary>String representation</summary>
    public override string ToString() => Value;
}

/// <summary>
/// Value object representing approval status with workflow information.
/// Tracks the current state of document approval process.
/// </summary>
public record ApprovalStatus
{
    /// <summary>Current status value</summary>
    public ApprovalStatusType Status { get; }

    /// <summary>When this status was set</summary>
    public DateTime StatusDate { get; }

    /// <summary>Who set this status</summary>
    public string StatusBy { get; }

    /// <summary>Optional comments about the status</summary>
    public string? Comments { get; }

    /// <summary>
    /// Creates a new ApprovalStatus.
    /// </summary>
    /// <param name="status">Status type</param>
    /// <param name="statusBy">Who set the status</param>
    /// <param name="comments">Optional comments</param>
    public ApprovalStatus(ApprovalStatusType status, string statusBy, string? comments = null)
    {
        Status = status;
        StatusDate = DateTime.UtcNow;
        StatusBy = statusBy ?? string.Empty;
        Comments = comments;
    }

    /// <summary>Draft status for new documents</summary>
    public static ApprovalStatus Draft => new(ApprovalStatusType.Draft, "system");

    /// <summary>Creates a pending review status</summary>
    public static ApprovalStatus PendingReview(string submittedBy, string? comments = null) =>
        new(ApprovalStatusType.PendingReview, submittedBy, comments);

    /// <summary>Creates an approved status</summary>
    public static ApprovalStatus Approved(string approvedBy, string? comments = null) =>
        new(ApprovalStatusType.Approved, approvedBy, comments);

    /// <summary>Creates a rejected status</summary>
    public static ApprovalStatus Rejected(string rejectedBy, string? comments = null) =>
        new(ApprovalStatusType.Rejected, rejectedBy, comments);

    /// <summary>Creates an archived status</summary>
    public static ApprovalStatus Archived(string archivedBy, string? comments = null) =>
        new(ApprovalStatusType.Archived, archivedBy, comments);

    /// <summary>Checks if the status allows editing</summary>
    public bool AllowsEditing => Status is ApprovalStatusType.Draft or ApprovalStatusType.Rejected;

    /// <summary>Checks if the status is final (cannot be changed)</summary>
    public bool IsFinal => Status is ApprovalStatusType.Archived;

    /// <summary>String representation</summary>
    public override string ToString() => $"{Status} by {StatusBy} on {StatusDate:yyyy-MM-dd}";
}

/// <summary>
/// Value object representing security classification levels.
/// Determines access control and handling requirements for documents.
/// </summary>
public record SecurityClassification
{
    /// <summary>Classification level</summary>
    public SecurityLevel Level { get; }

    /// <summary>Human-readable description</summary>
    public string Description { get; }

    /// <summary>Required clearance level to access</summary>
    public int RequiredClearanceLevel { get; }

    /// <summary>
    /// Creates a new SecurityClassification.
    /// </summary>
    /// <param name="level">Security level</param>
    /// <param name="description">Description of the classification</param>
    /// <param name="requiredClearanceLevel">Required clearance level</param>
    public SecurityClassification(SecurityLevel level, string description, int requiredClearanceLevel)
    {
        Level = level;
        Description = description ?? string.Empty;
        RequiredClearanceLevel = requiredClearanceLevel;
    }

    /// <summary>Public classification - no restrictions</summary>
    public static SecurityClassification Public => 
        new(SecurityLevel.Public, "Publicly accessible information", 0);

    /// <summary>Internal classification - company employees only</summary>
    public static SecurityClassification Internal => 
        new(SecurityLevel.Internal, "Internal company information", 1);

    /// <summary>Confidential classification - restricted access</summary>
    public static SecurityClassification Confidential => 
        new(SecurityLevel.Confidential, "Confidential information requiring special access", 2);

    /// <summary>Secret classification - highly restricted</summary>
    public static SecurityClassification Secret => 
        new(SecurityLevel.Secret, "Secret information with strict access controls", 3);

    /// <summary>Top secret classification - maximum security</summary>
    public static SecurityClassification TopSecret => 
        new(SecurityLevel.TopSecret, "Top secret information with maximum security", 4);

    /// <summary>Checks if a clearance level can access this classification</summary>
    public bool CanAccess(int clearanceLevel) => clearanceLevel >= RequiredClearanceLevel;

    /// <summary>String representation</summary>
    public override string ToString() => $"{Level} (Clearance {RequiredClearanceLevel}+)";
}

/// <summary>
/// Approval status types for workflow management.
/// </summary>
public enum ApprovalStatusType
{
    /// <summary>Document is in draft state</summary>
    Draft = 0,
    /// <summary>Document is pending review</summary>
    PendingReview = 1,
    /// <summary>Document has been approved</summary>
    Approved = 2,
    /// <summary>Document has been rejected</summary>
    Rejected = 3,
    /// <summary>Document has been archived</summary>
    Archived = 4
}

/// <summary>
/// Security classification levels.
/// </summary>
public enum SecurityLevel
{
    /// <summary>Publicly accessible</summary>
    Public = 0,
    /// <summary>Internal to organization</summary>
    Internal = 1,
    /// <summary>Confidential information</summary>
    Confidential = 2,
    /// <summary>Secret information</summary>
    Secret = 3,
    /// <summary>Top secret information</summary>
    TopSecret = 4
}
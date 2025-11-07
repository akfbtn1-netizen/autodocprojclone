
using Enterprise.Documentation.Core.Domain.ValueObjects;

namespace Enterprise.Documentation.Core.Domain.Entities;

/// <summary>
/// User entity representing platform users with roles and permissions.
/// Manages authentication, authorization, and user profile information.
/// </summary>
public class User : BaseEntity<UserId>
{
    /// <summary>
    /// User's email address (unique identifier).
    /// </summary>
    public string Email { get; private set; }

    /// <summary>
    /// User's display name.
    /// </summary>
    public string DisplayName { get; private set; }

    /// <summary>
    /// User's first name.
    /// </summary>
    public string? FirstName { get; private set; }

    /// <summary>
    /// User's last name.
    /// </summary>
    public string? LastName { get; private set; }

    /// <summary>
    /// User's department or organizational unit.
    /// </summary>
    public string? Department { get; private set; }

    /// <summary>
    /// User's job title.
    /// </summary>
    public string? JobTitle { get; private set; }

    /// <summary>
    /// User's roles within the platform.
    /// </summary>
    public List<UserRole> Roles { get; private set; }

    /// <summary>
    /// Whether the user account is currently active.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// User's maximum security clearance level.
    /// </summary>
    public SecurityClearanceLevel SecurityClearance { get; private set; }

    /// <summary>
    /// When the user last accessed the platform.
    /// </summary>
    public DateTime? LastAccessAt { get; private set; }

    /// <summary>
    /// User preferences and settings.
    /// </summary>
    public UserPreferences Preferences { get; private set; }

    /// <summary>
    /// User's current approval queue capacity.
    /// </summary>
    public int ApprovalQueueCapacity { get; private set; }

    // Private constructor for EF Core
    private User() : base()
    {
        Email = string.Empty;
        DisplayName = string.Empty;
        Roles = new List<UserRole>();
        IsActive = true;
        SecurityClearance = SecurityClearanceLevel.Public;
        Preferences = UserPreferences.Default();
        ApprovalQueueCapacity = 50;
    }

    /// <summary>
    /// Creates a new user.
    /// </summary>
    public User(
        UserId id,
        string email,
        string displayName,
        SecurityClearanceLevel securityClearance,
        UserId createdBy,
        string? firstName = null,
        string? lastName = null,
        string? department = null,
        string? jobTitle = null,
        List<UserRole>? roles = null) : base(id, createdBy)
    {
        Email = IsValidEmail(email) 
            ? email.ToLowerInvariant() 
            : throw new ArgumentException("Invalid email format", nameof(email));
            
        DisplayName = !string.IsNullOrWhiteSpace(displayName) 
            ? displayName 
            : throw new ArgumentException("Display name cannot be empty", nameof(displayName));
            
        FirstName = firstName;
        LastName = lastName;
        Department = department;
        JobTitle = jobTitle;
        Roles = roles ?? new List<UserRole> { UserRole.Reader };
        IsActive = true;
        SecurityClearance = securityClearance;
        Preferences = UserPreferences.Default();
        ApprovalQueueCapacity = CalculateDefaultApprovalCapacity(Roles);

        AddDomainEvent(new UserCreatedEvent(id, email, displayName, createdBy));
    }

    /// <summary>
    /// Updates user profile information.
    /// </summary>
    public void UpdateProfile(
        string? displayName = null,
        string? firstName = null,
        string? lastName = null,
        string? department = null,
        string? jobTitle = null,
        UserId? updatedBy = null)
    {
        if (updatedBy == null)
            throw new ArgumentNullException(nameof(updatedBy));

        if (!string.IsNullOrWhiteSpace(displayName))
            DisplayName = displayName;

        FirstName = firstName;
        LastName = lastName;
        Department = department;
        JobTitle = jobTitle;

        UpdateModificationTracking(updatedBy);
        AddDomainEvent(new UserProfileUpdatedEvent(Id, updatedBy));
    }

    /// <summary>
    /// Assigns a role to the user.
    /// </summary>
    public void AssignRole(UserRole role, UserId assignedBy)
    {
        if (Roles.Contains(role))
            throw new InvalidOperationException($"User already has role: {role}");

        Roles.Add(role);
        
        // Recalculate approval capacity based on new roles
        ApprovalQueueCapacity = CalculateDefaultApprovalCapacity(Roles);
        
        UpdateModificationTracking(assignedBy);
        AddDomainEvent(new UserRoleAssignedEvent(Id, role, assignedBy));
    }

    /// <summary>
    /// Removes a role from the user.
    /// </summary>
    public void RemoveRole(UserRole role, UserId removedBy)
    {
        if (!Roles.Contains(role))
            throw new InvalidOperationException($"User does not have role: {role}");

        if (Roles.Count == 1)
            throw new InvalidOperationException("Cannot remove the last role from a user");

        Roles.Remove(role);
        
        // Recalculate approval capacity based on remaining roles
        ApprovalQueueCapacity = CalculateDefaultApprovalCapacity(Roles);
        
        UpdateModificationTracking(removedBy);
        AddDomainEvent(new UserRoleRemovedEvent(Id, role, removedBy));
    }

    /// <summary>
    /// Updates the user's security clearance level.
    /// </summary>
    public void UpdateSecurityClearance(SecurityClearanceLevel newClearance, UserId updatedBy)
    {
        var oldClearance = SecurityClearance;
        SecurityClearance = newClearance;
        
        UpdateModificationTracking(updatedBy);
        AddDomainEvent(new UserSecurityClearanceUpdatedEvent(Id, oldClearance, newClearance, updatedBy));
    }

    /// <summary>
    /// Activates the user account.
    /// </summary>
    public void Activate(UserId activatedBy)
    {
        if (IsActive)
            throw new InvalidOperationException("User is already active");

        IsActive = true;
        UpdateModificationTracking(activatedBy);
        AddDomainEvent(new UserActivatedEvent(Id, activatedBy));
    }

    /// <summary>
    /// Deactivates the user account.
    /// </summary>
    public void Deactivate(UserId deactivatedBy)
    {
        if (!IsActive)
            throw new InvalidOperationException("User is already inactive");

        IsActive = false;
        UpdateModificationTracking(deactivatedBy);
        AddDomainEvent(new UserDeactivatedEvent(Id, deactivatedBy));
    }

    /// <summary>
    /// Records user access to the platform.
    /// </summary>
    public void RecordAccess()
    {
        LastAccessAt = DateTime.UtcNow;
        AddDomainEvent(new UserAccessRecordedEvent(Id, LastAccessAt.Value));
    }

    /// <summary>
    /// Updates user preferences.
    /// </summary>
    public void UpdatePreferences(UserPreferences preferences, UserId updatedBy)
    {
        Preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        UpdateModificationTracking(updatedBy);
        AddDomainEvent(new UserPreferencesUpdatedEvent(Id, updatedBy));
    }

    /// <summary>
    /// Checks if the user has a specific role.
    /// </summary>
    public bool HasRole(UserRole role) => Roles.Contains(role);

    /// <summary>
    /// Checks if the user has any of the specified roles.
    /// </summary>
    public bool HasAnyRole(params UserRole[] roles) => roles.Any(role => Roles.Contains(role));

    /// <summary>
    /// Checks if the user has all of the specified roles.
    /// </summary>
    public bool HasAllRoles(params UserRole[] roles) => roles.All(role => Roles.Contains(role));

    /// <summary>
    /// Checks if the user can access content with the specified security classification.
    /// </summary>
    public bool CanAccessSecurityLevel(SecurityClassification classification)
    {
        return SecurityClearance >= GetRequiredClearanceLevel(classification);
    }



    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static int CalculateDefaultApprovalCapacity(List<UserRole> roles)
    {
        // Managers and admins get higher capacity
        if (roles.Contains(UserRole.Administrator))
            return 100;
        if (roles.Contains(UserRole.Manager))
            return 75;
        if (roles.Contains(UserRole.Approver))
            return 50;
        
        return 25; // Default for regular users
    }

    private static SecurityClearanceLevel GetRequiredClearanceLevel(SecurityClassification classification)
    {
        return classification.Level switch
        {
            "Public" => SecurityClearanceLevel.Public,
            "Internal" => SecurityClearanceLevel.Internal,
            "Confidential" => SecurityClearanceLevel.Confidential,
            "Restricted" => SecurityClearanceLevel.Restricted,
            _ => SecurityClearanceLevel.Restricted
        };
    }
}

/// <summary>
/// User roles within the platform.
/// </summary>
public enum UserRole
{
    Reader,
    Contributor,
    Approver,
    Manager,
    Administrator
}

/// <summary>
/// Security clearance levels for users.
/// </summary>
public enum SecurityClearanceLevel
{
    Public = 0,
    Internal = 1,
    Confidential = 2,
    Restricted = 3
}

/// <summary>
/// User preferences and settings.
/// </summary>
public class UserPreferences : BaseValueObject
{
    public string Theme { get; private set; } = "light";
    public string Language { get; private set; } = "en";
    public bool EmailNotifications { get; private set; } = true;
    public bool PushNotifications { get; private set; } = false;
    public string TimeZone { get; private set; } = "UTC";
    public int PageSize { get; private set; } = 25;

    // Parameterless constructor for EF Core
    private UserPreferences()
    {
    }

    public UserPreferences(
        string theme = "light",
        string language = "en",
        bool emailNotifications = true,
        bool pushNotifications = false,
        string timeZone = "UTC",
        int pageSize = 25)
    {
        Theme = theme;
        Language = language;
        EmailNotifications = emailNotifications;
        PushNotifications = pushNotifications;
        TimeZone = timeZone;
        PageSize = pageSize > 0 ? pageSize : 25;
    }

    public static UserPreferences Default() => new();

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Theme;
        yield return Language;
        yield return EmailNotifications;
        yield return PushNotifications;
        yield return TimeZone;
        yield return PageSize;
    }
}

// Domain Events
public record UserCreatedEvent(UserId UserId, string Email, string DisplayName, UserId CreatedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(UserCreatedEvent);
}

public record UserProfileUpdatedEvent(UserId UserId, UserId UpdatedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(UserProfileUpdatedEvent);
}

public record UserRoleAssignedEvent(UserId UserId, UserRole Role, UserId AssignedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(UserRoleAssignedEvent);
}

public record UserRoleRemovedEvent(UserId UserId, UserRole Role, UserId RemovedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(UserRoleRemovedEvent);
}

public record UserSecurityClearanceUpdatedEvent(UserId UserId, SecurityClearanceLevel OldClearance, SecurityClearanceLevel NewClearance, UserId UpdatedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(UserSecurityClearanceUpdatedEvent);
}

public record UserActivatedEvent(UserId UserId, UserId ActivatedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(UserActivatedEvent);
}

public record UserDeactivatedEvent(UserId UserId, UserId DeactivatedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(UserDeactivatedEvent);
}

public record UserAccessRecordedEvent(UserId UserId, DateTime AccessTime) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(UserAccessRecordedEvent);
}

public record UserPreferencesUpdatedEvent(UserId UserId, UserId UpdatedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(UserPreferencesUpdatedEvent);
}
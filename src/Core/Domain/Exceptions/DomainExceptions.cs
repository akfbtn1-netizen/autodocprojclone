
namespace Enterprise.Documentation.Core.Domain.Exceptions;

/// <summary>
/// Base class for all domain-specific exceptions.
/// Provides consistent error handling and categorization.
/// </summary>
public abstract class DomainException : Exception
{
    public string ErrorCode { get; }
    public DateTime OccurredAt { get; }

    protected DomainException(string errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
        OccurredAt = DateTime.UtcNow;
    }

    protected DomainException(string errorCode, string message, Exception innerException) 
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        OccurredAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Exception thrown when a business rule is violated.
/// </summary>
public class BusinessRuleViolationException : DomainException
{
    public string RuleName { get; }

    public BusinessRuleViolationException(string ruleName, string message) 
        : base("BUSINESS_RULE_VIOLATION", message)
    {
        RuleName = ruleName;
    }
}

/// <summary>
/// Exception thrown when attempting to use an inactive template.
/// </summary>
public class InactiveTemplateException : DomainException
{
    public InactiveTemplateException(string message) 
        : base("INACTIVE_TEMPLATE", message) { }
}

/// <summary>
/// Exception thrown when user lacks sufficient security clearance.
/// </summary>
public class InsufficientSecurityClearanceException : DomainException
{
    public InsufficientSecurityClearanceException(string message) 
        : base("INSUFFICIENT_SECURITY_CLEARANCE", message) { }
}

/// <summary>
/// Exception thrown when required template variables are missing.
/// </summary>
public class MissingTemplateVariablesException : DomainException
{
    public List<string> MissingVariables { get; }

    public MissingTemplateVariablesException(string message) 
        : base("MISSING_TEMPLATE_VARIABLES", message)
    {
        MissingVariables = new List<string>();
    }

    public MissingTemplateVariablesException(string message, List<string> missingVariables) 
        : base("MISSING_TEMPLATE_VARIABLES", message)
    {
        MissingVariables = missingVariables;
    }
}

/// <summary>
/// Exception thrown when trying to perform an invalid approval state transition.
/// </summary>
public class InvalidApprovalTransitionException : DomainException
{
    public string CurrentStatus { get; }
    public string AttemptedStatus { get; }

    public InvalidApprovalTransitionException(string currentStatus, string attemptedStatus, string message)
        : base("INVALID_APPROVAL_TRANSITION", message)
    {
        CurrentStatus = currentStatus;
        AttemptedStatus = attemptedStatus;
    }
}

/// <summary>
/// Exception thrown when a document is already published and cannot be modified.
/// </summary>
public class DocumentAlreadyPublishedException : DomainException
{
    public DocumentAlreadyPublishedException(string documentId)
        : base("DOCUMENT_ALREADY_PUBLISHED", $"Document {documentId} is already published and cannot be modified") { }
}

/// <summary>
/// Exception thrown when attempting to publish a document that hasn't been approved.
/// </summary>
public class DocumentNotApprovedException : DomainException
{
    public DocumentNotApprovedException(string documentId)
        : base("DOCUMENT_NOT_APPROVED", $"Document {documentId} must be approved before publishing") { }
}

/// <summary>
/// Exception thrown when an agent exceeds its concurrent request limit.
/// </summary>
public class AgentCapacityExceededException : DomainException
{
    public int CurrentRequests { get; }
    public int MaxCapacity { get; }

    public AgentCapacityExceededException(string agentId, int currentRequests, int maxCapacity)
        : base("AGENT_CAPACITY_EXCEEDED", 
               $"Agent {agentId} is at capacity ({currentRequests}/{maxCapacity} requests)")
    {
        CurrentRequests = currentRequests;
        MaxCapacity = maxCapacity;
    }
}

/// <summary>
/// Exception thrown when an agent is not available for processing requests.
/// </summary>
public class AgentNotAvailableException : DomainException
{
    public string AgentStatus { get; }

    public AgentNotAvailableException(string agentId, string agentStatus)
        : base("AGENT_NOT_AVAILABLE", $"Agent {agentId} is not available (Status: {agentStatus})")
    {
        AgentStatus = agentStatus;
    }
}

/// <summary>
/// Exception thrown when trying to remove the last role from a user.
/// </summary>
public class CannotRemoveLastRoleException : DomainException
{
    public CannotRemoveLastRoleException(string userId)
        : base("CANNOT_REMOVE_LAST_ROLE", $"Cannot remove the last role from user {userId}") { }
}

/// <summary>
/// Exception thrown when a user's approval queue is at capacity.
/// </summary>
public class ApprovalQueueFullException : DomainException
{
    public int CurrentCapacity { get; }
    public int MaxCapacity { get; }

    public ApprovalQueueFullException(string userId, int currentCapacity, int maxCapacity)
        : base("APPROVAL_QUEUE_FULL", 
               $"User {userId} approval queue is full ({currentCapacity}/{maxCapacity})")
    {
        CurrentCapacity = currentCapacity;
        MaxCapacity = maxCapacity;
    }
}

/// <summary>
/// Exception thrown when attempting to approve/reject without proper authorization.
/// </summary>
public class UnauthorizedApprovalException : DomainException
{
    public UnauthorizedApprovalException(string userId, string documentId)
        : base("UNAUTHORIZED_APPROVAL", 
               $"User {userId} is not authorized to approve document {documentId}") { }
}

/// <summary>
/// Exception thrown when template variable validation fails.
/// </summary>
public class TemplateVariableValidationException : DomainException
{
    public string VariableName { get; }
    public string ValidationError { get; }

    public TemplateVariableValidationException(string variableName, string validationError)
        : base("TEMPLATE_VARIABLE_VALIDATION", 
               $"Template variable '{variableName}' validation failed: {validationError}")
    {
        VariableName = variableName;
        ValidationError = validationError;
    }
}
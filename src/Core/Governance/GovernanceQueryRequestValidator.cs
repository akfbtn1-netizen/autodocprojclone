using FluentValidation;

namespace Enterprise.Documentation.Core.Governance;

/// <summary>
/// FluentValidation validator for GovernanceQueryRequest.
/// Implements enterprise validation rules following V1 patterns with V2 enhancements.
/// </summary>
public class GovernanceQueryRequestValidator : AbstractValidator<GovernanceQueryRequest>
{
    // Enterprise constants adapted from V1 template
    private const int MAX_QUERY_LENGTH = 10000;
    private const int MIN_QUERY_LENGTH = 5;
    private const int MAX_AGENT_ID_LENGTH = 100;
    private const int MIN_AGENT_ID_LENGTH = 3;
    private const int MAX_AGENT_NAME_LENGTH = 200;
    private const int MAX_DATABASE_NAME_LENGTH = 128;
    private const int MAX_CONNECTION_STRING_LENGTH = 2000;

    public GovernanceQueryRequestValidator()
    {
        // Agent validation rules
        RuleFor(x => x.AgentId)
            .NotEmpty()
            .WithMessage("Agent ID is required for governance tracking")
            .Length(MIN_AGENT_ID_LENGTH, MAX_AGENT_ID_LENGTH)
            .WithMessage($"Agent ID must be between {MIN_AGENT_ID_LENGTH} and {MAX_AGENT_ID_LENGTH} characters")
            .Matches(@"^[a-zA-Z0-9._-]+$")
            .WithMessage("Agent ID can only contain alphanumeric characters, dots, underscores, and hyphens");

        RuleFor(x => x.AgentName)
            .NotEmpty()
            .WithMessage("Agent Name is required for audit logging")
            .MaximumLength(MAX_AGENT_NAME_LENGTH)
            .WithMessage($"Agent Name cannot exceed {MAX_AGENT_NAME_LENGTH} characters");

        // SQL Query validation rules
        RuleFor(x => x.SqlQuery)
            .NotEmpty()
            .WithMessage("SQL Query is required")
            .Length(MIN_QUERY_LENGTH, MAX_QUERY_LENGTH)
            .WithMessage($"SQL Query must be between {MIN_QUERY_LENGTH} and {MAX_QUERY_LENGTH} characters")
            .Must(NotContainNullBytes)
            .WithMessage("SQL Query cannot contain null bytes or control characters");

        // Database validation rules
        RuleFor(x => x.DatabaseName)
            .NotEmpty()
            .WithMessage("Database Name is required")
            .MaximumLength(MAX_DATABASE_NAME_LENGTH)
            .WithMessage($"Database Name cannot exceed {MAX_DATABASE_NAME_LENGTH} characters")
            .Matches(@"^[a-zA-Z0-9_]+$")
            .WithMessage("Database Name can only contain alphanumeric characters and underscores");

        // Agent purpose validation
        RuleFor(x => x.AgentPurpose)
            .NotEmpty()
            .WithMessage("Agent Purpose is required for compliance documentation")
            .MaximumLength(MAX_AGENT_NAME_LENGTH)
            .WithMessage($"Agent Purpose cannot exceed {MAX_AGENT_NAME_LENGTH} characters");

        // Clearance level validation
        RuleFor(x => x.ClearanceLevel)
            .IsInEnum()
            .WithMessage("Invalid clearance level specified");

        // Correlation ID validation
        RuleFor(x => x.CorrelationId)
            .NotEmpty()
            .WithMessage("Correlation ID is required for request tracking")
            .Must(BeValidGuid)
            .WithMessage("Correlation ID must be a valid GUID");

        // Execution time validation
        RuleFor(x => x.MaxExecutionTime)
            .GreaterThan(TimeSpan.Zero)
            .WithMessage("Max execution time must be greater than zero")
            .LessThanOrEqualTo(TimeSpan.FromMinutes(5))
            .WithMessage("Max execution time cannot exceed 5 minutes");

        // Additional security validations
        RuleFor(x => x)
            .Must(NotContainSuspiciousPatterns)
            .WithMessage("Query contains potentially dangerous patterns");
    }

    /// <summary>
    /// Validates that the string doesn't contain null bytes or dangerous control characters.
    /// </summary>
    private static bool NotContainNullBytes(string input)
    {
        if (string.IsNullOrEmpty(input))
            return true;

        // Check for null bytes and dangerous control characters
        return !input.Any(c => c == '\0' || c == '\x08' || c == '\x0B' || c == '\x0C' || c == '\x1A');
    }



    /// <summary>
    /// Validates that the correlation ID is a valid GUID.
    /// </summary>
    private static bool BeValidGuid(string correlationId)
    {
        return Guid.TryParse(correlationId, out _);
    }

    /// <summary>
    /// Enhanced security validation to detect suspicious query patterns.
    /// Implements patterns from V1 security engine with additional checks.
    /// </summary>
    private static bool NotContainSuspiciousPatterns(GovernanceQueryRequest request)
    {
        if (string.IsNullOrEmpty(request.SqlQuery))
            return true;

        var query = request.SqlQuery.ToUpperInvariant();

        // Check for basic SQL injection patterns
        var suspiciousPatterns = new[]
        {
            "EXEC(", "EXECUTE(", "SP_", "XP_", 
            "OPENROWSET", "OPENQUERY", "OPENDATASOURCE",
            "BULK INSERT", "BCP",
            "--@", "/*@", "WAITFOR DELAY",
            "CONVERT(", "CAST(", "CHAR(", "NCHAR(",
            "@@", "INFORMATION_SCHEMA",
            "SYS.", "SYSOBJECTS", "SYSCOLUMNS"
        };

        // Allow certain patterns for Administrator clearance
        if (request.ClearanceLevel == AgentClearanceLevel.Administrator)
        {
            // Administrators can use more advanced patterns, but still block the most dangerous
            var adminBlockedPatterns = new[] { "EXEC(", "EXECUTE(", "XP_", "WAITFOR DELAY" };
            return !adminBlockedPatterns.Any(pattern => query.Contains(pattern));
        }

        // Standard validation for non-administrator clearance levels
        return !suspiciousPatterns.Any(pattern => query.Contains(pattern));
    }
}
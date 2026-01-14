using System.Diagnostics.CodeAnalysis;

namespace Enterprise.Documentation.Core.Governance;

/// <summary>
/// Enterprise Data Governance Proxy - MANDATORY for all database access.
/// Implements comprehensive security, compliance, and audit capabilities.
/// Following ADR-004: All database access MUST go through this proxy.
/// </summary>
public interface IDataGovernanceProxy
{
    /// <summary>
    /// Executes a secure query through the governance layer with full audit trail.
    /// Implements SQL injection prevention, PII detection, RBAC authorization.
    /// </summary>
    /// <param name="request">Governance query request with security context</param>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <returns>Secure query result with applied data masking</returns>
    Task<GovernanceQueryResult> ExecuteSecureQueryAsync(GovernanceQueryRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates query security and compliance before execution.
    /// Checks for SQL injection, unauthorized table access, dangerous patterns.
    /// </summary>
    /// <param name="request">Query request to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with security assessment</returns>
    Task<GovernanceValidationResult> ValidateQueryAsync(GovernanceQueryRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Authorizes agent access to requested resources based on clearance level.
    /// Implements RBAC with agent-specific permissions and rate limiting.
    /// </summary>
    /// <param name="agentId">Agent identifier requesting access</param>
    /// <param name="requestedTables">Tables the agent wants to access</param>
    /// <param name="clearanceLevel">Agent's security clearance level</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Authorization result with granted permissions</returns>
    Task<GovernanceAuthorizationResult> AuthorizeAccessAsync(string agentId, IEnumerable<string> requestedTables, AgentClearanceLevel clearanceLevel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves immutable audit trail for compliance and investigation.
    /// Supports filtering by agent, time range, and event type.
    /// </summary>
    /// <param name="filter">Audit query filter criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated audit trail results</returns>
    Task<GovernanceAuditResult> GetAuditTrailAsync(GovernanceAuditFilter filter, CancellationToken cancellationToken = default);
}

/// <summary>
/// Governance query request with full security context.
/// Leverages V1 validation patterns while adding V2 async support.
/// </summary>
public record GovernanceQueryRequest
{
    /// <summary>Unique identifier for the requesting agent</summary>
    public required string AgentId { get; init; }

    /// <summary>Display name of the requesting agent for audit logs</summary>
    public required string AgentName { get; init; }

    /// <summary>Business purpose justifying the data access request</summary>
    public required string AgentPurpose { get; init; }

    /// <summary>Target database name for the query</summary>
    public required string DatabaseName { get; init; }

    /// <summary>SQL query to execute (must be parameterized)</summary>
    public required string SqlQuery { get; init; }

    /// <summary>Parameter values for the SQL query</summary>
    public Dictionary<string, object?> Parameters { get; init; } = new();

    /// <summary>Tables that the query will access (for authorization)</summary>
    public IReadOnlyList<string> RequestedTables { get; init; } = Array.Empty<string>();

    /// <summary>Specific columns being accessed (for PII detection)</summary>
    public IReadOnlyList<string> RequestedColumns { get; init; } = Array.Empty<string>();

    /// <summary>Agent's security clearance level</summary>
    public AgentClearanceLevel ClearanceLevel { get; init; }

    /// <summary>Correlation ID for request tracing across services</summary>
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>Timestamp when the request was created</summary>
    public DateTime RequestTimestamp { get; init; } = DateTime.UtcNow;

    /// <summary>Maximum execution time allowed for the query</summary>
    public TimeSpan MaxExecutionTime { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Whether to apply data masking based on clearance level</summary>
    public bool ApplyDataMasking { get; init; } = true;
}

/// <summary>
/// Agent clearance levels for data access authorization.
/// Adapted from V1 governance with enhanced security model.
/// </summary>
public enum AgentClearanceLevel
{
    /// <summary>Minimal data access with heavy masking (95% of PII masked)</summary>
    Restricted = 0,

    /// <summary>Standard operations with moderate masking (60% of PII masked)</summary>
    Standard = 1,

    /// <summary>Sensitive operations with light masking (30% of PII masked)</summary>
    Elevated = 2,

    /// <summary>Full access with audit-only masking (compliance logging only)</summary>
    Administrator = 3
}

/// <summary>
/// Result of governance query execution with security metadata.
/// </summary>
public record GovernanceQueryResult
{
    /// <summary>Column metadata for the result set</summary>
    public IReadOnlyList<GovernanceColumnInfo> Columns { get; init; } = Array.Empty<GovernanceColumnInfo>();

    /// <summary>Query result rows with applied data masking</summary>
    public IReadOnlyList<Dictionary<string, object?>> Rows { get; init; } = Array.Empty<Dictionary<string, object?>>();

    /// <summary>Total number of rows returned</summary>
    public int RowCount => Rows.Count;

    /// <summary>Summary of data masking applied to the results</summary>
    public GovernanceMaskingSummary MaskingSummary { get; init; } = new();

    /// <summary>PII findings detected during query execution</summary>
    public IReadOnlyList<GovernancePIIFinding> PIIFindings { get; init; } = Array.Empty<GovernancePIIFinding>();

    /// <summary>Execution metadata for performance monitoring</summary>
    public GovernanceExecutionMetadata ExecutionMetadata { get; init; } = new();

    /// <summary>Unique audit trail identifier for this query execution</summary>
    public string AuditTrailId { get; init; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Column information with security classification.
/// </summary>
public record GovernanceColumnInfo
{
    /// <summary>Column name</summary>
    public required string Name { get; init; }

    /// <summary>SQL data type</summary>
    public required string DataType { get; init; }

    /// <summary>Whether this column contains PII</summary>
    public bool ContainsPII { get; init; }

    /// <summary>Security classification level</summary>
    public DataClassification Classification { get; init; } = DataClassification.Public;

    /// <summary>Whether masking was applied to this column</summary>
    public bool WasMasked { get; init; }
}

/// <summary>
/// Data classification levels for security policy application.
/// </summary>
public enum DataClassification
{
    /// <summary>Public data - no restrictions</summary>
    Public = 0,

    /// <summary>Internal data - standard access controls</summary>
    Internal = 1,

    /// <summary>Confidential data - elevated access required</summary>
    Confidential = 2,

    /// <summary>Restricted data - administrator access only</summary>
    Restricted = 3
}

/// <summary>
/// Summary of data masking operations applied to query results.
/// </summary>
public record GovernanceMaskingSummary
{
    /// <summary>Total number of fields that were masked</summary>
    public int TotalFieldsMasked { get; init; }

    /// <summary>Breakdown of masking by PII type</summary>
    public IReadOnlyDictionary<PIIType, int> MaskingByType { get; init; } = new Dictionary<PIIType, int>();

    /// <summary>List of column names that were masked</summary>
    public IReadOnlyList<string> MaskedColumns { get; init; } = Array.Empty<string>();

    /// <summary>Masking strategy that was applied</summary>
    public MaskingStrategy StrategyApplied { get; init; }
}

/// <summary>
/// Types of Personally Identifiable Information for targeted masking.
/// </summary>
public enum PIIType
{
    /// <summary>Email addresses</summary>
    EmailAddress,

    /// <summary>Phone numbers</summary>
    PhoneNumber,

    /// <summary>Social Security Numbers</summary>
    SSN,

    /// <summary>Credit card numbers</summary>
    CreditCard,

    /// <summary>Physical addresses</summary>
    Address,

    /// <summary>Personal names</summary>
    PersonName,

    /// <summary>Date of birth</summary>
    DateOfBirth,

    /// <summary>Government ID numbers</summary>
    GovernmentID,

    /// <summary>Financial account numbers</summary>
    FinancialAccount,

    /// <summary>Other sensitive data</summary>
    Other
}

/// <summary>
/// Data masking strategies for different security contexts.
/// </summary>
public enum MaskingStrategy
{
    /// <summary>No masking applied</summary>
    None,

    /// <summary>Partial masking (show first/last characters)</summary>
    Partial,

    /// <summary>Full masking (replace with asterisks)</summary>
    Full,

    /// <summary>Hash-based masking (consistent obfuscation)</summary>
    Hash,

    /// <summary>Format-preserving masking</summary>
    FormatPreserving
}

/// <summary>
/// PII finding detected during query analysis.
/// </summary>
public record GovernancePIIFinding
{
    /// <summary>Column name containing PII</summary>
    public required string ColumnName { get; init; }

    /// <summary>Type of PII detected</summary>
    public required PIIType PIIType { get; init; }

    /// <summary>Confidence level of PII detection (0.0 to 1.0)</summary>
    public double Confidence { get; init; }

    /// <summary>Pattern or rule that triggered the detection</summary>
    public string DetectionRule { get; init; } = string.Empty;

    /// <summary>Number of PII instances found in the column</summary>
    public int InstanceCount { get; init; }
}

/// <summary>
/// Execution metadata for performance monitoring and compliance.
/// </summary>
public record GovernanceExecutionMetadata
{
    /// <summary>Query execution duration</summary>
    public TimeSpan ExecutionDuration { get; init; }

    /// <summary>Number of rows scanned during execution</summary>
    public long RowsScanned { get; init; }

    /// <summary>Database server that processed the query</summary>
    public string DatabaseServer { get; init; } = string.Empty;

    /// <summary>Timestamp when execution started</summary>
    public DateTime ExecutionStartTime { get; init; } = DateTime.UtcNow;

    /// <summary>Memory usage during query execution</summary>
    public long MemoryUsageBytes { get; init; }

    /// <summary>CPU time consumed by the query</summary>
    public TimeSpan CPUTime { get; init; }
}

/// <summary>
/// Query validation result with security assessment.
/// </summary>
public record GovernanceValidationResult
{
    /// <summary>Whether the query passed all security validations</summary>
    public bool IsValid { get; init; }

    /// <summary>Reason for validation failure if applicable</summary>
    public string? FailureReason { get; init; }

    /// <summary>Security risks detected in the query</summary>
    public IReadOnlyList<SecurityRisk> SecurityRisks { get; init; } = Array.Empty<SecurityRisk>();

    /// <summary>Validation warnings that don't block execution</summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>Recommended security actions</summary>
    public IReadOnlyList<string> Recommendations { get; init; } = Array.Empty<string>();

    /// <summary>Creates a successful validation result</summary>
    public static GovernanceValidationResult Success() => new() { IsValid = true };

    /// <summary>Creates a failed validation result with reason</summary>
    public static GovernanceValidationResult Failure(string reason, IEnumerable<SecurityRisk>? risks = null) =>
        new() { IsValid = false, FailureReason = reason, SecurityRisks = risks?.ToArray() ?? Array.Empty<SecurityRisk>() };
}

/// <summary>
/// Security risk detected during query validation.
/// </summary>
public record SecurityRisk
{
    /// <summary>Type of security risk</summary>
    public required SecurityRiskType Type { get; init; }

    /// <summary>Severity level of the risk</summary>
    public required RiskSeverity Severity { get; init; }

    /// <summary>Detailed description of the risk</summary>
    public required string Description { get; init; }

    /// <summary>Location in query where risk was detected</summary>
    public string? Location { get; init; }

    /// <summary>Recommended mitigation action</summary>
    public string? Mitigation { get; init; }
}

/// <summary>
/// Types of security risks that can be detected.
/// </summary>
public enum SecurityRiskType
{
    /// <summary>SQL injection attempt detected</summary>
    SQLInjection,

    /// <summary>Unauthorized table access</summary>
    UnauthorizedAccess,

    /// <summary>Dangerous SQL patterns</summary>
    DangerousPattern,

    /// <summary>Data exfiltration attempt</summary>
    DataExfiltration,

    /// <summary>Performance attack (resource exhaustion)</summary>
    PerformanceAttack,

    /// <summary>Privilege escalation attempt</summary>
    PrivilegeEscalation,

    /// <summary>Malformed or suspicious query structure</summary>
    SuspiciousStructure
}

/// <summary>
/// Risk severity levels for security assessment.
/// </summary>
public enum RiskSeverity
{
    /// <summary>Low risk - log and monitor</summary>
    Low = 1,

    /// <summary>Medium risk - warn and apply additional controls</summary>
    Medium = 2,

    /// <summary>High risk - block execution and alert security team</summary>
    High = 3,

    /// <summary>Critical risk - immediate block and incident response</summary>
    Critical = 4
}

/// <summary>
/// Authorization result for agent data access requests.
/// </summary>
public record GovernanceAuthorizationResult
{
    /// <summary>Whether access is authorized</summary>
    public bool IsAuthorized { get; init; }

    /// <summary>Reason for access denial if applicable</summary>
    public string? DenialReason { get; init; }

    /// <summary>Clearance level granted for this request</summary>
    public AgentClearanceLevel GrantedClearanceLevel { get; init; }

    /// <summary>Tables the agent is authorized to access</summary>
    public IReadOnlyList<string> AuthorizedTables { get; init; } = Array.Empty<string>();

    /// <summary>Rate limiting information</summary>
    public RateLimitInfo RateLimit { get; init; } = new();

    /// <summary>Authorization expiration time</summary>
    public DateTime ExpiresAt { get; init; } = DateTime.UtcNow.AddHours(1);

    /// <summary>Creates an authorized result</summary>
    public static GovernanceAuthorizationResult Allow(AgentClearanceLevel level, IEnumerable<string>? tables = null) =>
        new() { IsAuthorized = true, GrantedClearanceLevel = level, AuthorizedTables = tables?.ToArray() ?? Array.Empty<string>() };

    /// <summary>Creates a denied authorization result</summary>
    public static GovernanceAuthorizationResult Deny(string reason) =>
        new() { IsAuthorized = false, DenialReason = reason };
}

/// <summary>
/// Rate limiting information for API throttling.
/// </summary>
public record RateLimitInfo
{
    /// <summary>Number of requests remaining in current window</summary>
    public int RequestsRemaining { get; init; }

    /// <summary>Time window reset timestamp</summary>
    public DateTime WindowResetTime { get; init; }

    /// <summary>Maximum requests allowed per window</summary>
    public int MaxRequestsPerWindow { get; init; }
}

/// <summary>
/// Audit trail filter for compliance queries.
/// </summary>
public record GovernanceAuditFilter
{
    /// <summary>Filter by specific agent ID</summary>
    public string? AgentId { get; init; }

    /// <summary>Start date for audit trail query</summary>
    public DateTime? StartDate { get; init; }

    /// <summary>End date for audit trail query</summary>
    public DateTime? EndDate { get; init; }

    /// <summary>Filter by event type</summary>
    public AuditEventType? EventType { get; init; }

    /// <summary>Filter by database name</summary>
    public string? DatabaseName { get; init; }

    /// <summary>Page size for result pagination</summary>
    public int PageSize { get; init; } = 50;

    /// <summary>Page number for result pagination</summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>Include PII findings in results</summary>
    public bool IncludePIIFindings { get; init; } = true;
}

/// <summary>
/// Audit trail query results with pagination.
/// </summary>
public record GovernanceAuditResult
{
    /// <summary>Audit entries for the requested filter</summary>
    public IReadOnlyList<GovernanceAuditEntry> Entries { get; init; } = Array.Empty<GovernanceAuditEntry>();

    /// <summary>Total number of matching audit entries</summary>
    public long TotalCount { get; init; }

    /// <summary>Current page number</summary>
    public int PageNumber { get; init; }

    /// <summary>Number of entries per page</summary>
    public int PageSize { get; init; }

    /// <summary>Whether there are more pages available</summary>
    public bool HasMorePages => (PageNumber * PageSize) < TotalCount;
}

/// <summary>
/// Individual audit trail entry for compliance tracking.
/// </summary>
public record GovernanceAuditEntry
{
    /// <summary>Unique audit entry identifier</summary>
    public required string AuditId { get; init; }

    /// <summary>Correlation ID linking related operations</summary>
    public required string CorrelationId { get; init; }

    /// <summary>Agent that performed the operation</summary>
    public required string AgentId { get; init; }

    /// <summary>Display name of the agent</summary>
    public required string AgentName { get; init; }

    /// <summary>Database accessed</summary>
    public required string DatabaseName { get; init; }

    /// <summary>Tables accessed during the operation</summary>
    public IReadOnlyList<string> TablesAccessed { get; init; } = Array.Empty<string>();

    /// <summary>SQL query that was executed (parameterized)</summary>
    public string QueryText { get; init; } = string.Empty;

    /// <summary>Number of rows returned by the query</summary>
    public int RowsReturned { get; init; }

    /// <summary>PII findings from the operation</summary>
    public IReadOnlyList<GovernancePIIFinding> PIIFindings { get; init; } = Array.Empty<GovernancePIIFinding>();

    /// <summary>Data masking summary for the operation</summary>
    public GovernanceMaskingSummary? MaskingApplied { get; init; }

    /// <summary>Operation execution duration</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Whether the operation completed successfully</summary>
    public bool Success { get; init; }

    /// <summary>Failure reason if operation was unsuccessful</summary>
    public string? FailureReason { get; init; }

    /// <summary>Timestamp when the operation occurred</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>Type of audit event</summary>
    public AuditEventType EventType { get; init; }

    /// <summary>Security clearance level used for the operation</summary>
    public AgentClearanceLevel ClearanceLevel { get; init; }
}

/// <summary>
/// Types of events tracked in the audit trail.
/// </summary>
public enum AuditEventType
{
    /// <summary>Query was executed successfully</summary>
    QueryExecuted,

    /// <summary>Query was blocked by security controls</summary>
    QueryBlocked,

    /// <summary>Unauthorized access attempt</summary>
    UnauthorizedAccess,

    /// <summary>Query execution failed</summary>
    QueryFailed,

    /// <summary>PII exposure detected</summary>
    PIIExposure,

    /// <summary>Rate limit exceeded</summary>
    RateLimitExceeded,

    /// <summary>Security risk detected</summary>
    SecurityRiskDetected,

    /// <summary>Agent authorization granted</summary>
    AuthorizationGranted,

    /// <summary>Agent authorization denied</summary>
    AuthorizationDenied
}
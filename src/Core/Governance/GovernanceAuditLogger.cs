using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace Enterprise.Documentation.Core.Governance;

/// <summary>
/// Enterprise V2 Audit Logger for immutable compliance audit trails.
/// Implements comprehensive audit logging with structured data and correlation tracking.
/// Enhanced from V1 patterns with async support and enterprise observability.
/// </summary>
public class GovernanceAuditLogger
{
    private readonly ILogger<GovernanceAuditLogger> _logger;
    private readonly ActivitySource _activitySource;
    
    // In a real implementation, this would connect to Azure Blob Storage or similar
    // For now, we'll use structured logging with the option to extend
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GovernanceAuditLogger(ILogger<GovernanceAuditLogger> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activitySource = new ActivitySource($"{nameof(GovernanceAuditLogger)}-v2");
    }

    /// <summary>
    /// Logs an audit event to the immutable audit trail.
    /// Creates structured audit entries for compliance and investigation.
    /// </summary>
    public async Task LogEventAsync(GovernanceAuditEntry entry, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("LogAuditEvent");
        activity?.SetTag("audit.id", entry.AuditId);
        activity?.SetTag("audit.agent_id", entry.AgentId);
        activity?.SetTag("audit.event_type", entry.EventType.ToString());
        activity?.SetTag("audit.success", entry.Success);
        activity?.SetTag("correlation.id", entry.CorrelationId);

        try
        {
            // Validate audit entry
            ValidateAuditEntry(entry);

            // Create structured audit log entry
            var auditData = new
            {
                // Core identification
                audit_id = entry.AuditId,
                correlation_id = entry.CorrelationId,
                timestamp = entry.Timestamp,
                
                // Agent information
                agent_id = entry.AgentId,
                agent_name = entry.AgentName,
                clearance_level = entry.ClearanceLevel.ToString(),
                
                // Operation details
                event_type = entry.EventType.ToString(),
                database_name = entry.DatabaseName,
                tables_accessed = entry.TablesAccessed.ToArray(),
                query_text = SanitizeQueryForLogging(entry.QueryText),
                
                // Results
                success = entry.Success,
                failure_reason = entry.FailureReason,
                rows_returned = entry.RowsReturned,
                duration_ms = entry.Duration.TotalMilliseconds,
                
                // Security metadata
                pii_findings_count = entry.PIIFindings.Count,
                pii_types_detected = entry.PIIFindings.Select(p => p.PIIType.ToString()).Distinct().ToArray(),
                masking_applied = entry.MaskingApplied != null,
                fields_masked = entry.MaskingApplied?.TotalFieldsMasked ?? 0,
                
                // Compliance markers
                gdpr_relevant = ContainsGDPRData(entry),
                hipaa_relevant = ContainsHIPAAData(entry),
                
                // Environment metadata
                server_name = Environment.MachineName,
                process_id = Environment.ProcessId,
                
                // Version tracking
                governance_version = "2.0"
            };

            // Log to structured logger (this will go to configured sinks)
            _logger.LogInformation("GOVERNANCE_AUDIT: {AuditData}", JsonSerializer.Serialize(auditData, _jsonOptions));

            // For high-severity events, also log as warnings/errors
            if (!entry.Success || entry.EventType == AuditEventType.SecurityRiskDetected)
            {
                var severity = DetermineLogSeverity(entry);
                LogWithSeverity(severity, "Governance security event: {EventType} for agent {AgentId}: {Reason}", 
                    entry.EventType, entry.AgentId, entry.FailureReason ?? "Success");
            }

            // In a production environment, this would also:
            // 1. Write to Azure Blob Storage for long-term retention
            // 2. Send to Azure Event Hubs for real-time monitoring
            // 3. Store in Azure Table Storage for fast queries
            // 4. Trigger Azure Logic Apps for compliance workflows

            await Task.CompletedTask; // Placeholder for actual async storage operations

            activity?.SetTag("logging.success", true);
            
            _logger.LogDebug("Audit entry logged successfully: {AuditId}", entry.AuditId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log audit entry {AuditId} for agent {AgentId}", 
                entry.AuditId, entry.AgentId);
            activity?.SetTag("logging.error", ex.Message);
            
            // Audit logging failures are critical - they could indicate tampering attempts
            // In production, this would trigger immediate security alerts
            throw new GovernanceAuditException($"Critical audit logging failure: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Retrieves audit trail entries based on filter criteria.
    /// Supports pagination and complex filtering for compliance queries.
    /// </summary>
    public async Task<GovernanceAuditResult> GetAuditTrailAsync(GovernanceAuditFilter filter, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("GetAuditTrail");
        activity?.SetTag("filter.agent_id", filter.AgentId);
        activity?.SetTag("filter.page_size", filter.PageSize);
        activity?.SetTag("filter.page_number", filter.PageNumber);

        try
        {
            // In a real implementation, this would query Azure Table Storage, SQL Database, or similar
            // For now, we'll return a structured response indicating the query parameters
            
            _logger.LogInformation("Audit trail query requested: Agent={AgentId}, StartDate={StartDate}, EndDate={EndDate}, EventType={EventType}", 
                filter.AgentId, filter.StartDate, filter.EndDate, filter.EventType);

            // Simulate audit retrieval (in production, this would be actual database queries)
            var entries = await SimulateAuditRetrieval(filter, cancellationToken);
            
            var totalCount = await GetTotalAuditCount(filter, cancellationToken);

            activity?.SetTag("results.count", entries.Count);
            activity?.SetTag("results.total", totalCount);

            return new GovernanceAuditResult
            {
                Entries = entries,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audit trail retrieval failed for filter: {Filter}", 
                JsonSerializer.Serialize(filter, _jsonOptions));
            activity?.SetTag("retrieval.error", ex.Message);
            throw new GovernanceAuditException($"Audit trail retrieval failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates audit entry completeness and consistency.
    /// </summary>
    private void ValidateAuditEntry(GovernanceAuditEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.AuditId))
            throw new ArgumentException("Audit ID is required");
        
        if (string.IsNullOrWhiteSpace(entry.CorrelationId))
            throw new ArgumentException("Correlation ID is required");
        
        if (string.IsNullOrWhiteSpace(entry.AgentId))
            throw new ArgumentException("Agent ID is required");
        
        if (string.IsNullOrWhiteSpace(entry.AgentName))
            throw new ArgumentException("Agent Name is required");
        
        if (entry.Timestamp == default)
            throw new ArgumentException("Timestamp is required");

        // Validate timestamp is not too far in the future (clock skew protection)
        if (entry.Timestamp > DateTime.UtcNow.AddMinutes(5))
            throw new ArgumentException("Timestamp cannot be in the future");
    }

    /// <summary>
    /// Sanitizes SQL query text for safe logging (removes potential sensitive data).
    /// </summary>
    private string SanitizeQueryForLogging(string queryText)
    {
        if (string.IsNullOrWhiteSpace(queryText))
            return string.Empty;

        // In production, this would apply more sophisticated sanitization
        // For now, we'll truncate very long queries and mask potential sensitive literals
        
        var sanitized = queryText.Length > 500 ? $"{queryText.Substring(0, 500)}..." : queryText;
        
        // Replace string literals with placeholders to avoid logging sensitive data
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"'[^']*'", "'***'");
        
        return sanitized;
    }

    /// <summary>
    /// Determines if the audit entry involves GDPR-relevant data.
    /// </summary>
    private bool ContainsGDPRData(GovernanceAuditEntry entry)
    {
        // Check for European PII types or explicit GDPR-sensitive operations
        return entry.PIIFindings.Any(p => p.PIIType == PIIType.EmailAddress || 
                                         p.PIIType == PIIType.PersonName ||
                                         p.PIIType == PIIType.Address) ||
               entry.TablesAccessed.Any(t => t.ToLowerInvariant().Contains("customer") ||
                                            t.ToLowerInvariant().Contains("user") ||
                                            t.ToLowerInvariant().Contains("person"));
    }

    /// <summary>
    /// Determines if the audit entry involves HIPAA-relevant data.
    /// </summary>
    private bool ContainsHIPAAData(GovernanceAuditEntry entry)
    {
        // Check for healthcare-related PII or table access
        return entry.PIIFindings.Any(p => p.PIIType == PIIType.SSN ||
                                         p.PIIType == PIIType.DateOfBirth) ||
               entry.TablesAccessed.Any(t => t.ToLowerInvariant().Contains("patient") ||
                                            t.ToLowerInvariant().Contains("medical") ||
                                            t.ToLowerInvariant().Contains("health"));
    }

    /// <summary>
    /// Determines appropriate log severity based on audit entry characteristics.
    /// </summary>
    private LogLevel DetermineLogSeverity(GovernanceAuditEntry entry)
    {
        return entry.EventType switch
        {
            AuditEventType.SecurityRiskDetected => LogLevel.Warning,
            AuditEventType.UnauthorizedAccess => LogLevel.Warning,
            AuditEventType.QueryBlocked => LogLevel.Warning,
            AuditEventType.PIIExposure => LogLevel.Warning,
            AuditEventType.QueryFailed => LogLevel.Error,
            AuditEventType.AuthorizationDenied => LogLevel.Information,
            _ => LogLevel.Information
        };
    }

    /// <summary>
    /// Logs with appropriate severity level.
    /// </summary>
    private void LogWithSeverity(LogLevel level, string message, params object[] args)
    {
        switch (level)
        {
            case LogLevel.Error:
                _logger.LogError(message, args);
                break;
            case LogLevel.Warning:
                _logger.LogWarning(message, args);
                break;
            default:
                _logger.LogInformation(message, args);
                break;
        }
    }

    /// <summary>
    /// Simulates audit entry retrieval for demonstration.
    /// In production, this would query actual storage systems.
    /// </summary>
    private async Task<IReadOnlyList<GovernanceAuditEntry>> SimulateAuditRetrieval(GovernanceAuditFilter filter, CancellationToken cancellationToken)
    {
        await Task.Delay(10, cancellationToken); // Simulate async database query
        
        // Return empty list for now - in production this would return actual audit entries
        // matching the filter criteria from persistent storage
        return Array.Empty<GovernanceAuditEntry>();
    }

    /// <summary>
    /// Gets total count of audit entries matching filter.
    /// </summary>
    private async Task<long> GetTotalAuditCount(GovernanceAuditFilter filter, CancellationToken cancellationToken)
    {
        await Task.Delay(5, cancellationToken); // Simulate async count query
        return 0; // In production, this would return actual count from storage
    }

    public void Dispose()
    {
        _activitySource?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Exception for audit logging failures.
/// </summary>
public class GovernanceAuditException : GovernanceException
{
    public GovernanceAuditException(string message) : base(message) { }
    public GovernanceAuditException(string message, Exception innerException) : base(message, innerException) { }
}
using Enterprise.Documentation.Core.Governance;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Data;
using Microsoft.Data.SqlClient;
using Polly;
using Polly.CircuitBreaker;
using FluentValidation;

namespace Enterprise.Documentation.Core.Governance;

/// <summary>
/// Enterprise V2 Data Governance Proxy - MANDATORY security layer for all database access.
/// Implements comprehensive security, compliance, and audit capabilities following V2 standards.
/// Leverages V1 security patterns while adding async support, circuit breakers, and observability.
/// </summary>
public class DataGovernanceProxy : IDataGovernanceProxy
{
    private readonly ILogger<DataGovernanceProxy> _logger;
    private readonly IConfiguration _configuration;
    private readonly IValidator<GovernanceQueryRequest> _validator;
    private readonly ActivitySource _activitySource;
    private readonly ResiliencePipeline _circuitBreaker;
    
    // V1-inspired security patterns with V2 enhancements
    private readonly GovernanceSecurityEngine _securityEngine;
    private readonly GovernancePIIDetector _piiDetector;
    private readonly GovernanceAuditLogger _auditLogger;
    private readonly GovernanceAuthorizationEngine _authorizationEngine;
    
    // Enterprise constants adapted from V1 template
    private const int MAX_QUERY_LENGTH = 10000;
    private const int MAX_EXECUTION_TIME_SECONDS = 30;
    private const int MAX_RESULT_ROWS = 10000;
    private const int MAX_JOIN_COUNT = 5;
    private const int MAX_SUBQUERY_DEPTH = 3;

    public DataGovernanceProxy(
        ILogger<DataGovernanceProxy> logger,
        IConfiguration configuration,
        IValidator<GovernanceQueryRequest> validator,
        GovernanceSecurityEngine securityEngine,
        GovernancePIIDetector piiDetector,
        GovernanceAuditLogger auditLogger,
        GovernanceAuthorizationEngine authorizationEngine)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _securityEngine = securityEngine ?? throw new ArgumentNullException(nameof(securityEngine));
        _piiDetector = piiDetector ?? throw new ArgumentNullException(nameof(piiDetector));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        _authorizationEngine = authorizationEngine ?? throw new ArgumentNullException(nameof(authorizationEngine));
        
        _activitySource = new ActivitySource($"{nameof(DataGovernanceProxy)}-v2");
        
        // Initialize circuit breaker with enterprise configuration
        _circuitBreaker = new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.3, // More sensitive for security operations
                SamplingDuration = TimeSpan.FromSeconds(10),
                MinimumThroughput = 3,
                BreakDuration = TimeSpan.FromSeconds(60), // Longer break for security
                OnOpened = args =>
                {
                    _logger.LogWarning("Data Governance circuit breaker opened due to {FailureRatio:P} failure rate", 
                        args.BreakDuration);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation("Data Governance circuit breaker closed");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <inheritdoc />
    public async Task<GovernanceQueryResult> ExecuteSecureQueryAsync(GovernanceQueryRequest request, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("ExecuteSecureQuery");
        activity?.SetTag("agent.id", request.AgentId);
        activity?.SetTag("agent.name", request.AgentName);
        activity?.SetTag("database.name", request.DatabaseName);
        activity?.SetTag("clearance.level", request.ClearanceLevel.ToString());
        activity?.SetTag("correlation.id", request.CorrelationId);

        var stopwatch = Stopwatch.StartNew();
        var auditTrailId = Guid.NewGuid().ToString();
        
        try
        {
            _logger.LogInformation("Starting secure query execution for agent {AgentId} with correlation {CorrelationId}", 
                request.AgentId, request.CorrelationId);

            // Step 1: Input validation using FluentValidation (V1 pattern enhanced)
            var validationResult = await _validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                _logger.LogWarning("Governance request validation failed for agent {AgentId}: {ValidationErrors}", 
                    request.AgentId, errors);
                
                await _auditLogger.LogEventAsync(new GovernanceAuditEntry
                {
                    AuditId = auditTrailId,
                    CorrelationId = request.CorrelationId,
                    AgentId = request.AgentId,
                    AgentName = request.AgentName,
                    DatabaseName = request.DatabaseName,
                    EventType = AuditEventType.QueryBlocked,
                    Success = false,
                    FailureReason = $"Validation failed: {errors}",
                    Duration = stopwatch.Elapsed,
                    ClearanceLevel = request.ClearanceLevel
                }, cancellationToken);
                
                throw new GovernanceSecurityException($"Request validation failed: {errors}");
            }

            // Step 2: Authorization check (RBAC with agent clearance levels)
            var authResult = await AuthorizeAccessAsync(request.AgentId, request.RequestedTables, request.ClearanceLevel, cancellationToken);
            if (!authResult.IsAuthorized)
            {
                _logger.LogWarning("Authorization denied for agent {AgentId}: {DenialReason}", 
                    request.AgentId, authResult.DenialReason);
                
                await _auditLogger.LogEventAsync(new GovernanceAuditEntry
                {
                    AuditId = auditTrailId,
                    CorrelationId = request.CorrelationId,
                    AgentId = request.AgentId,
                    AgentName = request.AgentName,
                    DatabaseName = request.DatabaseName,
                    EventType = AuditEventType.AuthorizationDenied,
                    Success = false,
                    FailureReason = authResult.DenialReason,
                    Duration = stopwatch.Elapsed,
                    ClearanceLevel = request.ClearanceLevel
                }, cancellationToken);
                
                throw new GovernanceAuthorizationException(authResult.DenialReason ?? "Access denied");
            }

            // Step 3: Query security validation (SQL injection prevention, dangerous patterns)
            var securityValidation = await ValidateQueryAsync(request, cancellationToken);
            if (!securityValidation.IsValid)
            {
                _logger.LogWarning("Security validation failed for agent {AgentId}: {FailureReason}", 
                    request.AgentId, securityValidation.FailureReason);
                
                await _auditLogger.LogEventAsync(new GovernanceAuditEntry
                {
                    AuditId = auditTrailId,
                    CorrelationId = request.CorrelationId,
                    AgentId = request.AgentId,
                    AgentName = request.AgentName,
                    DatabaseName = request.DatabaseName,
                    EventType = AuditEventType.SecurityRiskDetected,
                    Success = false,
                    FailureReason = securityValidation.FailureReason,
                    Duration = stopwatch.Elapsed,
                    ClearanceLevel = request.ClearanceLevel
                }, cancellationToken);
                
                throw new GovernanceSecurityException(securityValidation.FailureReason ?? "Security validation failed");
            }

            // Step 4: Execute query through circuit breaker for resilience
            var queryResult = await _circuitBreaker.ExecuteAsync(async (ct) =>
            {
                return await ExecuteQueryWithSecurityAsync(request, auditTrailId, ct);
            }, cancellationToken);

            stopwatch.Stop();
            
            _logger.LogInformation("Secure query execution completed successfully for agent {AgentId} in {ElapsedMs}ms, returned {RowCount} rows", 
                request.AgentId, stopwatch.ElapsedMilliseconds, queryResult.RowCount);

            // Log successful execution to audit trail
            await _auditLogger.LogEventAsync(new GovernanceAuditEntry
            {
                AuditId = auditTrailId,
                CorrelationId = request.CorrelationId,
                AgentId = request.AgentId,
                AgentName = request.AgentName,
                DatabaseName = request.DatabaseName,
                TablesAccessed = request.RequestedTables.ToArray(),
                QueryText = request.SqlQuery,
                RowsReturned = queryResult.RowCount,
                PIIFindings = queryResult.PIIFindings.ToArray(),
                MaskingApplied = queryResult.MaskingSummary,
                Duration = stopwatch.Elapsed,
                Success = true,
                EventType = AuditEventType.QueryExecuted,
                ClearanceLevel = request.ClearanceLevel
            }, cancellationToken);

            activity?.SetTag("execution.success", true);
            activity?.SetTag("execution.rows", queryResult.RowCount);
            activity?.SetTag("execution.duration_ms", stopwatch.ElapsedMilliseconds);

            return queryResult with { AuditTrailId = auditTrailId };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(ex, "Secure query execution failed for agent {AgentId} after {ElapsedMs}ms", 
                request.AgentId, stopwatch.ElapsedMilliseconds);

            // Log failure to audit trail
            await _auditLogger.LogEventAsync(new GovernanceAuditEntry
            {
                AuditId = auditTrailId,
                CorrelationId = request.CorrelationId,
                AgentId = request.AgentId,
                AgentName = request.AgentName,
                DatabaseName = request.DatabaseName,
                EventType = AuditEventType.QueryFailed,
                Success = false,
                FailureReason = ex.Message,
                Duration = stopwatch.Elapsed,
                ClearanceLevel = request.ClearanceLevel
            }, cancellationToken);

            activity?.SetTag("execution.success", false);
            activity?.SetTag("execution.error", ex.Message);
            
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<GovernanceValidationResult> ValidateQueryAsync(GovernanceQueryRequest request, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("ValidateQuery");
        activity?.SetTag("agent.id", request.AgentId);
        activity?.SetTag("query.length", request.SqlQuery.Length);

        try
        {
            return await _securityEngine.ValidateQuerySecurityAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Query validation failed for agent {AgentId}", request.AgentId);
            return GovernanceValidationResult.Failure($"Validation error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<GovernanceAuthorizationResult> AuthorizeAccessAsync(string agentId, IEnumerable<string> requestedTables, AgentClearanceLevel clearanceLevel, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("AuthorizeAccess");
        activity?.SetTag("agent.id", agentId);
        activity?.SetTag("clearance.level", clearanceLevel.ToString());
        activity?.SetTag("tables.count", requestedTables.Count());

        try
        {
            return await _authorizationEngine.AuthorizeAgentAccessAsync(agentId, requestedTables, clearanceLevel, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authorization failed for agent {AgentId}", agentId);
            return GovernanceAuthorizationResult.Deny($"Authorization error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<GovernanceAuditResult> GetAuditTrailAsync(GovernanceAuditFilter filter, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("GetAuditTrail");
        activity?.SetTag("filter.agent_id", filter.AgentId);
        activity?.SetTag("filter.page_size", filter.PageSize);
        activity?.SetTag("filter.page_number", filter.PageNumber);

        try
        {
            return await _auditLogger.GetAuditTrailAsync(filter, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audit trail retrieval failed");
            throw new GovernanceException($"Failed to retrieve audit trail: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes the validated query with comprehensive security monitoring.
    /// Implements PII detection, data masking, and performance monitoring.
    /// </summary>
    private async Task<GovernanceQueryResult> ExecuteQueryWithSecurityAsync(GovernanceQueryRequest request, string auditTrailId, CancellationToken cancellationToken)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection") 
            ?? throw new GovernanceException("Database connection string not configured");

        var executionMetadata = new GovernanceExecutionMetadata
        {
            ExecutionStartTime = DateTime.UtcNow,
            DatabaseServer = ExtractServerFromConnectionString(connectionString)
        };

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            using var command = new SqlCommand(request.SqlQuery, connection)
            {
                CommandTimeout = (int)request.MaxExecutionTime.TotalSeconds
            };

            // Add parameters to prevent SQL injection
            foreach (var parameter in request.Parameters)
            {
                command.Parameters.AddWithValue($"@{parameter.Key}", parameter.Value ?? DBNull.Value);
            }

            var columns = new List<GovernanceColumnInfo>();
            var rows = new List<Dictionary<string, object?>>();
            var piiFindings = new List<GovernancePIIFinding>();

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            
            // Process column metadata
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var columnName = reader.GetName(i);
                var dataType = reader.GetDataTypeName(i);
                
                var columnInfo = new GovernanceColumnInfo
                {
                    Name = columnName,
                    DataType = dataType,
                    ContainsPII = _piiDetector.IsColumnPII(columnName, dataType),
                    Classification = _piiDetector.ClassifyColumn(columnName, dataType)
                };
                
                columns.Add(columnInfo);
            }

            // Process data rows with PII detection and masking
            int rowCount = 0;
            while (await reader.ReadAsync(cancellationToken) && rowCount < MAX_RESULT_ROWS)
            {
                var row = new Dictionary<string, object?>();
                
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var columnName = reader.GetName(i);
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    
                    // PII detection on actual data
                    if (value != null)
                    {
                        var piiResult = await _piiDetector.DetectPIIAsync(columnName, value.ToString()!, cancellationToken);
                        if (piiResult.IsPII)
                        {
                            piiFindings.Add(new GovernancePIIFinding
                            {
                                ColumnName = columnName,
                                PIIType = piiResult.PIIType,
                                Confidence = piiResult.Confidence,
                                DetectionRule = piiResult.DetectionRule,
                                InstanceCount = 1
                            });

                            // Apply data masking based on clearance level
                            if (request.ApplyDataMasking)
                            {
                                value = ApplyDataMasking(value.ToString()!, piiResult.PIIType, request.ClearanceLevel);
                            }
                        }
                    }
                    
                    row[columnName] = value;
                }
                
                rows.Add(row);
                rowCount++;
            }

            stopwatch.Stop();

            // Update execution metadata
            executionMetadata = executionMetadata with
            {
                ExecutionDuration = stopwatch.Elapsed,
                RowsScanned = rowCount,
                MemoryUsageBytes = GC.GetTotalMemory(false),
                CPUTime = stopwatch.Elapsed // Approximate CPU time
            };

            // Create masking summary
            var maskingSummary = CreateMaskingSummary(piiFindings, request.ClearanceLevel);

            return new GovernanceQueryResult
            {
                Columns = columns,
                Rows = rows,
                PIIFindings = piiFindings,
                MaskingSummary = maskingSummary,
                ExecutionMetadata = executionMetadata
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Query execution failed for agent {AgentId}: {ErrorMessage}", 
                request.AgentId, ex.Message);
            throw new GovernanceExecutionException($"Query execution failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Applies data masking based on PII type and clearance level.
    /// Uses V1-inspired masking strategies with V2 enhancements.
    /// </summary>
    private string ApplyDataMasking(string value, PIIType piiType, AgentClearanceLevel clearanceLevel)
    {
        // No masking for administrator level
        if (clearanceLevel == AgentClearanceLevel.Administrator)
            return value;

        // Determine masking percentage based on clearance level
        var maskingPercentage = clearanceLevel switch
        {
            AgentClearanceLevel.Restricted => 0.95, // 95% masking
            AgentClearanceLevel.Standard => 0.60,   // 60% masking
            AgentClearanceLevel.Elevated => 0.30,   // 30% masking
            _ => 0.95 // Default to most restrictive
        };

        return piiType switch
        {
            PIIType.EmailAddress => MaskEmail(value, maskingPercentage),
            PIIType.PhoneNumber => MaskPhone(value, maskingPercentage),
            PIIType.SSN => MaskSSN(value, maskingPercentage),
            PIIType.CreditCard => MaskCreditCard(value, maskingPercentage),
            PIIType.PersonName => MaskName(value, maskingPercentage),
            _ => MaskGeneric(value, maskingPercentage)
        };
    }

    /// <summary>
    /// Creates masking summary for audit and compliance reporting.
    /// </summary>
    private GovernanceMaskingSummary CreateMaskingSummary(List<GovernancePIIFinding> piiFindings, AgentClearanceLevel clearanceLevel)
    {
        var maskingByType = piiFindings
            .GroupBy(f => f.PIIType)
            .ToDictionary(g => g.Key, g => g.Sum(f => f.InstanceCount));

        var maskedColumns = piiFindings
            .Select(f => f.ColumnName)
            .Distinct()
            .ToArray();

        var strategy = clearanceLevel switch
        {
            AgentClearanceLevel.Administrator => MaskingStrategy.None,
            AgentClearanceLevel.Elevated => MaskingStrategy.Partial,
            AgentClearanceLevel.Standard => MaskingStrategy.Full,
            AgentClearanceLevel.Restricted => MaskingStrategy.Hash,
            _ => MaskingStrategy.Hash
        };

        return new GovernanceMaskingSummary
        {
            TotalFieldsMasked = piiFindings.Sum(f => f.InstanceCount),
            MaskingByType = maskingByType,
            MaskedColumns = maskedColumns,
            StrategyApplied = strategy
        };
    }

    // Helper methods for specific PII masking (V1-inspired implementations)
    private string MaskEmail(string email, double maskingPercentage) =>
        maskingPercentage > 0.8 ? "***@***.***" :
        maskingPercentage > 0.5 ? $"{email[0]}***@{email.Split('@')[1]}" :
        $"{email.Substring(0, 2)}***@{email.Split('@')[1]}";

    private string MaskPhone(string phone, double maskingPercentage) =>
        maskingPercentage > 0.8 ? "***-***-****" :
        maskingPercentage > 0.5 ? $"***-***-{phone.Substring(phone.Length - 4)}" :
        $"{phone.Substring(0, 3)}-***-{phone.Substring(phone.Length - 4)}";

    private string MaskSSN(string ssn, double maskingPercentage) =>
        maskingPercentage > 0.5 ? "***-**-****" :
        $"***-**-{ssn.Substring(ssn.Length - 4)}";

    private string MaskCreditCard(string card, double maskingPercentage) =>
        maskingPercentage > 0.5 ? "****-****-****-****" :
        $"****-****-****-{card.Substring(card.Length - 4)}";

    private string MaskName(string name, double maskingPercentage) =>
        maskingPercentage > 0.8 ? "***" :
        maskingPercentage > 0.5 ? $"{name[0]}***" :
        $"{name.Substring(0, Math.Min(2, name.Length))}***";

    private string MaskGeneric(string value, double maskingPercentage)
    {
        if (string.IsNullOrEmpty(value)) return value;
        
        var visibleChars = (int)((1 - maskingPercentage) * value.Length);
        var maskChars = value.Length - visibleChars;
        
        return visibleChars <= 0 ? new string('*', value.Length) :
               $"{value.Substring(0, visibleChars)}{new string('*', maskChars)}";
    }

    private string ExtractServerFromConnectionString(string connectionString)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            return builder.DataSource ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    /// <summary>
    /// Disposes resources used by the governance proxy.
    /// </summary>
    public void Dispose()
    {
        _activitySource?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Base exception for governance-related errors.
/// </summary>
public class GovernanceException : Exception
{
    public GovernanceException(string message) : base(message) { }
    public GovernanceException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception for security-related governance failures.
/// </summary>
public class GovernanceSecurityException : GovernanceException
{
    public GovernanceSecurityException(string message) : base(message) { }
    public GovernanceSecurityException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception for authorization-related governance failures.
/// </summary>
public class GovernanceAuthorizationException : GovernanceException
{
    public GovernanceAuthorizationException(string message) : base(message) { }
    public GovernanceAuthorizationException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception for query execution failures in governance layer.
/// </summary>
public class GovernanceExecutionException : GovernanceException
{
    public GovernanceExecutionException(string message) : base(message) { }
    public GovernanceExecutionException(string message, Exception innerException) : base(message, innerException) { }
}
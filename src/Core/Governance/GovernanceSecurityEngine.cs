using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace Enterprise.Documentation.Core.Governance;

/// <summary>
/// Enterprise V2 Security Engine for query validation and threat detection.
/// Implements comprehensive SQL injection prevention and dangerous pattern detection.
/// Enhanced from V1 patterns with async support and modern threat intelligence.
/// </summary>
public class GovernanceSecurityEngine
{
    private readonly ILogger<GovernanceSecurityEngine> _logger;
    private readonly ActivitySource _activitySource;

    // Enhanced dangerous patterns from V1 with additional modern threats
    private readonly List<SecurityPattern> _dangerousPatterns = new()
    {
        // SQL injection attempts
        new SecurityPattern(
            @"('\s*(OR|AND)\s*'?\d*'?\s*=\s*'?\d*)", 
            SecurityRiskType.SQLInjection, 
            RiskSeverity.Critical,
            "Boolean-based SQL injection attempt"),
        
        new SecurityPattern(
            @"(;|\s)(DROP|ALTER|CREATE|TRUNCATE|EXEC|EXECUTE)\s", 
            SecurityRiskType.SQLInjection, 
            RiskSeverity.Critical,
            "DDL/DML injection attempt"),
        
        new SecurityPattern(
            @"UNION\s+ALL\s+SELECT", 
            SecurityRiskType.SQLInjection, 
            RiskSeverity.High,
            "UNION-based SQL injection"),
        
        // System access attempts
        new SecurityPattern(
            @"sys\.(objects|tables|columns|databases)", 
            SecurityRiskType.UnauthorizedAccess, 
            RiskSeverity.High,
            "System catalog access attempt"),
        
        new SecurityPattern(
            @"INFORMATION_SCHEMA\.", 
            SecurityRiskType.UnauthorizedAccess, 
            RiskSeverity.Medium,
            "Information schema access"),
        
        // Data exfiltration attempts
        new SecurityPattern(
            @"xp_cmdshell", 
            SecurityRiskType.DataExfiltration, 
            RiskSeverity.Critical,
            "Command shell execution attempt"),
        
        new SecurityPattern(
            @"OPENROWSET", 
            SecurityRiskType.DataExfiltration, 
            RiskSeverity.High,
            "External data source access"),
        
        // Performance attacks
        new SecurityPattern(
            @"WAITFOR\s+DELAY", 
            SecurityRiskType.PerformanceAttack, 
            RiskSeverity.Medium,
            "Time-based attack attempt"),
        
        new SecurityPattern(
            @"BENCHMARK\s*\(", 
            SecurityRiskType.PerformanceAttack, 
            RiskSeverity.Medium,
            "Benchmark-based attack"),
        
        // Modern threats
        new SecurityPattern(
            @"JSON_QUERY.*OPENJSON", 
            SecurityRiskType.DataExfiltration, 
            RiskSeverity.Medium,
            "JSON-based data extraction"),
        
        new SecurityPattern(
            @"FOR\s+XML\s+PATH", 
            SecurityRiskType.DataExfiltration, 
            RiskSeverity.Medium,
            "XML-based data extraction")
    };

    // Safe query patterns (whitelist approach)
    private readonly Regex _safeSelectPattern = new(
        @"^\s*SELECT\s+.+\s+FROM\s+[\w\.\[\]]+(\s+WHERE\s+.+)?(\s+ORDER\s+BY\s+.+)?(\s+GROUP\s+BY\s+.+)?;?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Singleline);

    public GovernanceSecurityEngine(ILogger<GovernanceSecurityEngine> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activitySource = new ActivitySource($"{nameof(GovernanceSecurityEngine)}-v2");
    }

    /// <summary>
    /// Validates query security with comprehensive threat detection.
    /// </summary>
    public async Task<GovernanceValidationResult> ValidateQuerySecurityAsync(GovernanceQueryRequest request, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("ValidateQuerySecurity");
        activity?.SetTag("agent.id", request.AgentId);
        activity?.SetTag("query.length", request.SqlQuery.Length);

        var securityRisks = new List<SecurityRisk>();
        var warnings = new List<string>();
        var recommendations = new List<string>();

        try
        {
            var normalizedQuery = NormalizeQuery(request.SqlQuery);

            // Validation 1: Basic query constraints
            var basicValidation = ValidateBasicConstraints(normalizedQuery, securityRisks);
            if (!basicValidation)
            {
                return GovernanceValidationResult.Failure("Basic validation failed", securityRisks);
            }

            // Validation 2: SQL injection detection
            await ValidateSQLInjectionAsync(normalizedQuery, securityRisks, cancellationToken);

            // Validation 3: Dangerous pattern detection
            await ValidateDangerousPatternsAsync(normalizedQuery, securityRisks, cancellationToken);

            // Validation 4: Query complexity analysis
            ValidateQueryComplexity(normalizedQuery, securityRisks, warnings);

            // Validation 5: Table access validation
            ValidateTableAccess(request, securityRisks, warnings);

            // Check if any critical or high-severity risks were found
            var criticalRisks = securityRisks.Where(r => r.Severity >= RiskSeverity.High).ToList();
            if (criticalRisks.Any())
            {
                var riskDescriptions = string.Join("; ", criticalRisks.Select(r => r.Description));
                _logger.LogWarning("Critical security risks detected for agent {AgentId}: {Risks}", 
                    request.AgentId, riskDescriptions);
                
                activity?.SetTag("validation.critical_risks", criticalRisks.Count);
                return GovernanceValidationResult.Failure($"Critical security risks detected: {riskDescriptions}", securityRisks);
            }

            // Add recommendations for medium/low risks
            if (securityRisks.Any())
            {
                recommendations.Add("Review query for potential security improvements");
                recommendations.Add("Consider using stored procedures for complex operations");
            }

            _logger.LogInformation("Query security validation passed for agent {AgentId} with {RiskCount} risks and {WarningCount} warnings", 
                request.AgentId, securityRisks.Count, warnings.Count);

            activity?.SetTag("validation.success", true);
            activity?.SetTag("validation.risks", securityRisks.Count);
            activity?.SetTag("validation.warnings", warnings.Count);

            return new GovernanceValidationResult
            {
                IsValid = true,
                SecurityRisks = securityRisks,
                Warnings = warnings,
                Recommendations = recommendations
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Security validation failed for agent {AgentId}", request.AgentId);
            activity?.SetTag("validation.error", ex.Message);
            return GovernanceValidationResult.Failure($"Validation error: {ex.Message}");
        }
    }

    private bool ValidateBasicConstraints(string normalizedQuery, List<SecurityRisk> risks)
    {
        // Length check
        if (normalizedQuery.Length > 10000)
        {
            risks.Add(new SecurityRisk
            {
                Type = SecurityRiskType.PerformanceAttack,
                Severity = RiskSeverity.Medium,
                Description = $"Query exceeds maximum length ({normalizedQuery.Length} characters)"
            });
            return false;
        }

        // Read-only check
        if (!normalizedQuery.Trim().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            risks.Add(new SecurityRisk
            {
                Type = SecurityRiskType.UnauthorizedAccess,
                Severity = RiskSeverity.Critical,
                Description = "Only SELECT queries are allowed"
            });
            return false;
        }

        return true;
    }

    private async Task ValidateSQLInjectionAsync(string query, List<SecurityRisk> risks, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            // Check for SQL injection patterns
            foreach (var pattern in _dangerousPatterns.Where(p => p.Type == SecurityRiskType.SQLInjection))
            {
                if (pattern.Regex.IsMatch(query))
                {
                    risks.Add(new SecurityRisk
                    {
                        Type = pattern.Type,
                        Severity = pattern.Severity,
                        Description = pattern.Description,
                        Location = FindPatternLocation(query, pattern.Regex),
                        Mitigation = GetMitigationAdvice(pattern.Type)
                    });
                }
            }
        }, cancellationToken);
    }

    private async Task ValidateDangerousPatternsAsync(string query, List<SecurityRisk> risks, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            // Check all dangerous patterns except SQL injection (already checked)
            foreach (var pattern in _dangerousPatterns.Where(p => p.Type != SecurityRiskType.SQLInjection))
            {
                if (pattern.Regex.IsMatch(query))
                {
                    risks.Add(new SecurityRisk
                    {
                        Type = pattern.Type,
                        Severity = pattern.Severity,
                        Description = pattern.Description,
                        Location = FindPatternLocation(query, pattern.Regex),
                        Mitigation = GetMitigationAdvice(pattern.Type)
                    });
                }
            }
        }, cancellationToken);
    }

    private void ValidateQueryComplexity(string query, List<SecurityRisk> risks, List<string> warnings)
    {
        // JOIN complexity check
        var joinCount = CountOccurrences(query, @"\bJOIN\b");
        if (joinCount > 5)
        {
            risks.Add(new SecurityRisk
            {
                Type = SecurityRiskType.PerformanceAttack,
                Severity = RiskSeverity.Medium,
                Description = $"Query has {joinCount} JOINs (maximum recommended: 5)"
            });
        }
        else if (joinCount > 3)
        {
            warnings.Add($"Query has {joinCount} JOINs - consider optimization");
        }

        // Subquery depth check
        var subqueryDepth = CalculateSubqueryDepth(query);
        if (subqueryDepth > 3)
        {
            risks.Add(new SecurityRisk
            {
                Type = SecurityRiskType.PerformanceAttack,
                Severity = RiskSeverity.Medium,
                Description = $"Query has subquery nesting depth of {subqueryDepth} (maximum recommended: 3)"
            });
        }
    }

    private void ValidateTableAccess(GovernanceQueryRequest request, List<SecurityRisk> risks, List<string> warnings)
    {
        // Check for system table access
        var systemTables = new[] { "sys.", "INFORMATION_SCHEMA.", "master.", "msdb.", "tempdb." };
        
        foreach (var table in request.RequestedTables)
        {
            if (systemTables.Any(st => table.StartsWith(st, StringComparison.OrdinalIgnoreCase)))
            {
                risks.Add(new SecurityRisk
                {
                    Type = SecurityRiskType.UnauthorizedAccess,
                    Severity = RiskSeverity.High,
                    Description = $"Attempt to access system table: {table}"
                });
            }
        }

        // Warn about cross-database access
        if (request.RequestedTables.Any(t => t.Contains(".")))
        {
            warnings.Add("Cross-database or schema access detected - ensure proper authorization");
        }
    }

    private string NormalizeQuery(string query)
    {
        // Remove extra whitespace
        query = Regex.Replace(query, @"\s+", " ");
        
        // Remove comments to prevent comment-based injection
        query = Regex.Replace(query, @"--.*$", "", RegexOptions.Multiline);
        query = Regex.Replace(query, @"/\*.*?\*/", "", RegexOptions.Singleline);
        
        return query.Trim();
    }

    private int CountOccurrences(string input, string pattern)
    {
        return Regex.Matches(input, pattern, RegexOptions.IgnoreCase).Count;
    }

    private int CalculateSubqueryDepth(string query)
    {
        int maxDepth = 0;
        int currentDepth = 0;

        foreach (char c in query)
        {
            if (c == '(')
            {
                currentDepth++;
                maxDepth = Math.Max(maxDepth, currentDepth);
            }
            else if (c == ')')
            {
                currentDepth--;
            }
        }

        return maxDepth;
    }

    private string FindPatternLocation(string query, Regex pattern)
    {
        var match = pattern.Match(query);
        if (match.Success)
        {
            var lineNumber = query.Substring(0, match.Index).Count(c => c == '\n') + 1;
            return $"Line {lineNumber}, Position {match.Index}";
        }
        return "Unknown location";
    }

    private string GetMitigationAdvice(SecurityRiskType riskType)
    {
        return riskType switch
        {
            SecurityRiskType.SQLInjection => "Use parameterized queries and input validation",
            SecurityRiskType.UnauthorizedAccess => "Verify table access permissions and use principle of least privilege",
            SecurityRiskType.DataExfiltration => "Review data access patterns and implement additional monitoring",
            SecurityRiskType.PerformanceAttack => "Optimize query structure and implement resource limits",
            SecurityRiskType.PrivilegeEscalation => "Review security context and access controls",
            _ => "Review query for security best practices"
        };
    }

    public void Dispose()
    {
        _activitySource?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Security pattern definition for threat detection.
/// </summary>
public record SecurityPattern
{
    public Regex Regex { get; }
    public SecurityRiskType Type { get; }
    public RiskSeverity Severity { get; }
    public string Description { get; }

    public SecurityPattern(string pattern, SecurityRiskType type, RiskSeverity severity, string description)
    {
        Regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        Type = type;
        Severity = severity;
        Description = description;
    }
}
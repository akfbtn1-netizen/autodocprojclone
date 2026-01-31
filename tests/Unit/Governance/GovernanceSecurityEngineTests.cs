using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using Enterprise.Documentation.Core.Governance;

namespace Tests.Unit.Governance;

/// <summary>
/// Comprehensive unit tests for GovernanceSecurityEngine.
/// Tests SQL injection detection, query validation, and security threat detection.
///
/// Test Strategy:
/// 1. True Positives - Known attack patterns that MUST be detected
/// 2. True Negatives - Legitimate queries that must NOT trigger false positives
/// 3. Edge Cases - Boundary conditions, obfuscation attempts
/// 4. Severity Classification - Verify risk severity is accurate
/// 5. Query Complexity - Test JOIN and subquery limits
/// </summary>
public class GovernanceSecurityEngineTests
{
    private readonly GovernanceSecurityEngine _engine;
    private readonly Mock<ILogger<GovernanceSecurityEngine>> _mockLogger;

    public GovernanceSecurityEngineTests()
    {
        _mockLogger = new Mock<ILogger<GovernanceSecurityEngine>>();
        _engine = new GovernanceSecurityEngine(_mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => new GovernanceSecurityEngine(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region Valid Query Tests - True Negatives (Must NOT trigger false positives)

    [Theory]
    [InlineData("SELECT * FROM Documents WHERE Id = 1", "Simple select with WHERE")]
    [InlineData("SELECT Id, Title, Category FROM Documents", "Select specific columns")]
    [InlineData("SELECT * FROM Documents WHERE Title LIKE '%test%'", "LIKE query")]
    [InlineData("SELECT * FROM Documents WHERE CreatedAt > '2024-01-01'", "Date comparison")]
    [InlineData("SELECT * FROM Documents ORDER BY CreatedAt DESC", "ORDER BY clause")]
    [InlineData("SELECT * FROM Documents WHERE Status = 'Active' AND Category = 'Technical'", "Multiple WHERE conditions")]
    [InlineData("SELECT d.Title, u.Name FROM Documents d JOIN Users u ON d.AuthorId = u.Id", "Simple JOIN")]
    [InlineData("SELECT * FROM Documents GROUP BY Category", "GROUP BY clause")]
    public async Task ValidateQuerySecurityAsync_WithValidQueries_ShouldPass(string query, string testCase)
    {
        // Arrange
        var request = CreateQueryRequest(query);

        // Act
        var result = await _engine.ValidateQuerySecurityAsync(request);

        // Assert
        result.IsValid.Should().BeTrue(because: $"{testCase} should be a valid query");
    }

    [Fact]
    public async Task ValidateQuerySecurityAsync_WithSimpleSelectQuery_ShouldReturnValidResult()
    {
        // Arrange
        var request = CreateQueryRequest("SELECT Id, Title FROM Documents WHERE Status = 'Published'");

        // Act
        var result = await _engine.ValidateQuerySecurityAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.SecurityRisks.Should().BeEmpty();
    }

    #endregion

    #region SQL Injection Detection Tests - True Positives

    [Theory]
    [InlineData("SELECT * FROM Documents WHERE Id = 1 OR '1'='1'", "Boolean-based SQL injection")]
    [InlineData("SELECT * FROM Documents WHERE Id = 1 AND '1'='1'", "AND-based SQL injection")]
    [InlineData("SELECT * FROM Documents; DROP TABLE Users;--", "DDL injection - DROP")]
    [InlineData("SELECT * FROM Documents; EXEC xp_cmdshell 'dir'", "Command execution injection")]
    [InlineData("SELECT * FROM Documents; TRUNCATE TABLE Users", "DDL injection - TRUNCATE")]
    [InlineData("SELECT * FROM Documents; ALTER TABLE Users DROP COLUMN Password", "DDL injection - ALTER")]
    [InlineData("SELECT * FROM Documents; CREATE TABLE Hack(id int)", "DDL injection - CREATE")]
    public async Task ValidateQuerySecurityAsync_WithSQLInjection_ShouldDetect(string query, string testCase)
    {
        // Arrange
        var request = CreateQueryRequest(query);

        // Act
        var result = await _engine.ValidateQuerySecurityAsync(request);

        // Assert
        result.IsValid.Should().BeFalse(because: $"{testCase} should be detected as SQL injection");
        result.SecurityRisks.Should().Contain(r => r.Type == SecurityRiskType.SQLInjection);
    }

    [Fact]
    public async Task ValidateQuerySecurityAsync_WithUnionInjection_ShouldDetect()
    {
        // Arrange
        var request = CreateQueryRequest("SELECT * FROM Documents UNION ALL SELECT * FROM Users");

        // Act
        var result = await _engine.ValidateQuerySecurityAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.SecurityRisks.Should().Contain(r =>
            r.Type == SecurityRiskType.SQLInjection &&
            r.Description.Contains("UNION", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Non-SELECT Query Tests

    [Theory]
    [InlineData("INSERT INTO Documents (Title) VALUES ('Test')", "INSERT statement")]
    [InlineData("UPDATE Documents SET Title = 'New' WHERE Id = 1", "UPDATE statement")]
    [InlineData("DELETE FROM Documents WHERE Id = 1", "DELETE statement")]
    [InlineData("DROP TABLE Documents", "DROP statement")]
    [InlineData("TRUNCATE TABLE Documents", "TRUNCATE statement")]
    [InlineData("ALTER TABLE Documents ADD NewColumn VARCHAR(100)", "ALTER statement")]
    [InlineData("CREATE TABLE NewTable (Id INT)", "CREATE statement")]
    [InlineData("EXEC StoredProcedure", "EXEC statement")]
    public async Task ValidateQuerySecurityAsync_WithNonSelectQuery_ShouldReject(string query, string testCase)
    {
        // Arrange
        var request = CreateQueryRequest(query);

        // Act
        var result = await _engine.ValidateQuerySecurityAsync(request);

        // Assert
        result.IsValid.Should().BeFalse(because: $"{testCase} - only SELECT queries are allowed");
        result.SecurityRisks.Should().Contain(r =>
            r.Type == SecurityRiskType.UnauthorizedAccess &&
            r.Severity == RiskSeverity.Critical);
    }

    #endregion

    #region System Table Access Tests

    [Theory]
    [InlineData("SELECT * FROM sys.objects", "sys.objects access")]
    [InlineData("SELECT * FROM sys.tables", "sys.tables access")]
    [InlineData("SELECT * FROM sys.columns", "sys.columns access")]
    [InlineData("SELECT * FROM sys.databases", "sys.databases access")]
    [InlineData("SELECT * FROM INFORMATION_SCHEMA.TABLES", "INFORMATION_SCHEMA access")]
    [InlineData("SELECT * FROM INFORMATION_SCHEMA.COLUMNS", "INFORMATION_SCHEMA.COLUMNS access")]
    public async Task ValidateQuerySecurityAsync_WithSystemTableAccess_ShouldDetect(string query, string testCase)
    {
        // Arrange
        var request = CreateQueryRequest(query);

        // Act
        var result = await _engine.ValidateQuerySecurityAsync(request);

        // Assert
        result.IsValid.Should().BeFalse(because: $"{testCase} should be detected as unauthorized access");
        result.SecurityRisks.Should().Contain(r => r.Type == SecurityRiskType.UnauthorizedAccess);
    }

    #endregion

    #region Data Exfiltration Tests

    [Theory]
    [InlineData("SELECT * FROM Documents; EXEC xp_cmdshell 'dir'", "xp_cmdshell execution")]
    [InlineData("SELECT * FROM OPENROWSET('SQLNCLI', 'Server=hack;', 'SELECT * FROM data')", "OPENROWSET")]
    [InlineData("SELECT * FROM Documents FOR XML PATH('')", "XML PATH extraction")]
    public async Task ValidateQuerySecurityAsync_WithDataExfiltrationAttempt_ShouldDetect(string query, string testCase)
    {
        // Arrange
        var request = CreateQueryRequest(query);

        // Act
        var result = await _engine.ValidateQuerySecurityAsync(request);

        // Assert
        result.IsValid.Should().BeFalse(because: $"{testCase} should be detected as data exfiltration");
    }

    #endregion

    #region Performance Attack Tests

    [Theory]
    [InlineData("SELECT * FROM Documents; WAITFOR DELAY '00:00:10'", "Time-based delay attack")]
    [InlineData("SELECT BENCHMARK(10000000, SHA1('test'))", "Benchmark attack")]
    public async Task ValidateQuerySecurityAsync_WithPerformanceAttack_ShouldDetect(string query, string testCase)
    {
        // Arrange
        var request = CreateQueryRequest(query);

        // Act
        var result = await _engine.ValidateQuerySecurityAsync(request);

        // Assert
        result.IsValid.Should().BeFalse(because: $"{testCase} should be detected as performance attack");
        result.SecurityRisks.Should().Contain(r => r.Type == SecurityRiskType.PerformanceAttack);
    }

    #endregion

    #region Query Complexity Tests - JOIN Limits

    [Fact]
    public async Task ValidateQuerySecurityAsync_WithTooManyJoins_ShouldAddRisk()
    {
        // Arrange - 6 JOINs (exceeds limit of 5)
        var query = @"
            SELECT * FROM Documents d
            JOIN Users u1 ON d.AuthorId = u1.Id
            JOIN Users u2 ON d.ReviewerId = u2.Id
            JOIN Categories c ON d.CategoryId = c.Id
            JOIN Tags t ON d.Id = t.DocumentId
            JOIN Permissions p ON d.Id = p.DocumentId
            JOIN AuditLogs a ON d.Id = a.DocumentId";
        var request = CreateQueryRequest(query);

        // Act
        var result = await _engine.ValidateQuerySecurityAsync(request);

        // Assert
        result.IsValid.Should().BeFalse(because: "more than 5 JOINs should trigger performance risk");
        result.SecurityRisks.Should().Contain(r =>
            r.Type == SecurityRiskType.PerformanceAttack &&
            r.Description.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateQuerySecurityAsync_WithAcceptableJoins_ShouldPass()
    {
        // Arrange - 3 JOINs (within limits, might get warning)
        var query = @"
            SELECT d.Title, u.Name, c.Name
            FROM Documents d
            JOIN Users u ON d.AuthorId = u.Id
            JOIN Categories c ON d.CategoryId = c.Id";
        var request = CreateQueryRequest(query);

        // Act
        var result = await _engine.ValidateQuerySecurityAsync(request);

        // Assert
        result.IsValid.Should().BeTrue(because: "3 JOINs is acceptable");
    }

    #endregion

    #region Query Complexity Tests - Subquery Depth

    [Fact]
    public async Task ValidateQuerySecurityAsync_WithDeepSubqueries_ShouldAddRisk()
    {
        // Arrange - Nesting depth > 3
        var query = @"
            SELECT * FROM Documents WHERE Id IN (
                SELECT DocumentId FROM Tags WHERE TagName IN (
                    SELECT Name FROM Categories WHERE ParentId IN (
                        SELECT Id FROM Categories WHERE Level IN (
                            SELECT MAX(Level) FROM Categories
                        )
                    )
                )
            )";
        var request = CreateQueryRequest(query);

        // Act
        var result = await _engine.ValidateQuerySecurityAsync(request);

        // Assert
        result.SecurityRisks.Should().Contain(r =>
            r.Type == SecurityRiskType.PerformanceAttack &&
            r.Description.Contains("subquery", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Query Length Tests

    [Fact]
    public async Task ValidateQuerySecurityAsync_WithExcessivelyLongQuery_ShouldReject()
    {
        // Arrange - Query longer than 10000 characters
        var longCondition = string.Join(" OR ", Enumerable.Range(1, 1000).Select(i => $"Id = {i}"));
        var query = $"SELECT * FROM Documents WHERE {longCondition}";
        var request = CreateQueryRequest(query);

        // Act
        var result = await _engine.ValidateQuerySecurityAsync(request);

        // Assert
        result.IsValid.Should().BeFalse(because: "query exceeds maximum length");
        result.SecurityRisks.Should().Contain(r => r.Description.Contains("length", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Comment Stripping Tests

    [Theory]
    [InlineData("SELECT * FROM Documents -- comment", "Single line comment")]
    [InlineData("SELECT * FROM Documents /* block comment */", "Block comment")]
    [InlineData("SELECT * FROM Documents /* multi\nline\ncomment */", "Multi-line comment")]
    public async Task ValidateQuerySecurityAsync_WithComments_ShouldStripAndValidate(string query, string testCase)
    {
        // Arrange
        var request = CreateQueryRequest(query);

        // Act
        var result = await _engine.ValidateQuerySecurityAsync(request);

        // Assert - Comments should be stripped, query should still be valid
        result.IsValid.Should().BeTrue(because: $"{testCase} should be stripped during normalization");
    }

    [Fact]
    public async Task ValidateQuerySecurityAsync_WithInjectionHiddenInComment_ShouldStripComment()
    {
        // Arrange - Injection attempt hidden after comment
        var query = "SELECT * FROM Documents -- ; DROP TABLE Users;";
        var request = CreateQueryRequest(query);

        // Act
        var result = await _engine.ValidateQuerySecurityAsync(request);

        // Assert - Comment is stripped, remaining query is valid
        result.IsValid.Should().BeTrue(because: "injection after -- comment is stripped");
    }

    #endregion

    #region System Table Request Validation

    [Fact]
    public async Task ValidateQuerySecurityAsync_WithSystemTableInRequestedTables_ShouldReject()
    {
        // Arrange
        var request = new GovernanceQueryRequest
        {
            AgentId = "test-agent",
            AgentName = "Test Agent",
            AgentPurpose = "Unit Testing",
            DatabaseName = "TestDB",
            SqlQuery = "SELECT * FROM Documents",
            RequestedTables = new List<string> { "Documents", "sys.objects" },
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        var result = await _engine.ValidateQuerySecurityAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.SecurityRisks.Should().Contain(r =>
            r.Type == SecurityRiskType.UnauthorizedAccess &&
            r.Description.Contains("sys.objects", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("master.dbo.sysobjects", "master database")]
    [InlineData("msdb.dbo.backupset", "msdb database")]
    [InlineData("tempdb.dbo.temp_table", "tempdb database")]
    public async Task ValidateQuerySecurityAsync_WithSystemDatabaseAccess_ShouldReject(string table, string testCase)
    {
        // Arrange
        var request = new GovernanceQueryRequest
        {
            AgentId = "test-agent",
            AgentName = "Test Agent",
            AgentPurpose = "Unit Testing",
            DatabaseName = "TestDB",
            SqlQuery = "SELECT * FROM Documents",
            RequestedTables = new List<string> { table },
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        var result = await _engine.ValidateQuerySecurityAsync(request);

        // Assert
        result.IsValid.Should().BeFalse(because: $"{testCase} access should be rejected");
    }

    #endregion

    #region Risk Severity Tests

    [Fact]
    public async Task ValidateQuerySecurityAsync_WithCriticalRisk_ShouldIncludeSeverityInRisk()
    {
        // Arrange - DDL injection is Critical severity
        var request = CreateQueryRequest("SELECT * FROM Documents; DROP TABLE Users;--");

        // Act
        var result = await _engine.ValidateQuerySecurityAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.SecurityRisks.Should().Contain(r => r.Severity == RiskSeverity.Critical);
    }

    [Fact]
    public async Task ValidateQuerySecurityAsync_WithHighRisk_ShouldIncludeSeverityInRisk()
    {
        // Arrange - System catalog access is High severity
        var request = CreateQueryRequest("SELECT * FROM sys.objects");

        // Act
        var result = await _engine.ValidateQuerySecurityAsync(request);

        // Assert
        result.SecurityRisks.Should().Contain(r => r.Severity == RiskSeverity.High);
    }

    #endregion

    #region Mitigation Advice Tests

    [Fact]
    public async Task ValidateQuerySecurityAsync_WithSQLInjection_ShouldProvideMitigationAdvice()
    {
        // Arrange
        var request = CreateQueryRequest("SELECT * FROM Documents WHERE Id = 1 OR '1'='1'");

        // Act
        var result = await _engine.ValidateQuerySecurityAsync(request);

        // Assert
        result.SecurityRisks.Should().Contain(r =>
            r.Type == SecurityRiskType.SQLInjection &&
            !string.IsNullOrEmpty(r.Mitigation));

        var sqlInjectionRisk = result.SecurityRisks.First(r => r.Type == SecurityRiskType.SQLInjection);
        sqlInjectionRisk.Mitigation.Should().Contain("parameterized", StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Recommendations Tests

    [Fact]
    public async Task ValidateQuerySecurityAsync_WithMediumRisks_ShouldProvideRecommendations()
    {
        // Arrange - Query with 4 JOINs (warns but passes)
        var query = @"
            SELECT * FROM Documents d
            JOIN Users u ON d.AuthorId = u.Id
            JOIN Categories c ON d.CategoryId = c.Id
            JOIN Tags t ON d.Id = t.DocumentId
            JOIN Permissions p ON d.Id = p.DocumentId";
        var request = CreateQueryRequest(query);

        // Act
        var result = await _engine.ValidateQuerySecurityAsync(request);

        // Assert
        if (!result.IsValid || result.SecurityRisks.Any())
        {
            result.Recommendations.Should().NotBeEmpty(
                because: "queries with risks should include recommendations");
        }
    }

    #endregion

    #region Concurrent Validation Tests

    [Fact]
    public async Task ValidateQuerySecurityAsync_WithConcurrentCalls_ShouldBeThreadSafe()
    {
        // Arrange
        var queries = new[]
        {
            "SELECT * FROM Documents WHERE Id = 1",
            "SELECT * FROM Documents; DROP TABLE Users;--",
            "SELECT * FROM sys.objects",
            "SELECT * FROM Documents WHERE Status = 'Active'",
            "SELECT * FROM Documents WHERE Id = 1 OR '1'='1'"
        };

        // Act - Run all validations concurrently
        var tasks = queries.Select(async query =>
        {
            var request = CreateQueryRequest(query);
            return await _engine.ValidateQuerySecurityAsync(request);
        });

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(5);
        results[0].IsValid.Should().BeTrue(); // Valid query
        results[1].IsValid.Should().BeFalse(); // DROP injection
        results[2].IsValid.Should().BeFalse(); // sys.objects access
        results[3].IsValid.Should().BeTrue(); // Valid query
        results[4].IsValid.Should().BeFalse(); // Boolean injection
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task ValidateQuerySecurityAsync_WithWhitespaceVariations_ShouldNormalizeAndValidate()
    {
        // Arrange - Query with extra whitespace
        var query = "SELECT    *   FROM    Documents   WHERE   Id   =   1";
        var request = CreateQueryRequest(query);

        // Act
        var result = await _engine.ValidateQuerySecurityAsync(request);

        // Assert
        result.IsValid.Should().BeTrue(because: "whitespace normalization should handle this");
    }

    [Fact]
    public async Task ValidateQuerySecurityAsync_WithMixedCaseKeywords_ShouldValidate()
    {
        // Arrange - Mixed case keywords
        var query = "SeLeCt * FrOm Documents WhErE Id = 1";
        var request = CreateQueryRequest(query);

        // Act
        var result = await _engine.ValidateQuerySecurityAsync(request);

        // Assert
        result.IsValid.Should().BeTrue(because: "SQL keywords are case-insensitive");
    }

    #endregion

    #region Cross-Database Access Warning

    [Fact]
    public async Task ValidateQuerySecurityAsync_WithSchemaQualifiedTable_ShouldWarn()
    {
        // Arrange
        var request = new GovernanceQueryRequest
        {
            AgentId = "test-agent",
            AgentName = "Test Agent",
            AgentPurpose = "Unit Testing",
            DatabaseName = "TestDB",
            SqlQuery = "SELECT * FROM Documents",
            RequestedTables = new List<string> { "dbo.Documents", "archive.Documents" },
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        var result = await _engine.ValidateQuerySecurityAsync(request);

        // Assert
        result.Warnings.Should().Contain(w =>
            w.Contains("cross-database", StringComparison.OrdinalIgnoreCase) ||
            w.Contains("schema access", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Helper Methods

    private static GovernanceQueryRequest CreateQueryRequest(string query)
    {
        return new GovernanceQueryRequest
        {
            AgentId = "test-agent",
            AgentName = "Test Agent",
            AgentPurpose = "Unit Testing",
            DatabaseName = "TestDB",
            SqlQuery = query,
            RequestedTables = new List<string>(),
            CorrelationId = Guid.NewGuid().ToString()
        };
    }

    private static GovernanceQueryRequest CreateQueryRequestWithTables(string query, List<string> tables)
    {
        return new GovernanceQueryRequest
        {
            AgentId = "test-agent",
            AgentName = "Test Agent",
            AgentPurpose = "Unit Testing",
            DatabaseName = "TestDB",
            SqlQuery = query,
            RequestedTables = tables,
            CorrelationId = Guid.NewGuid().ToString()
        };
    }

    #endregion
}

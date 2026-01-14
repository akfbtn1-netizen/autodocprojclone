// =============================================================================
// Agent #7: Gap Intelligence Agent - Core Service Implementation
// ML-style documentation gap detection with pattern-based analysis
// =============================================================================

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using Dapper;
using Polly;
using Polly.Retry;

namespace Enterprise.Documentation.Core.Application.Services.GapIntelligence;

/// <summary>
/// Core Gap Intelligence Agent implementation.
/// Detects documentation gaps using learned patterns, usage analysis, and semantic clustering.
/// </summary>
public class GapIntelligenceAgent : IGapIntelligenceAgent
{
    private readonly ILogger<GapIntelligenceAgent> _logger;
    private readonly string _connectionString;
    private readonly IQueryPatternMiner _queryMiner;
    private readonly ISemanticClusteringService _clusteringService;
    private readonly AsyncRetryPolicy _retryPolicy;

    private static readonly int[] TransientErrorNumbers = { -2, 20, 64, 233, 10053, 10054, 10060, 10928, 10929, 40197, 40501, 40613 };

    public GapIntelligenceAgent(
        ILogger<GapIntelligenceAgent> logger,
        IConfiguration configuration,
        IQueryPatternMiner queryMiner,
        ISemanticClusteringService clusteringService)
    {
        _logger = logger;
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        _queryMiner = queryMiner;
        _clusteringService = clusteringService;

        _retryPolicy = Policy
            .Handle<SqlException>(ex => TransientErrorNumbers.Contains(ex.Number))
            .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                (ex, ts, attempt, _) => _logger.LogWarning(ex, "Retry {Attempt} after {Delay}s", attempt, ts.TotalSeconds));
    }

    #region Detection

    public async Task<GapDetectionResult> RunFullDetectionAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Starting full gap detection");
        var result = new GapDetectionResult { StartedAt = DateTime.UtcNow };

        return await _retryPolicy.ExecuteAsync(async () =>
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            // Create detection run record
            result.RunId = await connection.QuerySingleAsync<int>(@"
                INSERT INTO DaQa.GapDetectionRuns (RunType, StartedAt)
                OUTPUT INSERTED.RunId VALUES ('FULL', GETUTCDATE())");

            // Get all database objects
            var objects = await connection.QueryAsync<(string Schema, string Name, string Type)>(@"
                SELECT s.name, o.name, o.type_desc
                FROM sys.objects o JOIN sys.schemas s ON o.schema_id = s.schema_id
                WHERE o.type IN ('U', 'V', 'P', 'FN', 'IF', 'TF')
                  AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')");

            result.ObjectsScanned = objects.Count();
            var patterns = await GetActivePatternsAsync(ct);
            var newGaps = new List<DetectedGap>();

            foreach (var obj in objects)
            {
                var gaps = await DetectGapsForObjectInternalAsync(connection, obj.Schema, obj.Name, obj.Type, patterns);
                newGaps.AddRange(gaps);
            }

            // Insert new gaps (avoid duplicates)
            foreach (var gap in newGaps)
            {
                await connection.ExecuteAsync(@"
                    IF NOT EXISTS (SELECT 1 FROM DaQa.DetectedGaps WHERE SchemaName=@SchemaName AND ObjectName=@ObjectName AND GapType=@GapType AND Status='OPEN')
                    INSERT INTO DaQa.DetectedGaps (SchemaName, ObjectName, ObjectType, GapType, PatternId, Severity, Priority, Confidence, Evidence, DetectionRunId)
                    VALUES (@SchemaName, @ObjectName, @ObjectType, @GapType, @PatternId, @Severity, @Priority, @Confidence, @Evidence, @RunId)",
                    new { gap.SchemaName, gap.ObjectName, gap.ObjectType, gap.GapType, gap.PatternId, gap.Severity, gap.Priority, gap.Confidence, gap.Evidence, RunId = result.RunId });
            }

            result.GapsDetected = newGaps.Count;
            result.NewGaps = newGaps.Count;
            result.CompletedAt = DateTime.UtcNow;

            // Update run record
            await connection.ExecuteAsync(@"
                UPDATE DaQa.GapDetectionRuns
                SET CompletedAt=GETUTCDATE(), DurationMs=DATEDIFF(MS, StartedAt, GETUTCDATE()),
                    ObjectsScanned=@Scanned, GapsDetected=@Detected, NewGaps=@New
                WHERE RunId=@RunId",
                new { Scanned = result.ObjectsScanned, Detected = result.GapsDetected, New = result.NewGaps, result.RunId });

            _logger.LogInformation("Full detection complete: {Gaps} gaps from {Objects} objects", result.GapsDetected, result.ObjectsScanned);
            return result;
        });
    }

    public async Task<GapDetectionResult> RunIncrementalDetectionAsync(DateTime since, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting incremental detection since {Since}", since);
        var result = new GapDetectionResult { StartedAt = DateTime.UtcNow };

        return await _retryPolicy.ExecuteAsync(async () =>
        {
            using var connection = new SqlConnection(_connectionString);

            // Create run record
            result.RunId = await connection.QuerySingleAsync<int>(@"
                INSERT INTO DaQa.GapDetectionRuns (RunType, StartedAt)
                OUTPUT INSERTED.RunId VALUES ('INCREMENTAL', GETUTCDATE())");

            var modifiedObjects = await connection.QueryAsync<(string Schema, string Name, string Type)>(@"
                SELECT s.name, o.name, o.type_desc
                FROM sys.objects o JOIN sys.schemas s ON o.schema_id = s.schema_id
                WHERE o.modify_date > @Since AND o.type IN ('U', 'V', 'P', 'FN', 'IF', 'TF')",
                new { Since = since });

            result.ObjectsScanned = modifiedObjects.Count();
            var patterns = await GetActivePatternsAsync(ct);

            foreach (var obj in modifiedObjects)
            {
                var gaps = await DetectGapsForObjectInternalAsync(connection, obj.Schema, obj.Name, obj.Type, patterns);
                result.GapsDetected += gaps.Count;
            }

            result.CompletedAt = DateTime.UtcNow;

            // Update run record
            await connection.ExecuteAsync(@"
                UPDATE DaQa.GapDetectionRuns
                SET CompletedAt=GETUTCDATE(), DurationMs=DATEDIFF(MS, StartedAt, GETUTCDATE()),
                    ObjectsScanned=@Scanned, GapsDetected=@Detected
                WHERE RunId=@RunId",
                new { Scanned = result.ObjectsScanned, Detected = result.GapsDetected, result.RunId });

            return result;
        });
    }

    public async Task<List<DetectedGap>> DetectGapsForObjectAsync(string schema, string objectName, CancellationToken ct = default)
    {
        using var connection = new SqlConnection(_connectionString);
        var objType = await connection.QuerySingleOrDefaultAsync<string>(@"
            SELECT o.type_desc FROM sys.objects o JOIN sys.schemas s ON o.schema_id = s.schema_id
            WHERE s.name = @Schema AND o.name = @Name", new { Schema = schema, Name = objectName });

        if (objType == null) return new List<DetectedGap>();

        var patterns = await GetActivePatternsAsync(ct);
        return await DetectGapsForObjectInternalAsync(connection, schema, objectName, objType, patterns);
    }

    private async Task<List<DetectedGap>> DetectGapsForObjectInternalAsync(
        SqlConnection connection, string schema, string objectName, string objectType, List<GapPattern> patterns)
    {
        var gaps = new List<DetectedGap>();

        // Check MasterIndex coverage
        var hasMasterIndex = await connection.QuerySingleAsync<bool>(@"
            SELECT CASE WHEN EXISTS(SELECT 1 FROM DaQa.MasterIndex WHERE SchemaName=@Schema AND ObjectName=@Name AND IsActive=1) THEN 1 ELSE 0 END",
            new { Schema = schema, Name = objectName });

        if (!hasMasterIndex)
        {
            // STRUCTURAL gap - no documentation at all
            gaps.Add(new DetectedGap
            {
                SchemaName = schema,
                ObjectName = objectName,
                ObjectType = objectType,
                GapType = "STRUCTURAL",
                PatternId = patterns.FirstOrDefault(p => p.PatternType == "STRUCTURAL")?.PatternId,
                Severity = "HIGH",
                Priority = 80,
                Confidence = 0.95m,
                Evidence = JsonSerializer.Serialize(new { Reason = "No MasterIndex entry" })
            });
        }

        // Check usage-based gaps (high use, no docs)
        var heatScore = await connection.QuerySingleOrDefaultAsync<decimal?>(@"
            SELECT HeatScore FROM DaQa.UsageHeatmap WHERE SchemaName=@Schema AND ObjectName=@Name",
            new { Schema = schema, Name = objectName });

        if (heatScore > 30 && !hasMasterIndex)
        {
            gaps.Add(new DetectedGap
            {
                SchemaName = schema,
                ObjectName = objectName,
                ObjectType = objectType,
                GapType = "USAGE",
                PatternId = patterns.FirstOrDefault(p => p.PatternType == "USAGE")?.PatternId,
                Severity = heatScore > 50 ? "CRITICAL" : "HIGH",
                Priority = (int)(heatScore.Value * 1.5m),
                Confidence = 0.9m,
                Evidence = JsonSerializer.Serialize(new { HeatScore = heatScore })
            });
        }

        // Check PII compliance gaps
        var piiColumns = await connection.QuerySingleAsync<int>(@"
            SELECT COUNT(*) FROM sys.columns c
            JOIN sys.objects o ON c.object_id = o.object_id
            JOIN sys.schemas s ON o.schema_id = s.schema_id
            WHERE s.name = @Schema AND o.name = @Name
              AND (c.name LIKE '%SSN%' OR c.name LIKE '%email%' OR c.name LIKE '%phone%' OR c.name LIKE '%DOB%')",
            new { Schema = schema, Name = objectName });

        if (piiColumns > 0 && !hasMasterIndex)
        {
            gaps.Add(new DetectedGap
            {
                SchemaName = schema,
                ObjectName = objectName,
                ObjectType = objectType,
                GapType = "COMPLIANCE",
                PatternId = patterns.FirstOrDefault(p => p.PatternType == "COMPLIANCE")?.PatternId,
                Severity = "CRITICAL",
                Priority = 95,
                Confidence = 0.95m,
                Evidence = JsonSerializer.Serialize(new { PIIColumnCount = piiColumns })
            });
        }

        // Check lineage-based gaps (high dependency count, no docs)
        var dependentCount = await connection.QuerySingleAsync<int>(@"
            SELECT COUNT(DISTINCT referencing_entity_name) FROM sys.sql_expression_dependencies
            WHERE referenced_schema_name=@Schema AND referenced_entity_name=@Name",
            new { Schema = schema, Name = objectName });

        if (dependentCount >= 10 && !hasMasterIndex)
        {
            gaps.Add(new DetectedGap
            {
                SchemaName = schema,
                ObjectName = objectName,
                ObjectType = objectType,
                GapType = "LINEAGE",
                PatternId = patterns.FirstOrDefault(p => p.PatternType == "LINEAGE")?.PatternId,
                Severity = "HIGH",
                Priority = Math.Min(dependentCount * 5, 100),
                Confidence = 0.85m,
                Evidence = JsonSerializer.Serialize(new { DependentCount = dependentCount })
            });
        }

        return gaps;
    }

    #endregion

    #region Analysis

    public async Task<ObjectImportanceScore> CalculateImportanceScoreAsync(string schema, string objectName, CancellationToken ct = default)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            using var connection = new SqlConnection(_connectionString);

            var score = new ObjectImportanceScore { SchemaName = schema, ObjectName = objectName };

            // Get object type
            var objType = await connection.QuerySingleOrDefaultAsync<string>(@"
                SELECT o.type_desc FROM sys.objects o JOIN sys.schemas s ON o.schema_id = s.schema_id
                WHERE s.name = @Schema AND o.name = @Name", new { Schema = schema, Name = objectName });

            score.ObjectType = objType ?? "UNKNOWN";

            // Usage score from heatmap
            var heatScore = await connection.QuerySingleOrDefaultAsync<decimal?>(@"
                SELECT HeatScore FROM DaQa.UsageHeatmap WHERE SchemaName=@Schema AND ObjectName=@Name",
                new { Schema = schema, Name = objectName });
            score.UsageScore = Math.Min((int)(heatScore ?? 0), 100);

            // Dependency score
            var dependents = await connection.QuerySingleAsync<int>(@"
                SELECT COUNT(DISTINCT referencing_entity_name) FROM sys.sql_expression_dependencies
                WHERE referenced_schema_name=@Schema AND referenced_entity_name=@Name",
                new { Schema = schema, Name = objectName });
            score.DependencyScore = Math.Min(dependents * 5, 100);

            // Data volume score
            var rowCount = await connection.QuerySingleOrDefaultAsync<long?>(@"
                SELECT SUM(p.rows) FROM sys.partitions p
                JOIN sys.objects o ON p.object_id = o.object_id
                JOIN sys.schemas s ON o.schema_id = s.schema_id
                WHERE s.name=@Schema AND o.name=@Name AND p.index_id IN (0,1)",
                new { Schema = schema, Name = objectName });
            score.DataVolumeScore = rowCount switch
            {
                > 10000000 => 100,
                > 1000000 => 80,
                > 100000 => 60,
                > 10000 => 40,
                > 1000 => 20,
                _ => 10
            };

            // PII score
            var piiCount = await connection.QuerySingleAsync<int>(@"
                SELECT COUNT(*) FROM sys.columns c
                JOIN sys.objects o ON c.object_id = o.object_id
                JOIN sys.schemas s ON o.schema_id = s.schema_id
                WHERE s.name=@Schema AND o.name=@Name
                  AND (c.name LIKE '%SSN%' OR c.name LIKE '%email%' OR c.name LIKE '%phone%')",
                new { Schema = schema, Name = objectName });
            score.PIIScore = Math.Min(piiCount * 20, 100);

            // Check MasterIndex
            score.HasMasterIndexEntry = await connection.QuerySingleAsync<bool>(@"
                SELECT CASE WHEN EXISTS(SELECT 1 FROM DaQa.MasterIndex WHERE SchemaName=@Schema AND ObjectName=@Name AND IsActive=1) THEN 1 ELSE 0 END",
                new { Schema = schema, Name = objectName });

            // Upsert score
            await connection.ExecuteAsync(@"
                MERGE DaQa.ObjectImportanceScores AS t
                USING (SELECT @Schema AS SchemaName, @Name AS ObjectName) AS s
                ON t.SchemaName = s.SchemaName AND t.ObjectName = s.ObjectName
                WHEN MATCHED THEN UPDATE SET
                    UsageScore=@UsageScore, DependencyScore=@DependencyScore, DataVolumeScore=@DataVolumeScore,
                    PIIScore=@PIIScore, HasMasterIndexEntry=@HasMasterIndex, LastCalculatedAt=GETUTCDATE()
                WHEN NOT MATCHED THEN INSERT (SchemaName, ObjectName, ObjectType, UsageScore, DependencyScore, DataVolumeScore, PIIScore, HasMasterIndexEntry)
                VALUES (@Schema, @Name, @ObjectType, @UsageScore, @DependencyScore, @DataVolumeScore, @PIIScore, @HasMasterIndex);",
                new { Schema = schema, Name = objectName, score.ObjectType, score.UsageScore, score.DependencyScore, score.DataVolumeScore, score.PIIScore, HasMasterIndex = score.HasMasterIndexEntry });

            score.LastCalculatedAt = DateTime.UtcNow;
            return score;
        });
    }

    public async Task RefreshUsageHeatmapAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Refreshing usage heatmap from DMVs");
        var stats = await _queryMiner.MineQueryPatternsAsync(30, ct);

        using var connection = new SqlConnection(_connectionString);
        foreach (var stat in stats)
        {
            await connection.ExecuteAsync(@"
                MERGE DaQa.UsageHeatmap AS t
                USING (SELECT @Schema AS SchemaName, @Name AS ObjectName) AS s
                ON t.SchemaName = s.SchemaName AND t.ObjectName = s.ObjectName
                WHEN MATCHED THEN UPDATE SET
                    ExecutionCount30d=@ExecCount, AvgCpuTimeMs=@CpuTime, UniqueUsers7d=@Users, LastRefreshedAt=GETUTCDATE()
                WHEN NOT MATCHED THEN INSERT (SchemaName, ObjectName, ObjectType, ExecutionCount30d, AvgCpuTimeMs, UniqueUsers7d)
                VALUES (@Schema, @Name, @Type, @ExecCount, @CpuTime, @Users);",
                new { Schema = stat.SchemaName, Name = stat.ObjectName, Type = stat.ObjectType,
                      ExecCount = stat.ExecutionCount30d, CpuTime = stat.AvgCpuTimeMs, Users = stat.UniqueUsers7d });
        }

        _logger.LogInformation("Usage heatmap refreshed with {Count} entries", stats.Count);
    }

    public async Task<List<PredictedGap>> PredictFutureGapsAsync(int daysAhead = 30, CancellationToken ct = default)
    {
        var predictions = new List<PredictedGap>();

        using var connection = new SqlConnection(_connectionString);

        // Predict stale documentation
        var staleRisks = await connection.QueryAsync<PredictedGap>(@"
            SELECT m.SchemaName, m.ObjectName, 'TABLE' AS ObjectType, 'STALE' AS PredictedGapType,
                   0.8 AS PredictionConfidence,
                   @DaysAhead - DATEDIFF(DAY, m.ModifiedDate, GETUTCDATE()) AS DaysUntilGap,
                   'HIGH' AS RecommendedPriority
            FROM DaQa.MasterIndex m
            JOIN sys.objects o ON m.ObjectName = o.name
            JOIN sys.schemas s ON o.schema_id = s.schema_id AND m.SchemaName = s.name
            WHERE m.IsActive = 1
              AND DATEDIFF(DAY, m.ModifiedDate, GETUTCDATE()) > 60
              AND o.modify_date > m.ModifiedDate",
            new { DaysAhead = daysAhead });

        predictions.AddRange(staleRisks);

        // TODO [7]: Add more prediction types (schema drift, usage spike predictions)

        return predictions;
    }

    #endregion

    #region Clustering

    public async Task<ClusteringResult> RunSemanticClusteringAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Starting semantic clustering");
        var result = new ClusteringResult { StartedAt = DateTime.UtcNow };

        using var connection = new SqlConnection(_connectionString);

        // Get all objects with descriptions
        var objects = await connection.QueryAsync<ObjectEmbedding>(@"
            SELECT s.name AS SchemaName, o.name AS ObjectName, o.type_desc AS ObjectType,
                   m.Description, CASE WHEN m.IndexId IS NOT NULL THEN 1 ELSE 0 END AS IsDocumented
            FROM sys.objects o
            JOIN sys.schemas s ON o.schema_id = s.schema_id
            LEFT JOIN DaQa.MasterIndex m ON s.name = m.SchemaName AND o.name = m.ObjectName AND m.IsActive = 1
            WHERE o.type IN ('U', 'V', 'P') AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')");

        var embeddings = new List<ObjectEmbedding>();
        foreach (var obj in objects.Take(500)) // Limit for performance
        {
            obj.Embedding = await _clusteringService.GenerateObjectEmbeddingAsync(obj.SchemaName, obj.ObjectName, obj.Description, ct);
            if (obj.Embedding != null && obj.Embedding.Length > 0)
                embeddings.Add(obj);
        }

        result.TotalObjects = embeddings.Count;

        if (embeddings.Count > 0)
        {
            var clusters = await _clusteringService.ClusterObjectsAsync(embeddings, 10, ct);
            result.ClustersCreated = clusters.Count;
            result.Clusters = clusters;

            // Save clusters to database
            foreach (var cluster in clusters)
            {
                var clusterId = await connection.QuerySingleAsync<int>(@"
                    INSERT INTO DaQa.SemanticClusters (ClusterName, DomainTag, MemberCount, DocumentedCount)
                    OUTPUT INSERTED.ClusterId
                    VALUES (@Name, @Domain, @Members, @Documented)",
                    new { Name = cluster.ClusterName, Domain = cluster.DomainTag, Members = cluster.MemberCount, Documented = cluster.DocumentedCount });

                foreach (var member in cluster.Members)
                {
                    await connection.ExecuteAsync(@"
                        INSERT INTO DaQa.ClusterMemberships (ClusterId, SchemaName, ObjectName, ObjectType, DistanceFromCentroid, IsOutlier, IsDocumented)
                        VALUES (@ClusterId, @Schema, @Name, @Type, @Distance, @IsOutlier, @IsDocumented)",
                        new { ClusterId = clusterId, Schema = member.SchemaName, Name = member.ObjectName, Type = member.ObjectType,
                              Distance = member.DistanceFromCentroid, member.IsOutlier, member.IsDocumented });
                }
            }
        }

        result.CompletedAt = DateTime.UtcNow;
        _logger.LogInformation("Clustering complete: {Clusters} clusters from {Objects} objects", result.ClustersCreated, result.TotalObjects);
        return result;
    }

    public async Task<List<ClusterGap>> FindClusterOutliersAsync(CancellationToken ct = default)
    {
        using var connection = new SqlConnection(_connectionString);

        var outliers = await connection.QueryAsync<ClusterGap>(@"
            SELECT cm.SchemaName, cm.ObjectName, cm.ObjectType, cm.ClusterId, sc.ClusterName,
                   sc.CoveragePercent AS ClusterCoverage, cm.DistanceFromCentroid,
                   0.85 AS Confidence
            FROM DaQa.ClusterMemberships cm
            JOIN DaQa.SemanticClusters sc ON cm.ClusterId = sc.ClusterId
            WHERE cm.IsDocumented = 0 AND sc.CoveragePercent >= 80
            ORDER BY sc.CoveragePercent DESC, cm.DistanceFromCentroid");

        return outliers.ToList();
    }

    #endregion

    #region Learning (RLHF)

    public async Task RecordFeedbackAsync(GapFeedback feedback, CancellationToken ct = default)
    {
        using var connection = new SqlConnection(_connectionString);

        await connection.ExecuteAsync(@"
            INSERT INTO DaQa.GapFeedback (SchemaName, ObjectName, PatternId, DetectedGapType, DetectedConfidence, FeedbackType, FeedbackBy, FeedbackReason)
            VALUES (@SchemaName, @ObjectName, @PatternId, @DetectedGapType, @DetectedConfidence, @FeedbackType, @FeedbackBy, @FeedbackReason)",
            feedback);

        // Update pattern precision
        if (feedback.PatternId.HasValue)
        {
            await UpdatePatternPrecisionAsync(feedback.PatternId.Value, ct);
        }

        // If rejected, update gap status
        if (feedback.FeedbackType == "REJECTED")
        {
            await connection.ExecuteAsync(@"
                UPDATE DaQa.DetectedGaps SET Status = 'REJECTED', ResolvedAt = GETUTCDATE()
                WHERE SchemaName = @SchemaName AND ObjectName = @ObjectName AND Status = 'OPEN'",
                new { feedback.SchemaName, feedback.ObjectName });
        }

        _logger.LogInformation("Recorded {FeedbackType} feedback for {Schema}.{Object}", feedback.FeedbackType, feedback.SchemaName, feedback.ObjectName);
    }

    public async Task UpdatePatternPrecisionAsync(int patternId, CancellationToken ct = default)
    {
        using var connection = new SqlConnection(_connectionString);

        // Calculate precision from feedback
        var stats = await connection.QuerySingleAsync<(int TP, int FP)>(@"
            SELECT
                SUM(CASE WHEN FeedbackType = 'CONFIRMED' THEN 1 ELSE 0 END) AS TP,
                SUM(CASE WHEN FeedbackType = 'REJECTED' THEN 1 ELSE 0 END) AS FP
            FROM DaQa.GapFeedback WHERE PatternId = @PatternId",
            new { PatternId = patternId });

        await connection.ExecuteAsync(@"
            UPDATE DaQa.GapPatterns SET TruePositives = @TP, FalsePositives = @FP, UpdatedAt = GETUTCDATE()
            WHERE PatternId = @PatternId",
            new { PatternId = patternId, stats.TP, stats.FP });

        // Auto-adjust threshold if enough data
        if (stats.TP + stats.FP >= 10)
        {
            var precision = (decimal)stats.TP / (stats.TP + stats.FP);
            decimal adjustment = precision < 0.5m ? 0.05m : precision > 0.8m ? -0.05m : 0;

            if (adjustment != 0)
            {
                await connection.ExecuteAsync(@"
                    UPDATE DaQa.GapPatterns
                    SET ConfidenceThreshold = CASE
                        WHEN ConfidenceThreshold + @Adj > 0.95 THEN 0.95
                        WHEN ConfidenceThreshold + @Adj < 0.5 THEN 0.5
                        ELSE ConfidenceThreshold + @Adj END
                    WHERE PatternId = @PatternId",
                    new { PatternId = patternId, Adj = adjustment });

                _logger.LogInformation("Adjusted confidence threshold for pattern {PatternId} by {Adjustment}", patternId, adjustment);
            }
        }
    }

    public async Task<List<GapPattern>> GetActivePatternsAsync(CancellationToken ct = default)
    {
        using var connection = new SqlConnection(_connectionString);
        var patterns = await connection.QueryAsync<GapPattern>(@"
            SELECT * FROM DaQa.GapPatterns WHERE IsActive = 1 ORDER BY PatternType, PatternName");
        return patterns.ToList();
    }

    #endregion

    #region Reporting

    public async Task<GapDashboardData> GetDashboardDataAsync(CancellationToken ct = default)
    {
        using var connection = new SqlConnection(_connectionString);
        var data = new GapDashboardData();

        // Gaps by severity
        var bySeverity = await connection.QueryAsync<(string Severity, int Count)>(@"
            SELECT Severity, COUNT(*) AS Count FROM DaQa.DetectedGaps WHERE Status = 'OPEN' GROUP BY Severity");
        data.GapsBySeverity = bySeverity.ToDictionary(x => x.Severity, x => x.Count);

        // Gaps by type
        var byType = await connection.QueryAsync<(string GapType, int Count)>(@"
            SELECT GapType, COUNT(*) AS Count FROM DaQa.DetectedGaps WHERE Status = 'OPEN' GROUP BY GapType");
        data.GapsByType = byType.ToDictionary(x => x.GapType, x => x.Count);

        // Top priority gaps
        data.TopPriorityGaps = (await connection.QueryAsync<DetectedGap>(@"
            SELECT TOP 10 * FROM DaQa.DetectedGaps WHERE Status = 'OPEN' ORDER BY Priority DESC")).ToList();

        // Coverage by schema
        data.CoverageBySchema = (await connection.QueryAsync<SchemaCoverage>(@"
            SELECT GroupName AS SchemaName, TotalObjects, DocumentedObjects FROM DaQa.DocumentationVelocity WHERE GroupType = 'SCHEMA'")).ToList();

        // Pattern effectiveness
        data.PatternEffectiveness = (await connection.QueryAsync<PatternEffectiveness>(@"
            SELECT p.PatternId, p.PatternName, p.Precision, p.TriggerCount,
                   (SELECT COUNT(*) FROM DaQa.DetectedGaps g WHERE g.PatternId = p.PatternId AND g.Status = 'OPEN') AS ActiveGaps
            FROM DaQa.GapPatterns p WHERE p.IsActive = 1")).ToList();

        // Predictions
        data.UpcomingPredictions = (await connection.QueryAsync<PredictedGap>(@"
            SELECT TOP 10 * FROM DaQa.PredictedGaps WHERE ExpiresAt > GETUTCDATE() ORDER BY PredictionConfidence DESC")).ToList();

        // Summary counts
        data.TotalOpenGaps = data.GapsBySeverity.Values.Sum();
        data.CriticalGaps = data.GapsBySeverity.GetValueOrDefault("CRITICAL", 0);
        data.HighPriorityGaps = data.GapsBySeverity.GetValueOrDefault("HIGH", 0);

        return data;
    }

    public async Task<DocumentationVelocity> GetVelocityMetricsAsync(string groupType, string groupName, CancellationToken ct = default)
    {
        using var connection = new SqlConnection(_connectionString);
        return await connection.QuerySingleOrDefaultAsync<DocumentationVelocity>(@"
            SELECT * FROM DaQa.DocumentationVelocity WHERE GroupType = @GroupType AND GroupName = @GroupName",
            new { GroupType = groupType, GroupName = groupName }) ?? new DocumentationVelocity { GroupType = groupType, GroupName = groupName };
    }

    #endregion
}

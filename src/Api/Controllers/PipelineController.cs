// ═══════════════════════════════════════════════════════════════════════════
// Pipeline Controller - END-TO-END VISIBILITY
// Complete pipeline status from Excel → Published
// ═══════════════════════════════════════════════════════════════════════════

using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;

namespace Enterprise.Documentation.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PipelineController : ControllerBase
{
    private readonly ILogger<PipelineController> _logger;
    private readonly string _connectionString;

    public PipelineController(ILogger<PipelineController> logger, IConfiguration config)
    {
        _logger = logger;
        _connectionString = config.GetConnectionString("DefaultConnection") 
            ?? throw new ArgumentNullException(nameof(config), "Connection string is required");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/pipeline/status
    // Complete pipeline overview with metrics and recent activity
    // ─────────────────────────────────────────────────────────────────────────
    [HttpGet("status")]
    public async Task<ActionResult<PipelineStatusDto>> GetPipelineStatus()
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Get stage counts
            var stageCountsSql = @"
                SELECT 
                    WorkflowStatus as Stage,
                    COUNT(*) as Count,
                    MAX(ModifiedDate) as LastActivity
                FROM DaQa.MasterIndex 
                WHERE WorkflowStatus IS NOT NULL
                GROUP BY WorkflowStatus";

            var stageCounts = await connection.QueryAsync<dynamic>(stageCountsSql);

            // Get metrics
            var metricsSql = @"
                SELECT 
                    COUNT(*) as TotalDocuments,
                    SUM(CASE WHEN WorkflowStatus = 'Staging' THEN 1 ELSE 0 END) as InStaging,
                    SUM(CASE WHEN WorkflowStatus = 'Draft' THEN 1 ELSE 0 END) as InDraft,
                    SUM(CASE WHEN WorkflowStatus = 'PendingApproval' THEN 1 ELSE 0 END) as PendingApproval,
                    SUM(CASE WHEN WorkflowStatus = 'Approved' THEN 1 ELSE 0 END) as Approved,
                    SUM(CASE WHEN WorkflowStatus = 'Published' THEN 1 ELSE 0 END) as Published,
                    SUM(CASE WHEN WorkflowStatus = 'Rejected' THEN 1 ELSE 0 END) as Rejected,
                    AVG(CAST(CompletenessScore as float)) as AvgCompletenessScore,
                    AVG(CAST(QualityScore as float)) as AvgQualityScore,
                    SUM(CASE WHEN PIIIndicator = 1 THEN 1 ELSE 0 END) as PiiDocuments
                FROM DaQa.MasterIndex";

            var metrics = await connection.QueryFirstAsync<dynamic>(metricsSql);

            // Get recent activity
            var activitySql = @"
                SELECT TOP 20
                    mi.IndexId,
                    mi.DocId,
                    mi.DocumentTitle,
                    mi.DocumentType,
                    mi.WorkflowStatus,
                    mi.SchemaName,
                    mi.TableName,
                    mi.ColumnName,
                    mi.JiraNumber,
                    mi.CreatedDate,
                    mi.ModifiedDate,
                    mi.CompletenessScore,
                    mi.QualityScore,
                    mi.PIIIndicator,
                    da.ApprovalStatus,
                    da.RequestedDate as ApprovalRequestedDate,
                    da.ApprovedDate,
                    da.ApprovedBy,
                    da.RejectionReason
                FROM DaQa.MasterIndex mi
                LEFT JOIN DaQa.DocumentApprovals da ON mi.DocId = da.DocumentId
                WHERE mi.ModifiedDate >= DATEADD(DAY, -1, GETDATE())
                ORDER BY mi.ModifiedDate DESC";

            var activity = await connection.QueryAsync<dynamic>(activitySql);

            // Get last Excel import (if tracking is implemented)
            ExcelImportStatusDto? lastImport = null;
            try
            {
                var importSql = @"
                    SELECT TOP 1
                        ImportId,
                        FileName,
                        ImportedDate,
                        RowsProcessed,
                        RowsSucceeded,
                        RowsFailed,
                        Status
                    FROM DaQa.ExcelImports
                    ORDER BY ImportedDate DESC";

                lastImport = await connection.QueryFirstOrDefaultAsync<ExcelImportStatusDto>(importSql);
            }
            catch
            {
                // Table might not exist yet - that's OK
            }

            var result = new PipelineStatusDto
            {
                StageCounts = stageCounts.Select(s => new StageCountDto
                {
                    Stage = s.Stage ?? "Unknown",
                    Count = s.Count,
                    LastActivity = s.LastActivity?.ToString("yyyy-MM-ddTHH:mm:ss")
                }).ToList(),
                
                RecentActivity = activity.Select(a => new PipelineActivityDto
                {
                    IndexId = a.IndexId,
                    DocId = a.DocId ?? "",
                    DocumentTitle = a.DocumentTitle,
                    DocumentType = a.DocumentType,
                    WorkflowStatus = a.WorkflowStatus,
                    SchemaName = a.SchemaName,
                    TableName = a.TableName,
                    ColumnName = a.ColumnName,
                    JiraNumber = a.JiraNumber,
                    CreatedDate = a.CreatedDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                    ModifiedDate = a.ModifiedDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                    CompletenessScore = a.CompletenessScore,
                    QualityScore = a.QualityScore,
                    PiiIndicator = a.PIIIndicator,
                    ApprovalStatus = a.ApprovalStatus,
                    ApprovalRequestedDate = a.ApprovalRequestedDate?.ToString("yyyy-MM-ddTHH:mm:ss"),
                    ApprovedDate = a.ApprovedDate?.ToString("yyyy-MM-ddTHH:mm:ss"),
                    ApprovedBy = a.ApprovedBy,
                    RejectionReason = a.RejectionReason
                }).ToList(),

                Metrics = new PipelineMetricsDto
                {
                    TotalDocuments = metrics.TotalDocuments,
                    InStaging = metrics.InStaging,
                    InDraft = metrics.InDraft,
                    PendingApproval = metrics.PendingApproval,
                    Approved = metrics.Approved,
                    Published = metrics.Published,
                    Rejected = metrics.Rejected,
                    AvgCompletenessScore = Math.Round(metrics.AvgCompletenessScore ?? 0, 2),
                    AvgQualityScore = Math.Round(metrics.AvgQualityScore ?? 0, 2),
                    PiiDocuments = metrics.PiiDocuments
                },

                LastExcelImport = lastImport,
                GeneratedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss")
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pipeline status");
            return StatusCode(500, new { error = "Failed to retrieve pipeline status" });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/pipeline/stages
    // Documents grouped by stage for swimlane view
    // ─────────────────────────────────────────────────────────────────────────
    [HttpGet("stages")]
    public async Task<ActionResult<List<PipelineStageDataDto>>> GetPipelineStages()
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var stagesSql = @"
                SELECT 
                    mi.WorkflowStatus,
                    mi.IndexId,
                    mi.DocId,
                    mi.DocumentTitle,
                    mi.DocumentType,
                    COALESCE(mi.SchemaName + '.' + mi.TableName + 
                        CASE WHEN mi.ColumnName IS NOT NULL THEN '.' + mi.ColumnName ELSE '' END, 
                        mi.DocumentTitle) as ObjectName,
                    mi.JiraNumber,
                    mi.ModifiedDate,
                    mi.CompletenessScore,
                    mi.PIIIndicator
                FROM DaQa.MasterIndex mi
                WHERE mi.WorkflowStatus IS NOT NULL
                ORDER BY mi.WorkflowStatus, mi.ModifiedDate DESC";

            var items = await connection.QueryAsync<dynamic>(stagesSql);

            var stages = new[]
            {
                new { Name = "Staging", Display = "Staging", Color = "#6b7280" },
                new { Name = "Draft", Display = "Draft", Color = "#3b82f6" },
                new { Name = "PendingApproval", Display = "Pending Approval", Color = "#f59e0b" },
                new { Name = "Approved", Display = "Approved", Color = "#22c55e" },
                new { Name = "Rejected", Display = "Rejected", Color = "#ef4444" },
                new { Name = "Published", Display = "Published", Color = "#14b8a6" }
            };

            var result = stages.Select(stage =>
            {
                var stageItems = items.Where(i => i.WorkflowStatus == stage.Name).ToList();
                
                return new PipelineStageDataDto
                {
                    StageName = stage.Name,
                    DisplayName = stage.Display,
                    Count = stageItems.Count,
                    Color = stage.Color,
                    Items = stageItems.Select(item => new PipelineStageItemDto
                    {
                        IndexId = item.IndexId,
                        DocId = item.DocId ?? "",
                        DocumentTitle = item.DocumentTitle,
                        DocumentType = item.DocumentType,
                        ObjectName = item.ObjectName,
                        JiraNumber = item.JiraNumber,
                        ModifiedDate = item.ModifiedDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                        CompletenessScore = item.CompletenessScore,
                        PiiIndicator = item.PIIIndicator
                    }).ToList()
                };
            }).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pipeline stages");
            return StatusCode(500, new { error = "Failed to retrieve pipeline stages" });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/pipeline/active
    // All active (non-published) items in pipeline
    // ─────────────────────────────────────────────────────────────────────────
    [HttpGet("active")]
    public async Task<ActionResult<List<ActivePipelineItemDto>>> GetActiveItems()
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT 
                    mi.IndexId,
                    mi.DocId,
                    mi.DocumentTitle,
                    mi.DocumentType,
                    COALESCE(mi.SchemaName + '.' + mi.TableName, mi.DocumentTitle) as ObjectPath,
                    mi.WorkflowStatus,
                    mi.JiraNumber,
                    mi.CreatedDate,
                    mi.ModifiedDate,
                    mi.CompletenessScore,
                    mi.PIIIndicator,
                    da.ApprovalStatus,
                    da.RequestedBy,
                    da.RequestedDate,
                    DATEDIFF(MINUTE, mi.CreatedDate, GETDATE()) as AgeMinutes
                FROM DaQa.MasterIndex mi
                LEFT JOIN DaQa.DocumentApprovals da ON mi.DocId = da.DocumentId 
                    AND da.ApprovalStatus = 'Pending'
                WHERE mi.WorkflowStatus NOT IN ('Published', 'Archived')
                    OR mi.WorkflowStatus IS NULL
                ORDER BY mi.ModifiedDate DESC";

            var items = await connection.QueryAsync<dynamic>(sql);

            var result = items.Select(item => new ActivePipelineItemDto
            {
                IndexId = item.IndexId,
                DocId = item.DocId ?? "",
                DocumentTitle = item.DocumentTitle,
                DocumentType = item.DocumentType,
                ObjectPath = item.ObjectPath,
                WorkflowStatus = item.WorkflowStatus,
                JiraNumber = item.JiraNumber,
                CreatedDate = item.CreatedDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                ModifiedDate = item.ModifiedDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                CompletenessScore = item.CompletenessScore,
                PiiIndicator = item.PIIIndicator,
                ApprovalStatus = item.ApprovalStatus,
                RequestedBy = item.RequestedBy,
                RequestedDate = item.RequestedDate?.ToString("yyyy-MM-ddTHH:mm:ss"),
                AgeMinutes = item.AgeMinutes
            }).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active pipeline items");
            return StatusCode(500, new { error = "Failed to retrieve active pipeline items" });
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// DTOs
// ═══════════════════════════════════════════════════════════════════════════

public class PipelineStatusDto
{
    public List<StageCountDto> StageCounts { get; set; } = new();
    public List<PipelineActivityDto> RecentActivity { get; set; } = new();
    public PipelineMetricsDto Metrics { get; set; } = new();
    public ExcelImportStatusDto? LastExcelImport { get; set; }
    public string GeneratedAt { get; set; } = "";
}

public class StageCountDto
{
    public string Stage { get; set; } = "";
    public int Count { get; set; }
    public string? LastActivity { get; set; }
}

public class PipelineMetricsDto
{
    public int TotalDocuments { get; set; }
    public int InStaging { get; set; }
    public int InDraft { get; set; }
    public int PendingApproval { get; set; }
    public int Approved { get; set; }
    public int Published { get; set; }
    public int Rejected { get; set; }
    public double AvgCompletenessScore { get; set; }
    public double AvgQualityScore { get; set; }
    public int PiiDocuments { get; set; }
}

public class PipelineActivityDto
{
    public int IndexId { get; set; }
    public string DocId { get; set; } = "";
    public string? DocumentTitle { get; set; }
    public string? DocumentType { get; set; }
    public string? WorkflowStatus { get; set; }
    public string? SchemaName { get; set; }
    public string? TableName { get; set; }
    public string? ColumnName { get; set; }
    public string? JiraNumber { get; set; }
    public string CreatedDate { get; set; } = "";
    public string ModifiedDate { get; set; } = "";
    public decimal? CompletenessScore { get; set; }
    public decimal? QualityScore { get; set; }
    public bool PiiIndicator { get; set; }
    public string? ApprovalStatus { get; set; }
    public string? ApprovalRequestedDate { get; set; }
    public string? ApprovedDate { get; set; }
    public string? ApprovedBy { get; set; }
    public string? RejectionReason { get; set; }
}

public class ExcelImportStatusDto
{
    public int ImportId { get; set; }
    public string? FileName { get; set; }
    public string ImportedDate { get; set; } = "";
    public int RowsProcessed { get; set; }
    public int RowsSucceeded { get; set; }
    public int RowsFailed { get; set; }
    public string? Status { get; set; }
}

public class PipelineStageDataDto
{
    public string StageName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Count { get; set; }
    public List<PipelineStageItemDto> Items { get; set; } = new();
    public string Color { get; set; } = "";
}

public class PipelineStageItemDto
{
    public int IndexId { get; set; }
    public string DocId { get; set; } = "";
    public string? DocumentTitle { get; set; }
    public string? DocumentType { get; set; }
    public string? ObjectName { get; set; }
    public string? JiraNumber { get; set; }
    public string ModifiedDate { get; set; } = "";
    public decimal? CompletenessScore { get; set; }
    public bool PiiIndicator { get; set; }
}

public class ActivePipelineItemDto
{
    public int IndexId { get; set; }
    public string DocId { get; set; } = "";
    public string? DocumentTitle { get; set; }
    public string? DocumentType { get; set; }
    public string? ObjectPath { get; set; }
    public string? WorkflowStatus { get; set; }
    public string? JiraNumber { get; set; }
    public string CreatedDate { get; set; } = "";
    public string ModifiedDate { get; set; } = "";
    public decimal? CompletenessScore { get; set; }
    public bool PiiIndicator { get; set; }
    public string? ApprovalStatus { get; set; }
    public string? RequestedBy { get; set; }
    public string? RequestedDate { get; set; }
    public int AgeMinutes { get; set; }
}
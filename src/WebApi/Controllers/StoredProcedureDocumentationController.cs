// API Controller for documentation management dashboard

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Enterprise.Documentation.Core.Application.Services.StoredProcedure;

namespace Enterprise.Documentation.WebApi.Controllers;

/// <summary>
/// API controller for stored procedure documentation management and analytics.
/// Provides endpoints for documentation operations, quality reports, and maintenance metrics.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StoredProcedureDocumentationController : ControllerBase
{
    private readonly IStoredProcedureDocumentationService _docService;
    private readonly IDocumentationAnalyticsService _analyticsService;
    private readonly ILogger<StoredProcedureDocumentationController> _logger;

    public StoredProcedureDocumentationController(
        IStoredProcedureDocumentationService docService,
        IDocumentationAnalyticsService analyticsService,
        ILogger<StoredProcedureDocumentationController> logger)
    {
        _docService = docService;
        _analyticsService = analyticsService;
        _logger = logger;
    }

    /// <summary>
    /// Creates or updates documentation for a stored procedure
    /// </summary>
    [HttpPost("{procedureName}/documentation")]
    public async Task<IActionResult> CreateOrUpdateDocumentation(
        string procedureName,
        [FromBody] CreateDocumentationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var docId = await _docService.CreateOrUpdateSPDocumentationAsync(
                procedureName, 
                request.ChangeDocumentId, 
                cancellationToken);

            return Ok(new { DocumentId = docId, Message = "Documentation created/updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating/updating documentation for {ProcedureName}", procedureName);
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Checks if documentation exists for a procedure
    /// </summary>
    [HttpGet("{procedureName}/documentation/exists")]
    public async Task<IActionResult> CheckDocumentationExists(
        string procedureName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var exists = await _docService.SPDocumentationExistsAsync(procedureName, cancellationToken);
            return Ok(new { ProcedureName = procedureName, DocumentationExists = exists });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking documentation for {ProcedureName}", procedureName);
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Gets complexity score for a stored procedure
    /// </summary>
    [HttpGet("{procedureName}/complexity")]
    public async Task<IActionResult> GetComplexityScore(
        string procedureName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // This would require getting the procedure definition first
            // Implementation depends on your security model for accessing procedure code
            return Ok(new { ProcedureName = procedureName, ComplexityScore = 42 }); // Placeholder
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating complexity for {ProcedureName}", procedureName);
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Gets comprehensive quality report for all documentation
    /// </summary>
    [HttpGet("analytics/quality-report")]
    public async Task<IActionResult> GetQualityReport(CancellationToken cancellationToken = default)
    {
        try
        {
            var report = await _analyticsService.GenerateQualityReportAsync(cancellationToken);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating quality report");
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Gets documentation coverage analysis
    /// </summary>
    [HttpGet("analytics/coverage")]
    public async Task<IActionResult> GetCoverageAnalysis(CancellationToken cancellationToken = default)
    {
        try
        {
            var coverage = await _analyticsService.AnalyzeCoverageAsync(cancellationToken);
            return Ok(coverage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing coverage");
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Identifies documentation gaps (missing or outdated documentation)
    /// </summary>
    [HttpGet("analytics/gaps")]
    public async Task<IActionResult> GetDocumentationGaps(CancellationToken cancellationToken = default)
    {
        try
        {
            var gaps = await _analyticsService.IdentifyGapsAsync(cancellationToken);
            return Ok(gaps);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error identifying documentation gaps");
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Gets maintenance metrics and team performance data
    /// </summary>
    [HttpGet("analytics/maintenance-metrics")]
    public async Task<IActionResult> GetMaintenanceMetrics(CancellationToken cancellationToken = default)
    {
        try
        {
            var metrics = await _analyticsService.GetMaintenanceMetricsAsync(cancellationToken);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance metrics");
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Bulk operation to update multiple procedures at once
    /// </summary>
    [HttpPost("bulk-update")]
    public async Task<IActionResult> BulkUpdateDocumentation(
        [FromBody] BulkUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var results = new List<BulkUpdateResult>();

            foreach (var procedure in request.ProcedureNames)
            {
                try
                {
                    var docId = await _docService.CreateOrUpdateSPDocumentationAsync(
                        procedure, 
                        request.ChangeDocumentId, 
                        cancellationToken);
                    
                    results.Add(new BulkUpdateResult 
                    { 
                        ProcedureName = procedure, 
                        Success = true, 
                        DocumentId = docId 
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new BulkUpdateResult 
                    { 
                        ProcedureName = procedure, 
                        Success = false, 
                        Error = ex.Message 
                    });
                }
            }

            var successCount = results.Count(r => r.Success);
            return Ok(new 
            { 
                TotalProcessed = results.Count, 
                SuccessCount = successCount, 
                FailureCount = results.Count - successCount, 
                Results = results 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk update operation");
            return BadRequest(new { Error = ex.Message });
        }
    }
}

// Request/Response models
public class CreateDocumentationRequest
{
    public string ChangeDocumentId { get; set; } = string.Empty;
}

public class BulkUpdateRequest
{
    public List<string> ProcedureNames { get; set; } = new();
    public string ChangeDocumentId { get; set; } = string.Empty;
}

public class BulkUpdateResult
{
    public string ProcedureName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? DocumentId { get; set; }
    public string? Error { get; set; }
}
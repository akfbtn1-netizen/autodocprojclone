using Microsoft.AspNetCore.Mvc;
using Enterprise.Documentation.Core.Application.Services.Workflow;

namespace Enterprise.Documentation.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkflowController : ControllerBase
{
    private readonly IWorkflowEventService _workflowEventService;
    private readonly ILogger<WorkflowController> _logger;

    public WorkflowController(
        IWorkflowEventService workflowEventService,
        ILogger<WorkflowController> logger)
    {
        _workflowEventService = workflowEventService;
        _logger = logger;
    }

    /// <summary>
    /// Get recent workflow events
    /// </summary>
    /// <param name="limit">Maximum number of events to return (default: 50)</param>
    /// <returns>List of workflow events ordered by timestamp (most recent first)</returns>
    [HttpGet("events")]
    public async Task<IActionResult> GetEvents([FromQuery] int limit = 50)
    {
        try
        {
            _logger.LogInformation("Retrieving {Limit} workflow events", limit);
            
            // Validate limit parameter
            if (limit <= 0 || limit > 1000)
            {
                return BadRequest("Limit must be between 1 and 1000");
            }
            
            var events = await _workflowEventService.GetEventsAsync(limit);
            
            _logger.LogInformation("Successfully retrieved {Count} workflow events", events.Count);
            
            return Ok(events);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflow events");
            return StatusCode(500, new { error = "Internal server error retrieving workflow events" });
        }
    }

    /// <summary>
    /// Get workflow events statistics
    /// </summary>
    /// <returns>Summary statistics for workflow events</returns>
    [HttpGet("stats")]
    public async Task<IActionResult> GetWorkflowStats()
    {
        try
        {
            _logger.LogInformation("Retrieving workflow statistics");
            
            var recentEvents = await _workflowEventService.GetEventsAsync(100);
            
            var stats = new
            {
                TotalEvents = recentEvents.Count,
                CompletedWorkflows = recentEvents.Count(e => e.EventType == WorkflowEventType.WorkflowCompleted && e.Status == WorkflowEventStatus.Completed),
                FailedWorkflows = recentEvents.Count(e => e.Status == WorkflowEventStatus.Failed),
                InProgressWorkflows = recentEvents.Count(e => e.Status == WorkflowEventStatus.InProgress),
                DocumentApprovals = recentEvents.Count(e => e.EventType == WorkflowEventType.DocumentApproved),
                DocumentRejections = recentEvents.Count(e => e.EventType == WorkflowEventType.DocumentRejected),
                LastEventTimestamp = recentEvents.FirstOrDefault()?.Timestamp
            };
            
            _logger.LogInformation("Successfully calculated workflow statistics");
            
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating workflow statistics");
            return StatusCode(500, new { error = "Internal server error calculating workflow statistics" });
        }
    }
}
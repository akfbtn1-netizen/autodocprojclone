using Microsoft.AspNetCore.Mvc;
using Enterprise.Documentation.Core.Application.Interfaces;

namespace Enterprise.Documentation.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(ILogger<DashboardController> logger)
        {
            _logger = logger;
        }

        [HttpGet("kpis")]
        public async Task<ActionResult<object>> GetKpis()
        {
            try
            {
                // Return mock KPIs for now
                var kpis = new
                {
                    totalDocuments = 156,
                    pendingApprovals = 23,
                    completedThisMonth = 45,
                    activeUsers = 12
                };

                return Ok(kpis);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving KPIs");
                return StatusCode(500, new { error = "Failed to retrieve KPIs" });
            }
        }

        [HttpGet("activity")]
        public async Task<ActionResult<object>> GetActivity([FromQuery] int limit = 10)
        {
            try
            {
                // Return mock activity data
                var activities = new[]
                {
                    new { id = 1, type = "document_created", user = "John Doe", timestamp = DateTime.UtcNow.AddHours(-1), description = "Created document DOC-2026-001" },
                    new { id = 2, type = "approval_completed", user = "Jane Smith", timestamp = DateTime.UtcNow.AddHours(-2), description = "Approved document DOC-2026-002" },
                    new { id = 3, type = "document_updated", user = "Bob Johnson", timestamp = DateTime.UtcNow.AddHours(-3), description = "Updated document DOC-2026-003" }
                }.Take(limit);

                return Ok(activities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving activity");
                return StatusCode(500, new { error = "Failed to retrieve activity" });
            }
        }
    }
}
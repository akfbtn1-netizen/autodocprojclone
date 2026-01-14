using Microsoft.AspNetCore.Mvc;

namespace Enterprise.Documentation.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AgentsController : ControllerBase
    {
        private readonly ILogger<AgentsController> _logger;

        public AgentsController(ILogger<AgentsController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<object>> GetAgents()
        {
            try
            {
                // Return mock agents data
                var agents = new[]
                {
                    new { id = "agent-001", name = "Document Processor", status = "active", lastSeen = DateTime.UtcNow.AddMinutes(-5) },
                    new { id = "agent-002", name = "Approval Workflow", status = "active", lastSeen = DateTime.UtcNow.AddMinutes(-2) },
                    new { id = "agent-003", name = "Quality Checker", status = "idle", lastSeen = DateTime.UtcNow.AddMinutes(-10) }
                };

                return Ok(agents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving agents");
                return StatusCode(500, new { error = "Failed to retrieve agents" });
            }
        }

        [HttpGet("health")]
        public async Task<ActionResult<object>> GetAgentsHealth()
        {
            try
            {
                // Return mock health data
                var health = new
                {
                    status = "healthy",
                    totalAgents = 3,
                    activeAgents = 2,
                    idleAgents = 1,
                    lastCheck = DateTime.UtcNow
                };

                return Ok(health);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving agents health");
                return StatusCode(500, new { error = "Failed to retrieve agents health" });
            }
        }
    }
}
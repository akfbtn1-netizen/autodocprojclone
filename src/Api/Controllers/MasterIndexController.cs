// TODO [7]: Add GET /api/masterindex/gaps - undocumented objects detection
// TODO [7]: Add GET /api/masterindex/stale - stale documentation detection
// TODO [7]: Add GET /api/masterindex/coverage - coverage dashboard stats
// TODO [7]: Query DMVs (sys.dm_exec_query_stats) for priority scoring
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Enterprise.Documentation.Core.Application.Interfaces;
using Enterprise.Documentation.Shared.Contracts.DTOs;

namespace Enterprise.Documentation.Api.Controllers;

/// <summary>
/// API endpoints for MasterIndex document catalog operations.
/// Returns real data from IRFS1.DaQa.MasterIndex table.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class MasterIndexController : ControllerBase
{
    private readonly IMasterIndexRepository _repository;
    private readonly ILogger<MasterIndexController> _logger;

    public MasterIndexController(
        IMasterIndexRepository repository,
        ILogger<MasterIndexController> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets a paginated list of all MasterIndex entries.
    /// </summary>
    /// <param name="pageNumber">Page number (1-based, default: 1)</param>
    /// <param name="pageSize">Items per page (default: 20, max: 100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of document summaries</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<MasterIndexSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PaginatedResponse<MasterIndexSummaryDto>>> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (pageSize > 100) pageSize = 100;
            if (pageNumber < 1) pageNumber = 1;

            var entities = await _repository.GetAllAsync(pageNumber, pageSize, cancellationToken);
            var totalCount = await _repository.GetTotalCountAsync(cancellationToken);

            var dtos = entities.Select(MapToSummaryDto).ToList();

            var response = new PaginatedResponse<MasterIndexSummaryDto>
            {
                Items = dtos,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };

            _logger.LogInformation(
                "Retrieved {Count} MasterIndex entries (page {Page} of {TotalPages})",
                dtos.Count, pageNumber, response.TotalPages);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving MasterIndex entries");
            return StatusCode(500, new { error = "An error occurred while retrieving entries" });
        }
    }

    /// <summary>
    /// Gets a single MasterIndex entry by ID.
    /// </summary>
    /// <param name="id">The IndexId to retrieve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detailed MasterIndex entry</returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(MasterIndexDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MasterIndexDetailDto>> GetById(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _repository.GetByIdAsync(id);

            if (entity == null)
            {
                _logger.LogWarning("MasterIndex entry not found: {IndexId}", id);
                return NotFound(new { error = $"Entry with ID {id} not found" });
            }

            var dto = MapToDetailDto(entity);
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving MasterIndex entry {IndexId}", id);
            return StatusCode(500, new { error = "An error occurred while retrieving the entry" });
        }
    }

    /// <summary>
    /// Gets MasterIndex entries filtered by approval status.
    /// </summary>
    /// <param name="status">Approval status (Draft, Pending, Approved, Rejected)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of entries with the specified status</returns>
    [HttpGet("by-status/{status}")]
    [ProducesResponseType(typeof(List<MasterIndexSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<MasterIndexSummaryDto>>> GetByApprovalStatus(
        string status,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entities = await _repository.GetByApprovalStatusAsync(status, cancellationToken);
            var dtos = entities.Select(MapToSummaryDto).ToList();

            _logger.LogInformation(
                "Retrieved {Count} MasterIndex entries with status: {Status}",
                dtos.Count, status);

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving entries by status: {Status}", status);
            return StatusCode(500, new { error = "An error occurred while retrieving entries" });
        }
    }

    /// <summary>
    /// Gets document statistics for dashboard display.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Aggregated statistics</returns>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(MasterIndexStatisticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MasterIndexStatisticsDto>> GetStatistics(
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get all entries for statistics calculation
            var allEntries = await _repository.GetAllAsync(1, 10000, cancellationToken);
            var entriesList = allEntries.ToList();

            var stats = new MasterIndexStatisticsDto
            {
                TotalDocuments = entriesList.Count,
                DraftCount = entriesList.Count(e => e.ApprovalStatus == "Draft"),
                PendingCount = entriesList.Count(e => e.ApprovalStatus == "Pending"),
                ApprovedCount = entriesList.Count(e => e.ApprovalStatus == "Approved"),
                RejectedCount = entriesList.Count(e => e.ApprovalStatus == "Rejected"),
                ByCategory = entriesList
                    .Where(e => !string.IsNullOrEmpty(e.Category))
                    .GroupBy(e => e.Category!)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ByObjectType = entriesList
                    .Where(e => !string.IsNullOrEmpty(e.ObjectType))
                    .GroupBy(e => e.ObjectType!)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ByDatabase = entriesList
                    .Where(e => !string.IsNullOrEmpty(e.DatabaseName))
                    .GroupBy(e => e.DatabaseName!)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ByTier = entriesList
                    .Where(e => !string.IsNullOrEmpty(e.Tier))
                    .GroupBy(e => e.Tier!)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ComputedAt = DateTime.UtcNow
            };

            _logger.LogInformation("Computed statistics for {Count} MasterIndex entries", entriesList.Count);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing statistics");
            return StatusCode(500, new { error = "An error occurred while computing statistics" });
        }
    }

    /// <summary>
    /// Searches MasterIndex entries by text query.
    /// </summary>
    /// <param name="query">Search query (searches across multiple fields)</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Items per page (max: 100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated search results</returns>
    [HttpGet("search")]
    [ProducesResponseType(typeof(PaginatedResponse<MasterIndexSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PaginatedResponse<MasterIndexSummaryDto>>> Search(
        [FromQuery] string query,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(new { error = "Search query cannot be empty" });
        }

        try
        {
            if (pageSize > 100) pageSize = 100;
            if (pageNumber < 1) pageNumber = 1;

            var entities = await _repository.SearchAsync(query, pageNumber, pageSize, cancellationToken);
            var dtos = entities.Select(MapToSummaryDto).ToList();

            // For search, we estimate total based on page results
            var response = new PaginatedResponse<MasterIndexSummaryDto>
            {
                Items = dtos,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = dtos.Count < pageSize ? (pageNumber - 1) * pageSize + dtos.Count : -1,
                TotalPages = dtos.Count < pageSize ? pageNumber : -1
            };

            _logger.LogInformation("Search '{Query}' returned {Count} results", query, dtos.Count);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for: {Query}", query);
            return StatusCode(500, new { error = "An error occurred while searching" });
        }
    }

    /// <summary>
    /// Gets MasterIndex entries by database name.
    /// </summary>
    /// <param name="databaseName">Database name to filter by</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of entries in the specified database</returns>
    [HttpGet("by-database/{databaseName}")]
    [ProducesResponseType(typeof(List<MasterIndexSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<MasterIndexSummaryDto>>> GetByDatabase(
        string databaseName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entities = await _repository.GetByDatabaseAsync(databaseName, cancellationToken);
            var dtos = entities.Select(MapToSummaryDto).ToList();

            _logger.LogInformation(
                "Retrieved {Count} MasterIndex entries for database: {Database}",
                dtos.Count, databaseName);

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving entries for database: {Database}", databaseName);
            return StatusCode(500, new { error = "An error occurred while retrieving entries" });
        }
    }

    /// <summary>
    /// Gets MasterIndex entries by tier level.
    /// </summary>
    /// <param name="tier">Tier level (1=Complex, 2=Standard, 3=Simple)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of entries for the specified tier</returns>
    [HttpGet("by-tier/{tier:int}")]
    [ProducesResponseType(typeof(List<MasterIndexSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<MasterIndexSummaryDto>>> GetByTier(
        int tier,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entities = await _repository.GetByTierAsync(tier);
            var dtos = entities.Select(MapToSummaryDto).ToList();

            _logger.LogInformation(
                "Retrieved {Count} MasterIndex entries for tier: {Tier}",
                dtos.Count, tier);

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving entries for tier: {Tier}", tier);
            return StatusCode(500, new { error = "An error occurred while retrieving entries" });
        }
    }

    #region Private Mapping Methods

    private static MasterIndexSummaryDto MapToSummaryDto(Core.Domain.Entities.MasterIndex entity)
    {
        return new MasterIndexSummaryDto
        {
            IndexId = entity.IndexId,
            DatabaseName = entity.DatabaseName,
            SchemaName = entity.SchemaName,
            TableName = entity.PhysicalName,
            ColumnName = entity.ColumnName,
            ObjectType = entity.ObjectType,
            ApprovalStatus = entity.ApprovalStatus,
            WorkflowStatus = entity.DocumentStatus,
            LastModifiedDate = entity.ModifiedDate,
            Category = entity.Category,
            Tier = entity.Tier,
            ObjectPath = entity.ObjectPath
        };
    }

    private static MasterIndexDetailDto MapToDetailDto(Core.Domain.Entities.MasterIndex entity)
    {
        return new MasterIndexDetailDto
        {
            IndexId = entity.IndexId,
            PhysicalName = entity.PhysicalName,
            LogicalName = entity.LogicalName,
            SchemaName = entity.SchemaName,
            DatabaseName = entity.DatabaseName,
            ServerName = entity.ServerName,
            ObjectType = entity.ObjectType,
            ColumnName = entity.ColumnName,
            Category = entity.Category,
            SubCategory = entity.SubCategory,
            BusinessDomain = entity.BusinessDomain,
            Tier = entity.Tier,
            Tags = entity.Tags?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
            Description = entity.Description,
            TechnicalSummary = entity.TechnicalSummary,
            BusinessPurpose = entity.BusinessPurpose,
            UsageNotes = entity.UsageNotes,
            GeneratedDocPath = entity.GeneratedDocPath,
            GeneratedDate = entity.GeneratedDate,
            ApprovalStatus = entity.ApprovalStatus,
            ApprovedBy = entity.ApprovedBy,
            ApprovedDate = entity.ApprovedDate,
            DocumentVersion = entity.DocumentVersion,
            DataType = entity.DataType,
            MaxLength = entity.MaxLength,
            IsNullable = entity.IsNullable,
            DefaultValue = entity.DefaultValue,
            PIIIndicator = entity.PIIIndicator,
            DataClassification = entity.DataClassification,
            SecurityLevel = entity.SecurityLevel,
            CreatedDate = entity.CreatedDate,
            CreatedBy = entity.CreatedBy,
            ModifiedDate = entity.ModifiedDate,
            ModifiedBy = entity.ModifiedBy,
            IsActive = entity.IsActive
        };
    }

    #endregion
}

// TODO [6]: Implement ISearchOrchestrator with Qdrant vector DB
// TODO [6]: Implement IGraphSearchService for GraphRAG queries
// TODO [6]: Implement IContinuousLearner for feedback loop
// TODO [6]: Wire hybrid search (semantic + BM25 keyword)
// TODO [6]: Requires Docker for Qdrant container
using Enterprise.Documentation.Core.Application.DTOs.Search;
using Enterprise.Documentation.Core.Application.Interfaces.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Enterprise.Documentation.Api.Controllers;

/// <summary>
/// Smart Search API endpoints with 5-path routing architecture.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SearchController : ControllerBase
{
    private readonly ISearchOrchestrator _searchOrchestrator;
    private readonly IGraphSearchService _graphSearch;
    private readonly IContinuousLearner _learner;
    private readonly IResultsExporter _exporter;
    private readonly ILogger<SearchController> _logger;

    public SearchController(
        ISearchOrchestrator searchOrchestrator,
        IGraphSearchService graphSearch,
        IContinuousLearner learner,
        IResultsExporter exporter,
        ILogger<SearchController> logger)
    {
        _searchOrchestrator = searchOrchestrator;
        _graphSearch = graphSearch;
        _learner = learner;
        _exporter = exporter;
        _logger = logger;
    }

    /// <summary>
    /// Execute a search query with automatic routing to optimal path.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SearchResponse>> Search(
        [FromBody] SearchRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest("Query is required");
        }

        var userId = User.FindFirst("sub")?.Value ?? "anonymous";

        var searchRequest = new SearchRequest(
            request.Query,
            userId,
            new SearchOptions(
                MaxResults: request.MaxResults ?? 20,
                IncludeLineage: request.IncludeLineage ?? false,
                IncludePiiFlows: request.IncludePiiFlows ?? false,
                EnableReranking: request.EnableReranking ?? true,
                MinConfidence: request.MinConfidence ?? 0.5m,
                FilterDatabases: request.FilterDatabases,
                FilterObjectTypes: request.FilterObjectTypes,
                FilterCategories: request.FilterCategories,
                ForceRoutingPath: null));

        var response = await _searchOrchestrator.SearchAsync(searchRequest, cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Get search suggestions based on partial input.
    /// </summary>
    [HttpGet("suggestions")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<string>>> GetSuggestions(
        [FromQuery] string query,
        [FromQuery] int maxSuggestions = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        {
            return Ok(new List<string>());
        }

        var suggestions = await _searchOrchestrator.GetSuggestionsAsync(
            query, maxSuggestions, cancellationToken);

        return Ok(suggestions);
    }

    /// <summary>
    /// Get follow-up suggestions for a previous search.
    /// </summary>
    [HttpGet("{queryId:guid}/follow-ups")]
    [ProducesResponseType(typeof(List<FollowUpSuggestion>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<FollowUpSuggestion>>> GetFollowUpSuggestions(
        Guid queryId,
        CancellationToken cancellationToken)
    {
        var suggestions = await _searchOrchestrator.GetFollowUpSuggestionsAsync(
            queryId, cancellationToken);

        return Ok(suggestions);
    }

    /// <summary>
    /// Find all objects that depend on the specified object (downstream lineage).
    /// </summary>
    [HttpGet("lineage/{nodeId}/dependents")]
    [ProducesResponseType(typeof(List<GraphSearchResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<GraphSearchResult>>> GetDependents(
        string nodeId,
        [FromQuery] int maxDepth = 3,
        CancellationToken cancellationToken = default)
    {
        var results = await _graphSearch.FindDependentsAsync(nodeId, maxDepth, cancellationToken);
        return Ok(results);
    }

    /// <summary>
    /// Find all objects that the specified object depends on (upstream lineage).
    /// </summary>
    [HttpGet("lineage/{nodeId}/dependencies")]
    [ProducesResponseType(typeof(List<GraphSearchResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<GraphSearchResult>>> GetDependencies(
        string nodeId,
        [FromQuery] int maxDepth = 3,
        CancellationToken cancellationToken = default)
    {
        var results = await _graphSearch.FindDependenciesAsync(nodeId, maxDepth, cancellationToken);
        return Ok(results);
    }

    /// <summary>
    /// Find the lineage path between two objects.
    /// </summary>
    [HttpGet("lineage/path")]
    [ProducesResponseType(typeof(List<GraphSearchResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<GraphSearchResult>>> GetLineagePath(
        [FromQuery] string sourceId,
        [FromQuery] string targetId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(targetId))
        {
            return BadRequest("Both sourceId and targetId are required");
        }

        var path = await _graphSearch.FindLineagePathAsync(sourceId, targetId, cancellationToken);
        return Ok(path);
    }

    /// <summary>
    /// Trace PII data flow paths from a source column.
    /// </summary>
    [HttpGet("pii-flow/{sourceNodeId}")]
    [ProducesResponseType(typeof(List<PiiFlowPath>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PiiFlowPath>>> TracePiiFlow(
        string sourceNodeId,
        CancellationToken cancellationToken)
    {
        var flows = await _graphSearch.TracePiiFlowAsync(sourceNodeId, cancellationToken);
        return Ok(flows);
    }

    /// <summary>
    /// Get all PII flow paths in the system.
    /// </summary>
    [HttpGet("pii-flows")]
    [ProducesResponseType(typeof(List<PiiFlowPath>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PiiFlowPath>>> GetAllPiiFlows(
        CancellationToken cancellationToken)
    {
        var flows = await _graphSearch.GetAllPiiFlowsAsync(cancellationToken);
        return Ok(flows);
    }

    /// <summary>
    /// Get graph statistics.
    /// </summary>
    [HttpGet("graph/stats")]
    [ProducesResponseType(typeof(GraphStats), StatusCodes.Status200OK)]
    public async Task<ActionResult<GraphStats>> GetGraphStats(
        CancellationToken cancellationToken)
    {
        var stats = await _graphSearch.GetGraphStatsAsync(cancellationToken);
        return Ok(stats);
    }

    /// <summary>
    /// Record a user interaction for continuous learning.
    /// </summary>
    [HttpPost("interactions")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RecordInteraction(
        [FromBody] InteractionDto interaction,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst("sub")?.Value ?? "anonymous";

        await _learner.RecordInteractionAsync(
            new LearningInteraction(
                interaction.QueryId,
                userId,
                interaction.InteractionType,
                interaction.DocumentId,
                interaction.Data),
            cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Get learning analytics.
    /// </summary>
    [HttpGet("analytics")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(LearningAnalytics), StatusCodes.Status200OK)]
    public async Task<ActionResult<LearningAnalytics>> GetAnalytics(
        [FromQuery] DateTime? since,
        CancellationToken cancellationToken)
    {
        var analytics = await _learner.GetAnalyticsAsync(since, cancellationToken);
        return Ok(analytics);
    }

    /// <summary>
    /// Get AI-generated category suggestions for review.
    /// </summary>
    [HttpGet("category-suggestions")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(List<CategorySuggestionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CategorySuggestionDto>>> GetCategorySuggestions(
        [FromQuery] int maxSuggestions = 10,
        [FromQuery] decimal minConfidence = 0.7m,
        CancellationToken cancellationToken = default)
    {
        var suggestions = await _learner.GenerateCategorySuggestionsAsync(
            maxSuggestions, minConfidence, cancellationToken);
        return Ok(suggestions);
    }

    /// <summary>
    /// Export search results to various formats.
    /// </summary>
    [HttpPost("{queryId:guid}/export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExportResults(
        Guid queryId,
        [FromBody] ExportRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst("sub")?.Value ?? "anonymous";

        var exportRequest = new ExportRequest(
            queryId,
            userId,
            request.Format,
            request.Results ?? new List<SearchResultItem>(),
            new ExportOptions(
                IncludeLineageGraph: request.IncludeLineageGraph ?? false,
                IncludeMetadata: request.IncludeMetadata ?? true,
                IncludeChangeHistory: request.IncludeChangeHistory ?? false,
                ReportTitle: request.ReportTitle,
                ReportDescription: request.ReportDescription));

        ExportResult result = request.Format switch
        {
            ExportFormat.Csv => await _exporter.ExportToCsvAsync(exportRequest, cancellationToken),
            ExportFormat.Excel => await _exporter.ExportToExcelAsync(exportRequest, cancellationToken),
            ExportFormat.Pdf => await _exporter.ExportToPdfAsync(exportRequest, cancellationToken),
            _ => throw new ArgumentException($"Unsupported format: {request.Format}")
        };

        if (!result.Success || result.FileContent == null)
        {
            return BadRequest(result.ErrorMessage ?? "Export failed");
        }

        return File(result.FileContent, result.ContentType, result.FileName);
    }

    /// <summary>
    /// Trigger a manual graph rebuild (admin only).
    /// </summary>
    [HttpPost("graph/rebuild")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RebuildGraph(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Manual graph rebuild triggered by {User}",
            User.FindFirst("sub")?.Value);

        await _graphSearch.RebuildGraphAsync(cancellationToken);
        return NoContent();
    }
}

// Request DTOs for API
public record SearchRequestDto(
    string Query,
    int? MaxResults = 20,
    bool? IncludeLineage = false,
    bool? IncludePiiFlows = false,
    bool? EnableReranking = true,
    decimal? MinConfidence = 0.5m,
    List<string>? FilterDatabases = null,
    List<string>? FilterObjectTypes = null,
    List<string>? FilterCategories = null);

public record InteractionDto(
    Guid QueryId,
    string InteractionType,
    string? DocumentId,
    Dictionary<string, object>? Data);

public record ExportRequestDto(
    ExportFormat Format,
    List<SearchResultItem>? Results,
    bool? IncludeLineageGraph,
    bool? IncludeMetadata,
    bool? IncludeChangeHistory,
    string? ReportTitle,
    string? ReportDescription);

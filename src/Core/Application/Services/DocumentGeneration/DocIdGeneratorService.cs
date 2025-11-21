using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Dapper;

namespace Enterprise.Documentation.Core.Application.Services.DocumentGeneration;

/// <summary>
/// Generates DocIds following the naming convention:
/// {DocumentType}-{SequentialNumber}-{ObjectName}[-{ColumnName}]-{JiraNumber}
/// </summary>
public interface IDocIdGeneratorService
{
    Task<string> GenerateDocIdAsync(DocIdRequest request, CancellationToken cancellationToken = default);
}

public class DocIdRequest
{
    public required string ChangeType { get; set; }          // "Business Request", "Enhancement", "Defect Fix"
    public required string Table { get; set; }                // "gwpc.irf_policy"
    public string? Column { get; set; }                       // "PolicyNumber" (optional)
    public required string JiraNumber { get; set; }           // "BAS-123"
    public string UpdatedBy { get; set; } = "ExcelSyncService";
}

public class DocIdGeneratorService : IDocIdGeneratorService
{
    private readonly ILogger<DocIdGeneratorService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    // Change Type mappings
    private static readonly Dictionary<string, string> ChangeTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Business Request", "BR" },
        { "Enhancement", "EN" },
        { "Defect Fix", "DF" },
        { "Anomaly", "AN" },           // Future
        { "EDW-Research", "ER" },      // Future
        { "EDW-Q&A", "EQ" },           // Future
        { "Research", "RS" }           // Future
    };

    public DocIdGeneratorService(
        ILogger<DocIdGeneratorService> _logger,
        IConfiguration configuration)
    {
        this._logger = _logger;
        _configuration = configuration;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured");
    }

    public async Task<string> GenerateDocIdAsync(DocIdRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Map Change Type to Document Type code
            if (!ChangeTypeMap.TryGetValue(request.ChangeType, out var documentType))
            {
                _logger.LogWarning("Unknown change type: {ChangeType}, defaulting to 'EN'", request.ChangeType);
                documentType = "EN";
            }

            // 2. Get next sequential number
            var sequentialNumber = await GetNextSequentialNumberAsync(documentType, request.UpdatedBy, cancellationToken);

            // 3. Parse object name from Table field (e.g., "gwpc.irf_policy" -> "irf_policy")
            var objectName = ParseObjectName(request.Table);

            // 4. Build DocId components
            var docIdParts = new List<string>
            {
                documentType,
                sequentialNumber.ToString("D4")  // Zero-padded 4 digits
            };

            // Add object name
            docIdParts.Add(objectName);

            // Add column name if provided
            if (!string.IsNullOrWhiteSpace(request.Column))
            {
                docIdParts.Add(request.Column);
            }

            // Add Jira number
            docIdParts.Add(request.JiraNumber);

            var docId = string.Join("-", docIdParts);

            _logger.LogInformation("Generated DocId: {DocId} for {ChangeType}", docId, request.ChangeType);

            return docId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating DocId for {ChangeType}", request.ChangeType);
            throw;
        }
    }

    private async Task<int> GetNextSequentialNumberAsync(string documentType, string updatedBy, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@DocumentType", documentType);
        parameters.Add("@UpdatedBy", updatedBy);
        parameters.Add("@NextNumber", dbType: System.Data.DbType.Int32, direction: System.Data.ParameterDirection.Output);

        await connection.ExecuteAsync(
            "DaQa.usp_GetNextDocIdNumber",
            parameters,
            commandType: System.Data.CommandType.StoredProcedure
        );

        var nextNumber = parameters.Get<int>("@NextNumber");

        _logger.LogDebug("Retrieved next number for {DocumentType}: {NextNumber}", documentType, nextNumber);

        return nextNumber;
    }

    private string ParseObjectName(string tableField)
    {
        if (string.IsNullOrWhiteSpace(tableField))
        {
            _logger.LogWarning("Table field is empty, returning 'Unknown'");
            return "Unknown";
        }

        // Handle schema.table format (e.g., "gwpc.irf_policy" -> "irf_policy")
        var parts = tableField.Split('.');
        var objectName = parts.Length > 1 ? parts[1] : parts[0];

        // Clean up object name (remove any special characters except underscore)
        objectName = new string(objectName.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());

        return objectName;
    }
}

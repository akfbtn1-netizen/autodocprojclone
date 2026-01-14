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
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var parameters = new DynamicParameters();
            parameters.Add("@DocumentType", documentType);
            parameters.Add("@UpdatedBy", updatedBy);
            parameters.Add("@NextNumber", dbType: System.Data.DbType.Int32, direction: System.Data.ParameterDirection.Output);

            _logger.LogDebug("Executing stored procedure DaQa.usp_GetNextDocIdNumber for {DocumentType}", documentType);

            await connection.ExecuteAsync(
                "DaQa.usp_GetNextDocIdNumber",
                parameters,
                commandType: System.Data.CommandType.StoredProcedure,
                commandTimeout: 30
            );

            var nextNumber = parameters.Get<int>("@NextNumber");

            _logger.LogDebug("Retrieved next number for {DocumentType}: {NextNumber}", documentType, nextNumber);

            return nextNumber;
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "SQL error getting next sequential number for {DocumentType}. Error: {Message}", documentType, ex.Message);
            
            // If the stored procedure doesn't exist, fall back to a simple incremental number
            if (ex.Message.Contains("usp_GetNextDocIdNumber") || ex.Message.Contains("Invalid object name"))
            {
                _logger.LogWarning("Stored procedure not found, using fallback sequential numbering for {DocumentType}", documentType);
                return await GetFallbackSequentialNumberAsync(documentType, cancellationToken);
            }
            throw;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Task was canceled while getting sequential number for {DocumentType}", documentType);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting next sequential number for {DocumentType}", documentType);
            throw;
        }
    }

    private async Task<int> GetFallbackSequentialNumberAsync(string documentType, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Simple fallback: get max existing number + 1, or start at 1
            var sql = @"
                SELECT ISNULL(MAX(CAST(RIGHT(DocId, 3) AS INT)), 0) + 1 
                FROM daqa.DocumentChanges 
                WHERE DocId IS NOT NULL AND DocId LIKE @Pattern";

            var pattern = $"{documentType}-%";
            var nextNumber = await connection.QueryFirstOrDefaultAsync<int>(sql, new { Pattern = pattern }, commandTimeout: 30);

            _logger.LogDebug("Fallback sequential number for {DocumentType}: {NextNumber}", documentType, nextNumber);
            return Math.Max(nextNumber, 1); // Ensure at least 1
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in fallback sequential numbering for {DocumentType}", documentType);
            // Ultimate fallback: use timestamp-based number
            return (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 1000);
        }
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

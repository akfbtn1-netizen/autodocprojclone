// Automated change detection service for stored procedures

#if false // Temporarily disabled - Complex service with many dependencies

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Security.Cryptography;
using System.Text;

namespace Enterprise.Documentation.Core.Application.Services.Documentation;

/// <summary>
/// Background service that monitors stored procedures for changes and automatically
/// triggers documentation updates when procedures are modified.
/// </summary>
public class StoredProcedureChangeDetectionService : BackgroundService
{
    private readonly ILogger<StoredProcedureChangeDetectionService> _logger;
    private readonly IStoredProcedureDocumentationService _docService;
    private readonly string _connectionString;
    private readonly TimeSpan _scanInterval;

    public StoredProcedureChangeDetectionService(
        ILogger<StoredProcedureChangeDetectionService> logger,
        IStoredProcedureDocumentationService docService,
        IConfiguration configuration)
    {
        _logger = logger;
        _docService = docService;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection required");
        
        // Configurable scan interval (default: every 30 minutes)
        var intervalMinutes = configuration.GetValue("DocumentationServices:ChangeDetectionInterval", 30);
        _scanInterval = TimeSpan.FromMinutes(intervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("StoredProcedure change detection service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanForChangesAsync(stoppingToken);
                await Task.Delay(_scanInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during change detection scan");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Wait 5 minutes before retry
            }
        }
    }

    private async Task ScanForChangesAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting stored procedure change detection scan");

        var changedProcedures = await DetectChangedProceduresAsync(cancellationToken);
        
        if (changedProcedures.Any())
        {
            _logger.LogInformation("Detected {Count} changed stored procedures", changedProcedures.Count);
            
            foreach (var procedure in changedProcedures)
            {
                try
                {
                    await ProcessChangedProcedureAsync(procedure, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing changed procedure {ProcedureName}", procedure.Name);
                }
            }
        }
    }

    private async Task<List<ProcedureChangeInfo>> DetectChangedProceduresAsync(CancellationToken cancellationToken)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Get current procedure definitions and compare with stored hashes
        var currentProcedures = await connection.QueryAsync<ProcedureChangeInfo>(@"
            SELECT 
                s.name + '.' + o.name as FullName,
                o.name as Name,
                s.name as [Schema],
                o.modify_date as ModifyDate,
                m.definition as Definition
            FROM sys.objects o
            INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
            INNER JOIN sys.sql_modules m ON o.object_id = m.object_id
            WHERE o.type IN ('P', 'PC')
            AND o.name NOT LIKE 'sp_%' -- Exclude system procedures
            AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')");

        var changedProcedures = new List<ProcedureChangeInfo>();

        foreach (var procedure in currentProcedures)
        {
            var currentHash = ComputeHash(procedure.Definition);
            var storedHash = await GetStoredHashAsync(procedure.FullName, cancellationToken);

            if (storedHash != currentHash)
            {
                procedure.PreviousHash = storedHash;
                procedure.CurrentHash = currentHash;
                changedProcedures.Add(procedure);
            }
        }

        return changedProcedures;
    }

    private async Task ProcessChangedProcedureAsync(ProcedureChangeInfo procedure, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing changed procedure: {ProcedureName}", procedure.FullName);

        // Check if documentation exists
        var docExists = await _docService.SPDocumentationExistsAsync(procedure.Name, cancellationToken);
        
        if (docExists)
        {
            // Create automatic change document ID
            var autoChangeId = $"AUTO-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
            
            // Update documentation
            var docId = await _docService.CreateOrUpdateSPDocumentationAsync(
                procedure.Name, 
                autoChangeId, 
                cancellationToken);
            
            _logger.LogInformation("Updated documentation {DocId} for procedure {ProcedureName}", 
                docId, procedure.FullName);
            
            // Store the new hash to prevent duplicate processing
            await UpdateStoredHashAsync(procedure.FullName, procedure.CurrentHash, cancellationToken);
            
            // Send notification about the change
            await SendChangeNotificationAsync(procedure, docId, cancellationToken);
        }
        else
        {
            _logger.LogInformation("No existing documentation for {ProcedureName}, skipping auto-update", 
                procedure.FullName);
        }
    }

    private async Task<string?> GetStoredHashAsync(string procedureName, CancellationToken cancellationToken)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        return await connection.QueryFirstOrDefaultAsync<string>(@"
            SELECT DefinitionHash
            FROM DaQa.ProcedureChangeTracking
            WHERE ProcedureName = @ProcedureName",
            new { ProcedureName = procedureName });
    }

    private async Task UpdateStoredHashAsync(string procedureName, string hash, CancellationToken cancellationToken)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(@"
            MERGE DaQa.ProcedureChangeTracking AS target
            USING (SELECT @ProcedureName as ProcedureName, @Hash as DefinitionHash, GETUTCDATE() as LastChecked) AS source
            ON target.ProcedureName = source.ProcedureName
            WHEN MATCHED THEN
                UPDATE SET DefinitionHash = source.DefinitionHash, LastChecked = source.LastChecked
            WHEN NOT MATCHED THEN
                INSERT (ProcedureName, DefinitionHash, LastChecked)
                VALUES (source.ProcedureName, source.DefinitionHash, source.LastChecked);",
            new { ProcedureName = procedureName, Hash = hash });
    }

    private async Task SendChangeNotificationAsync(
        ProcedureChangeInfo procedure, 
        string docId, 
        CancellationToken cancellationToken)
    {
        // In a real implementation, this would send emails, Teams messages, etc.
        _logger.LogInformation(
            "CHANGE NOTIFICATION: Procedure {ProcedureName} was modified on {ModifyDate}. " +
            "Documentation {DocId} has been automatically updated. " +
            "Please review changes and update business documentation as needed.",
            procedure.FullName, procedure.ModifyDate, docId);
    }

    private string ComputeHash(string content)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(hashBytes);
    }
}

public class ProcedureChangeInfo
{
    public string FullName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public DateTime ModifyDate { get; set; }
    public string Definition { get; set; } = string.Empty;
    public string? PreviousHash { get; set; }
    public string CurrentHash { get; set; } = string.Empty;
}

#endif
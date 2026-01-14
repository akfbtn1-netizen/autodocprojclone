using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using Enterprise.Documentation.Core.Application.Services.MasterIndex;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Enterprise.Documentation.Core.Application.Services.StoredProcedure;

public interface IStoredProcedureDocumentationService
{
    Task<string> CreateOrUpdateSPDocumentationAsync(string procedureName, string documentId, CancellationToken cancellationToken = default);
    Task<bool> SPDocumentationExistsAsync(string procedureName, CancellationToken cancellationToken = default);
}

public class StoredProcedureDocumentationService : IStoredProcedureDocumentationService
{
    private readonly ILogger<StoredProcedureDocumentationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;
    private readonly string _outputPath;

    public StoredProcedureDocumentationService(
        ILogger<StoredProcedureDocumentationService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("DefaultConnection not found");
        _outputPath = configuration["StoredProcedureDocumentation:OutputPath"]
            ?? @"C:\Temp\Documentation-Catalog\Database";
    }

    public async Task<string> CreateOrUpdateSPDocumentationAsync(string procedureName, string documentId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🔥 SP DOC SERVICE CALLED 🔥");
        _logger.LogInformation("Procedure: {ProcName}, DocumentId: {DocId}", procedureName, documentId);
        _logger.LogInformation("Output path: {OutputPath}", _outputPath);
        _logger.LogInformation("Connection string exists: {HasConnection}", !string.IsNullOrEmpty(_connectionString));
            
        try
        {
            // Step 1: Get DocumentChanges entry 
            var documentChange = await GetDocumentChangeAsync(documentId, cancellationToken);
            if (documentChange == null)
            {
                _logger.LogWarning("⚠️ DocumentChanges entry not found for DocumentId: {DocumentId}", documentId);
                // Continue anyway - create basic documentation
            }

            // Step 2: Extract schema and procedure name
            var (schema, spName) = ParseProcedureName(procedureName);
            _logger.LogInformation("📋 Parsed SP: Schema={Schema}, Name={SpName}", schema, spName);
            
            // Step 3: Get SP metadata from database
            var spMetadata = await GetStoredProcedureMetadataAsync(schema, spName, cancellationToken);
            if (spMetadata == null)
            {
                throw new InvalidOperationException($"Stored procedure {schema}.{spName} not found in database");
            }

            // Step 4 & 5: Create Word documentation and save file
            var finalPath = await SaveDocumentationAsync(schema, spName, spMetadata, documentChange, cancellationToken);
            
            // Step 6: Update MasterIndex with documentation entry
            await UpdateMasterIndexAsync(schema, spName, finalPath, cancellationToken);
            
            _logger.LogInformation("✅ SP documentation created successfully: {FinalPath}", finalPath);
            
            return $"SP-DOC-{spName}-{DateTime.UtcNow:yyyyMMdd}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to create SP documentation for {ProcedureName}", procedureName);
            throw;
        }
    }

    public async Task<bool> SPDocumentationExistsAsync(string procedureName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking if SP documentation exists for {ProcedureName}", procedureName);
        
        try
        {
            var (schema, spName) = ParseProcedureName(procedureName);
            
            // Check MasterIndex for existing documentation entry
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            
            var query = @"
                SELECT COUNT(1) 
                FROM DaQa.MasterIndex 
                WHERE ObjectName = @ObjectName 
                  AND SchemaName = @Schema 
                  AND ObjectType = 'StoredProcedure'";
            
            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@ObjectName", spName);
            command.Parameters.AddWithValue("@Schema", schema);
            
            var count = (int)await command.ExecuteScalarAsync(cancellationToken);
            
            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if SP documentation exists for {ProcedureName}", procedureName);
            return false;
        }
    }

    private async Task<DocumentChange?> GetDocumentChangeAsync(string documentId, CancellationToken cancellationToken)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        var query = @"
            SELECT 
                DocId,
                StoredProcedureName,
                Description,
                Author,
                TicketNumber,
                ChangeType,
                CreatedDate
            FROM DaQa.DocumentChanges 
            WHERE DocId = @DocId";
        
        using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@DocId", documentId);
        
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        
        if (await reader.ReadAsync())
        {
            return new DocumentChange
            {
                DocId = reader["DocId"].ToString(),
                StoredProcedureName = reader["StoredProcedureName"].ToString(),
                Description = reader["Description"]?.ToString(),
                Author = reader["Author"]?.ToString(),
                TicketNumber = reader["TicketNumber"]?.ToString(),
                ChangeType = reader["ChangeType"]?.ToString(),
                CreatedDate = reader["CreatedDate"] as DateTime?
            };
        }
        
        return null;
    }

    private static (string schema, string procedureName) ParseProcedureName(string fullName)
    {
        // Handle formats like "DaQa.usp_TestDocumentation_V1" or "usp_TestDocumentation_V1"
        var parts = fullName.Split('.');
        if (parts.Length == 2)
        {
            return (parts[0], parts[1]);
        }
        
        // Default to DaQa schema if no schema specified
        return ("DaQa", fullName);
    }



    private async Task<SPMetadata?> GetStoredProcedureMetadataAsync(string schema, string procedureName, CancellationToken cancellationToken)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        var query = @"
            SELECT 
                SCHEMA_NAME(o.schema_id) as SchemaName,
                o.name as Name,
                o.type_desc as ObjectType,
                o.create_date as CreatedDate,
                o.modify_date as ModifiedDate,
                OBJECT_DEFINITION(o.object_id) as Definition
            FROM sys.objects o
            WHERE o.type = 'P'
                AND SCHEMA_NAME(o.schema_id) = @Schema
                AND o.name = @Name
                AND o.is_ms_shipped = 0";
        
        using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@Schema", schema);
        command.Parameters.AddWithValue("@Name", procedureName);
        
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        
        if (await reader.ReadAsync())
        {
            return new SPMetadata
            {
                Schema = reader["SchemaName"].ToString() ?? "",
                Name = reader["Name"].ToString() ?? "",
                ObjectType = reader["ObjectType"].ToString() ?? "",
                CreatedDate = reader["CreatedDate"] as DateTime?,
                ModifiedDate = reader["ModifiedDate"] as DateTime?,
                Definition = reader["Definition"].ToString() ?? ""
            };
        }
        
        return null;
    }

    private void CreateWordDocument(string filePath, SPMetadata spMetadata, DocumentChange? documentChange)
    {
        using (WordprocessingDocument wordDoc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document))
        {
            var mainPart = wordDoc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            // Title
            AddParagraph(body, "Stored Procedure Documentation", true, 18);
            AddParagraph(body, $"{spMetadata.Schema}.{spMetadata.Name}", true, 16);
            
            // Metadata section
            AddParagraph(body, $"Created: {spMetadata.CreatedDate:yyyy-MM-dd}", false, 11);
            AddParagraph(body, $"Modified: {spMetadata.ModifiedDate:yyyy-MM-dd}", false, 11);
            AddParagraph(body, $"Object Type: {spMetadata.ObjectType}", false, 11);
            
            // Description section
            AddParagraph(body, "Description", true, 14);
            AddParagraph(body, documentChange?.Description ?? "Auto-generated documentation", false, 11);
            
            // Source Code section
            AddParagraph(body, "Source Code", true, 14);
            AddCodeBlock(body, spMetadata.Definition);
            
            // Change Information section
            AddParagraph(body, "Change Information", true, 14);
            AddParagraph(body, $"Author: {documentChange?.Author ?? "System"}", false, 11);
            AddParagraph(body, $"Ticket: {documentChange?.TicketNumber ?? "N/A"}", false, 11);
            AddParagraph(body, $"Change Type: {documentChange?.ChangeType ?? "N/A"}", false, 11);
            AddParagraph(body, $"Created Date: {documentChange?.CreatedDate:yyyy-MM-dd HH:mm:ss}", false, 11);
            
            // Footer
            AddParagraph(body, "", false, 11); // Empty line
            AddParagraph(body, $"Generated by Enterprise Documentation Platform on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC", false, 9);
            
            mainPart.Document.Save();
        }
    }

    private void AddParagraph(Body body, string text, bool isBold, int fontSize)
    {
        var para = body.AppendChild(new Paragraph());
        var run = para.AppendChild(new Run());
        var runProps = run.AppendChild(new RunProperties());
        runProps.AppendChild(new FontSize { Val = (fontSize * 2).ToString() }); // Word uses half-points
        
        if (isBold)
        {
            runProps.AppendChild(new Bold());
        }
        
        run.AppendChild(new Text(text));
    }

    private void AddCodeBlock(Body body, string code)
    {
        var para = body.AppendChild(new Paragraph());
        var run = para.AppendChild(new Run());
        var runProps = run.AppendChild(new RunProperties());
        runProps.AppendChild(new RunFonts { Ascii = "Courier New" });
        runProps.AppendChild(new FontSize { Val = "20" }); // 10pt
        run.AppendChild(new Text(code) { Space = SpaceProcessingModeValues.Preserve });
    }

    private async Task<string> SaveDocumentationAsync(string schema, string procedureName, SPMetadata spMetadata, DocumentChange? documentChange, CancellationToken cancellationToken)
    {
        // Create directory structure
        var dirPath = Path.Combine(_outputPath, "IRFS1", schema, "StoredProcedures");
        Directory.CreateDirectory(dirPath);
        
        // Generate filename with version
        var version = 1.0;
        var fileName = $"{procedureName}_v{version:F1}.docx";
        var filePath = Path.Combine(dirPath, fileName);
        
        // If file exists, increment version
        while (File.Exists(filePath))
        {
            version += 0.1;
            fileName = $"{procedureName}_v{version:F1}.docx";
            filePath = Path.Combine(dirPath, fileName);
        }
        
        // Write Word document
        CreateWordDocument(filePath, spMetadata, documentChange);
        
        _logger.LogInformation("📁 Saved documentation to: {FilePath}", filePath);
        
        return filePath;
    }

    private async Task UpdateMasterIndexAsync(string schema, string procedureName, string filePath, CancellationToken cancellationToken)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            
            var query = @"
                INSERT INTO DaQa.MasterIndex (
                    DocumentTitle,
                    DocumentType,
                    SchemaName,
                    GeneratedDocPath,
                    VersionNumber,
                    Status,
                    CreatedDate,
                    ModifiedDate,
                    Description
                ) VALUES (
                    @DocumentTitle,
                    'StoredProcedure',
                    @SchemaName,
                    @GeneratedDocPath,
                    'v1.0',
                    'Published',
                    GETUTCDATE(),
                    GETUTCDATE(),
                    @Description
                )";
            
            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@DocumentTitle", $"{schema}.{procedureName}");
            command.Parameters.AddWithValue("@SchemaName", schema);
            command.Parameters.AddWithValue("@GeneratedDocPath", filePath);
            command.Parameters.AddWithValue("@Description", $"Auto-generated documentation for {procedureName}");
            
            await command.ExecuteNonQueryAsync(cancellationToken);
            
            _logger.LogInformation("📊 Updated MasterIndex for {Schema}.{ProcedureName}", schema, procedureName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update MasterIndex for {Schema}.{ProcedureName}", schema, procedureName);
            // Don't throw - documentation was created successfully
        }
    }
}

// Helper classes for SP documentation
public class DocumentChange
{
    public string? DocId { get; set; }
    public string? StoredProcedureName { get; set; }
    public string? Description { get; set; }
    public string? Author { get; set; }
    public string? TicketNumber { get; set; }
    public string? ChangeType { get; set; }
    public DateTime? CreatedDate { get; set; }
}

public class SPMetadata
{
    public string Schema { get; set; } = "";
    public string Name { get; set; } = "";
    public string ObjectType { get; set; } = "";
    public DateTime? CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string Definition { get; set; } = "";
}
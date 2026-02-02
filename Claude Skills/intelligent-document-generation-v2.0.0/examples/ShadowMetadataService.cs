using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.CustomProperties;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.VariantTypes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DocGen.Services;

/// <summary>
/// Synchronization status for documents against database schema
/// </summary>
public enum SyncStatus
{
    /// <summary>Document matches current database schema</summary>
    Current,
    /// <summary>Database schema has changed since last sync</summary>
    Stale,
    /// <summary>Update currently in progress</summary>
    Pending,
    /// <summary>Manual changes detected, requires review</summary>
    Conflict,
    /// <summary>Source database object no longer exists</summary>
    Orphaned,
    /// <summary>New document, not yet synchronized</summary>
    Draft
}

/// <summary>
/// Target audience types for documentation
/// </summary>
public enum AudienceType
{
    TechnicalDba,
    Developer,
    BusinessAnalyst,
    Executive,
    Compliance
}

/// <summary>
/// Shadow Metadata properties stored in Word custom properties
/// </summary>
public record ShadowMetadata
{
    public string DbObjectId { get; init; } = string.Empty;
    public string ContentHash { get; init; } = string.Empty;
    public string SyncStatus { get; init; } = "DRAFT";
    public string SchemaVersion { get; init; } = "1.0.0";
    public DateTime LastSync { get; init; } = DateTime.UtcNow;
    public int MasterIndexId { get; init; }
    public string AudienceType { get; init; } = "TECHNICAL_DBA";
    public string GeneratorVersion { get; init; } = "1.0.0";
}

/// <summary>
/// Manages Shadow Metadata in Word document custom properties.
/// Implements the Shadow Metadata pattern for document-database synchronization tracking.
/// </summary>
public class ShadowMetadataService
{
    // Standard format ID for custom document properties (per Open XML spec)
    private const string FormatId = "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}";
    
    private readonly ILogger<ShadowMetadataService> _logger;

    public ShadowMetadataService(ILogger<ShadowMetadataService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Computes SHA-256 hash of content for synchronization comparison
    /// </summary>
    public string ComputeContentHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return $"SHA256:{Convert.ToHexString(hash).ToLower()}";
    }

    /// <summary>
    /// Computes hash from a database schema object for comparison
    /// </summary>
    public string ComputeSchemaHash(object schemaObject)
    {
        var json = JsonSerializer.Serialize(schemaObject, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
        return ComputeContentHash(json);
    }

    /// <summary>
    /// Reads all shadow metadata from a Word document
    /// </summary>
    public async Task<ShadowMetadata> ReadMetadataAsync(string docxPath)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var document = WordprocessingDocument.Open(docxPath, false);
                var customProps = document.CustomFilePropertiesPart?.Properties;

                if (customProps == null)
                {
                    _logger.LogDebug("No custom properties found in {Path}", docxPath);
                    return new ShadowMetadata();
                }

                var props = ReadCustomProperties(customProps);
                
                return new ShadowMetadata
                {
                    DbObjectId = GetStringProperty(props, "DB_Object_ID"),
                    ContentHash = GetStringProperty(props, "Content_Hash"),
                    SyncStatus = GetStringProperty(props, "Sync_Status", "DRAFT"),
                    SchemaVersion = GetStringProperty(props, "Schema_Version", "1.0.0"),
                    LastSync = GetDateTimeProperty(props, "Last_Sync"),
                    MasterIndexId = GetIntProperty(props, "Master_Index_ID"),
                    AudienceType = GetStringProperty(props, "Audience_Type", "TECHNICAL_DBA"),
                    GeneratorVersion = GetStringProperty(props, "Generator_Version", "1.0.0")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read metadata from {Path}", docxPath);
                return new ShadowMetadata();
            }
        });
    }

    /// <summary>
    /// Writes shadow metadata to a Word document
    /// </summary>
    public async Task WriteMetadataAsync(string docxPath, ShadowMetadata metadata)
    {
        await Task.Run(() =>
        {
            try
            {
                using var document = WordprocessingDocument.Open(docxPath, true);
                
                // Get or create custom properties part
                var customPropsPart = document.CustomFilePropertiesPart 
                    ?? document.AddCustomFilePropertiesPart();
                
                customPropsPart.Properties = new Properties();
                
                int pid = 2; // PIDs start at 2 per Open XML spec
                
                // Write all metadata properties
                AddProperty(customPropsPart.Properties, "DB_Object_ID", metadata.DbObjectId, ref pid);
                AddProperty(customPropsPart.Properties, "Content_Hash", metadata.ContentHash, ref pid);
                AddProperty(customPropsPart.Properties, "Sync_Status", metadata.SyncStatus, ref pid);
                AddProperty(customPropsPart.Properties, "Schema_Version", metadata.SchemaVersion, ref pid);
                AddProperty(customPropsPart.Properties, "Last_Sync", metadata.LastSync, ref pid);
                AddProperty(customPropsPart.Properties, "Master_Index_ID", metadata.MasterIndexId, ref pid);
                AddProperty(customPropsPart.Properties, "Audience_Type", metadata.AudienceType, ref pid);
                AddProperty(customPropsPart.Properties, "Generator_Version", metadata.GeneratorVersion, ref pid);
                
                customPropsPart.Properties.Save();
                
                _logger.LogInformation("Updated shadow metadata for {Path}", docxPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write metadata to {Path}", docxPath);
                throw;
            }
        });
    }

    /// <summary>
    /// Checks synchronization status by comparing document hash with current database hash
    /// </summary>
    public async Task<SyncStatus> CheckSyncStatusAsync(string docxPath, string currentDbHash)
    {
        var metadata = await ReadMetadataAsync(docxPath);
        
        if (string.IsNullOrEmpty(metadata.ContentHash))
        {
            return Services.SyncStatus.Draft;
        }
        
        return metadata.ContentHash == currentDbHash 
            ? Services.SyncStatus.Current 
            : Services.SyncStatus.Stale;
    }

    /// <summary>
    /// Updates all synchronization metadata after successful document generation
    /// </summary>
    public async Task UpdateSyncMetadataAsync(
        string docxPath,
        string dbObjectId,
        string contentHash,
        int masterIndexId,
        AudienceType audienceType = AudienceType.TechnicalDba,
        string schemaVersion = "2.1.0")
    {
        var metadata = new ShadowMetadata
        {
            DbObjectId = dbObjectId,
            ContentHash = contentHash,
            SyncStatus = "CURRENT",
            SchemaVersion = schemaVersion,
            LastSync = DateTime.UtcNow,
            MasterIndexId = masterIndexId,
            AudienceType = audienceType.ToString().ToUpper(),
            GeneratorVersion = "1.0.0"
        };

        await WriteMetadataAsync(docxPath, metadata);
    }

    /// <summary>
    /// Marks document as stale when database changes are detected
    /// </summary>
    public async Task MarkAsStaleAsync(string docxPath)
    {
        var metadata = await ReadMetadataAsync(docxPath);
        var updated = metadata with { SyncStatus = "STALE" };
        await WriteMetadataAsync(docxPath, updated);
    }

    /// <summary>
    /// Marks document as orphaned when source object is deleted
    /// </summary>
    public async Task MarkAsOrphanedAsync(string docxPath)
    {
        var metadata = await ReadMetadataAsync(docxPath);
        var updated = metadata with { SyncStatus = "ORPHANED" };
        await WriteMetadataAsync(docxPath, updated);
    }

    #region Private Helper Methods

    private Dictionary<string, object?> ReadCustomProperties(Properties properties)
    {
        var result = new Dictionary<string, object?>();
        
        foreach (var prop in properties.Elements<CustomDocumentProperty>())
        {
            if (prop.Name?.Value is not null)
            {
                result[prop.Name.Value] = GetPropertyValue(prop);
            }
        }
        
        return result;
    }

    private object? GetPropertyValue(CustomDocumentProperty prop)
    {
        if (prop.VTLPWSTR != null) return prop.VTLPWSTR.Text;
        if (prop.VTInt32 != null && int.TryParse(prop.VTInt32.Text, out var intVal)) return intVal;
        if (prop.VTFloat != null && double.TryParse(prop.VTFloat.Text, out var floatVal)) return floatVal;
        if (prop.VTBool != null) return prop.VTBool.Text?.ToLower() == "true";
        if (prop.VTFileTime != null && DateTime.TryParse(prop.VTFileTime.Text, out var dateVal)) return dateVal;
        return null;
    }

    private void AddProperty(Properties properties, string name, string value, ref int pid)
    {
        var prop = new CustomDocumentProperty
        {
            FormatId = FormatId,
            PropertyId = pid++,
            Name = name,
            VTLPWSTR = new VTLPWSTR(value ?? string.Empty)
        };
        properties.AppendChild(prop);
    }

    private void AddProperty(Properties properties, string name, int value, ref int pid)
    {
        var prop = new CustomDocumentProperty
        {
            FormatId = FormatId,
            PropertyId = pid++,
            Name = name,
            VTInt32 = new VTInt32(value.ToString())
        };
        properties.AppendChild(prop);
    }

    private void AddProperty(Properties properties, string name, DateTime value, ref int pid)
    {
        var prop = new CustomDocumentProperty
        {
            FormatId = FormatId,
            PropertyId = pid++,
            Name = name,
            VTFileTime = new VTFileTime(value.ToString("yyyy-MM-ddTHH:mm:ssZ"))
        };
        properties.AppendChild(prop);
    }

    private static string GetStringProperty(Dictionary<string, object?> props, string key, string defaultValue = "")
    {
        return props.TryGetValue(key, out var value) && value is string str ? str : defaultValue;
    }

    private static int GetIntProperty(Dictionary<string, object?> props, string key, int defaultValue = 0)
    {
        return props.TryGetValue(key, out var value) && value is int intVal ? intVal : defaultValue;
    }

    private static DateTime GetDateTimeProperty(Dictionary<string, object?> props, string key)
    {
        return props.TryGetValue(key, out var value) && value is DateTime dt ? dt : DateTime.UtcNow;
    }

    #endregion
}

/// <summary>
/// Extension methods for ShadowMetadataService
/// </summary>
public static class ShadowMetadataServiceExtensions
{
    /// <summary>
    /// Registers ShadowMetadataService with dependency injection
    /// </summary>
    public static IServiceCollection AddShadowMetadataService(this IServiceCollection services)
    {
        services.AddScoped<ShadowMetadataService>();
        return services;
    }
}

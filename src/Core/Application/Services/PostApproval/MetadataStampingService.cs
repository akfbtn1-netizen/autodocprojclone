// =============================================================================
// Agent #5: Post-Approval Pipeline - Metadata Stamping Service
// Stamps Shadow Metadata as custom properties into Word documents
// =============================================================================

using System.Security.Cryptography;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.CustomProperties;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.VariantTypes;
using Microsoft.Extensions.Logging;

namespace Enterprise.Documentation.Core.Application.Services.PostApproval;

/// <summary>
/// Stamps Shadow Metadata as custom properties into Word documents.
/// This enables documents to be self-aware of their synchronization state.
/// </summary>
public class MetadataStampingService : IMetadataStampingService
{
    private readonly ILogger<MetadataStampingService> _logger;

    // Custom property names (Shadow Metadata)
    private static class PropertyNames
    {
        public const string DocumentId = "Doc_ID";
        public const string SyncStatus = "Sync_Status";
        public const string ContentHash = "Content_Hash";
        public const string SchemaHash = "Schema_Hash";
        public const string MasterIndexId = "Master_Index_ID";
        public const string LastSync = "Last_Sync";
        public const string TokensUsed = "Tokens_Used";
        public const string GenerationCost = "Generation_Cost_USD";
        public const string AIModel = "AI_Model";
        public const string GeneratedAt = "Generated_At";
        public const string ApprovedAt = "Approved_At";
        public const string ApprovedBy = "Approved_By";
        public const string SchemaName = "Schema_Name";
        public const string ObjectName = "Object_Name";
        public const string ObjectType = "Object_Type";
        public const string BusinessDomain = "Business_Domain";
        public const string DataClassification = "Data_Classification";
        public const string ContainsPII = "Contains_PII";
        public const string JiraNumber = "Jira_Number";
        public const string CABNumber = "CAB_Number";
    }

    public MetadataStampingService(ILogger<MetadataStampingService> logger)
    {
        _logger = logger;
    }

    public async Task<StampingResult> StampDocumentAsync(
        string documentPath,
        FinalizedMetadata metadata,
        CancellationToken ct = default)
    {
        var result = new StampingResult
        {
            DocumentPath = documentPath,
            StampedAt = DateTime.UtcNow,
            StampedProperties = new List<string>()
        };

        try
        {
            if (!File.Exists(documentPath))
            {
                result.Success = false;
                result.ErrorMessage = $"Document not found: {documentPath}";
                return result;
            }

            _logger.LogInformation("Stamping metadata to document: {Path}", documentPath);

            using var doc = WordprocessingDocument.Open(documentPath, true);

            // Get or create custom properties part
            var customProps = doc.CustomFilePropertiesPart;
            if (customProps == null)
            {
                customProps = doc.AddCustomFilePropertiesPart();
                customProps.Properties = new Properties();
            }

            var props = customProps.Properties;
            var nextPid = GetNextPropertyId(props);

            // Stamp all metadata properties
            StampProperty(props, ref nextPid, PropertyNames.DocumentId, metadata.DocumentId, result);
            StampProperty(props, ref nextPid, PropertyNames.SyncStatus, "CURRENT", result);
            StampProperty(props, ref nextPid, PropertyNames.ContentHash, metadata.ContentHash ?? ComputeContentHash(doc), result);
            StampProperty(props, ref nextPid, PropertyNames.LastSync, DateTime.UtcNow.ToString("O"), result);

            if (metadata.MasterIndexId.HasValue)
                StampProperty(props, ref nextPid, PropertyNames.MasterIndexId, metadata.MasterIndexId.Value, result);

            StampProperty(props, ref nextPid, PropertyNames.TokensUsed, metadata.TokensUsed, result);
            StampProperty(props, ref nextPid, PropertyNames.GenerationCost, (double)metadata.GenerationCostUSD, result);
            StampProperty(props, ref nextPid, PropertyNames.AIModel, metadata.AIModel ?? "gpt-4", result);
            StampProperty(props, ref nextPid, PropertyNames.GeneratedAt, metadata.ExtractedAt.ToString("O"), result);
            StampProperty(props, ref nextPid, PropertyNames.ApprovedAt, metadata.ApprovedAt.ToString("O"), result);
            StampProperty(props, ref nextPid, PropertyNames.ApprovedBy, metadata.ApprovedBy, result);
            StampProperty(props, ref nextPid, PropertyNames.SchemaName, metadata.SchemaName, result);
            StampProperty(props, ref nextPid, PropertyNames.ObjectName, metadata.ObjectName, result);
            StampProperty(props, ref nextPid, PropertyNames.ObjectType, metadata.ObjectType, result);

            if (metadata.Classification != null)
            {
                StampProperty(props, ref nextPid, PropertyNames.BusinessDomain, metadata.Classification.BusinessDomain, result);
                StampProperty(props, ref nextPid, PropertyNames.DataClassification, metadata.Classification.DataClassification, result);
                StampProperty(props, ref nextPid, PropertyNames.ContainsPII, metadata.Classification.ContainsPII, result);
            }

            if (!string.IsNullOrEmpty(metadata.JiraNumber))
                StampProperty(props, ref nextPid, PropertyNames.JiraNumber, metadata.JiraNumber, result);

            if (!string.IsNullOrEmpty(metadata.CABNumber))
                StampProperty(props, ref nextPid, PropertyNames.CABNumber, metadata.CABNumber, result);

            props.Save();
            result.Success = true;
            result.PropertiesStamped = result.StampedProperties.Count;

            _logger.LogInformation("Stamped {Count} properties to {Path}", result.PropertiesStamped, documentPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stamp document: {Path}", documentPath);
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return await Task.FromResult(result);
    }

    public async Task<ShadowMetadata?> ReadShadowMetadataAsync(string documentPath, CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(documentPath))
                return null;

            using var doc = WordprocessingDocument.Open(documentPath, false);
            var customProps = doc.CustomFilePropertiesPart?.Properties;

            if (customProps == null)
                return null;

            var shadow = new ShadowMetadata
            {
                DocumentId = GetPropertyValue<string>(customProps, PropertyNames.DocumentId) ?? string.Empty,
                SyncStatus = GetPropertyValue<string>(customProps, PropertyNames.SyncStatus) ?? "UNKNOWN",
                ContentHash = GetPropertyValue<string>(customProps, PropertyNames.ContentHash) ?? string.Empty,
                SchemaHash = GetPropertyValue<string>(customProps, PropertyNames.SchemaHash),
                MasterIndexId = GetPropertyValue<int?>(customProps, PropertyNames.MasterIndexId),
                LastModified = DateTime.TryParse(
                    GetPropertyValue<string>(customProps, PropertyNames.LastSync),
                    out var lastSync) ? lastSync : DateTime.MinValue,
                TokensUsed = GetPropertyValue<int?>(customProps, PropertyNames.TokensUsed),
                GenerationCostUSD = (decimal?)GetPropertyValue<double?>(customProps, PropertyNames.GenerationCost),
                AIModel = GetPropertyValue<string>(customProps, PropertyNames.AIModel)
            };

            return await Task.FromResult(shadow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read shadow metadata from {Path}", documentPath);
            return null;
        }
    }

    public async Task<SyncStatus> ValidateSyncStatusAsync(string documentPath, CancellationToken ct = default)
    {
        var shadow = await ReadShadowMetadataAsync(documentPath, ct);

        if (shadow == null)
            return SyncStatus.Draft;

        try
        {
            // Compute current content hash and compare
            using var doc = WordprocessingDocument.Open(documentPath, false);
            var currentHash = ComputeContentHash(doc);

            if (currentHash != shadow.ContentHash)
                return SyncStatus.Conflict;

            // Check if underlying schema has changed (would need DB query)
            // TODO [5]: Implement schema hash comparison via DB lookup
            // For now, trust the stored status
            return Enum.TryParse<SyncStatus>(shadow.SyncStatus, true, out var status)
                ? status
                : SyncStatus.Stale;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating sync status for {Path}", documentPath);
            return SyncStatus.Stale;
        }
    }

    #region Private Helpers

    private int GetNextPropertyId(Properties props)
    {
        var maxPid = 1;
        foreach (var prop in props.Elements<CustomDocumentProperty>())
        {
            if (prop.PropertyId?.Value > maxPid)
                maxPid = prop.PropertyId.Value;
        }
        return maxPid + 1;
    }

    private void StampProperty(Properties props, ref int pid, string name, string value, StampingResult result)
    {
        RemoveExistingProperty(props, name);
        props.AppendChild(new CustomDocumentProperty
        {
            FormatId = "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}",
            PropertyId = pid++,
            Name = name,
            VTLPWSTR = new VTLPWSTR(value)
        });
        result.StampedProperties.Add(name);
    }

    private void StampProperty(Properties props, ref int pid, string name, int value, StampingResult result)
    {
        RemoveExistingProperty(props, name);
        props.AppendChild(new CustomDocumentProperty
        {
            FormatId = "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}",
            PropertyId = pid++,
            Name = name,
            VTInt32 = new VTInt32(value.ToString())
        });
        result.StampedProperties.Add(name);
    }

    private void StampProperty(Properties props, ref int pid, string name, double value, StampingResult result)
    {
        RemoveExistingProperty(props, name);
        props.AppendChild(new CustomDocumentProperty
        {
            FormatId = "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}",
            PropertyId = pid++,
            Name = name,
            VTDouble = new VTDouble(value.ToString())
        });
        result.StampedProperties.Add(name);
    }

    private void StampProperty(Properties props, ref int pid, string name, bool value, StampingResult result)
    {
        RemoveExistingProperty(props, name);
        props.AppendChild(new CustomDocumentProperty
        {
            FormatId = "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}",
            PropertyId = pid++,
            Name = name,
            VTBool = new VTBool(value ? "true" : "false")
        });
        result.StampedProperties.Add(name);
    }

    private void RemoveExistingProperty(Properties props, string name)
    {
        var existing = props.Elements<CustomDocumentProperty>()
            .FirstOrDefault(p => p.Name?.Value == name);
        existing?.Remove();
    }

    private T? GetPropertyValue<T>(Properties props, string name)
    {
        var prop = props.Elements<CustomDocumentProperty>()
            .FirstOrDefault(p => p.Name?.Value == name);

        if (prop == null) return default;

        var value = prop.VTLPWSTR?.Text
            ?? prop.VTInt32?.Text
            ?? prop.VTDouble?.Text
            ?? prop.VTBool?.Text;

        if (value == null) return default;

        try
        {
            if (typeof(T) == typeof(string))
                return (T)(object)value;
            if (typeof(T) == typeof(int) || typeof(T) == typeof(int?))
                return (T)(object)int.Parse(value);
            if (typeof(T) == typeof(double) || typeof(T) == typeof(double?))
                return (T)(object)double.Parse(value);
            if (typeof(T) == typeof(bool) || typeof(T) == typeof(bool?))
                return (T)(object)(value.ToLower() == "true");
        }
        catch { }

        return default;
    }

    private string ComputeContentHash(WordprocessingDocument doc)
    {
        var body = doc.MainDocumentPart?.Document.Body;
        if (body == null) return string.Empty;

        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(body.InnerText);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }

    #endregion
}

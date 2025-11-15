namespace Enterprise.Documentation.Core.Domain.Entities;

/// <summary>
/// Master index entity representing cataloged database objects
/// </summary>
public class MasterIndex : BaseEntity
{
    public string? TableName { get; set; }
    public string? DocumentTitle { get; set; }
    public string? SchemaName { get; set; }
    public string? DatabaseName { get; set; }
    public string? Description { get; set; }
    public string? DocumentType { get; set; }
    public string? BusinessDomain { get; set; }
    public string? UpstreamSources { get; set; }
    public string? DownstreamTargets { get; set; }
    public string? SourceDocumentID { get; set; }
    public string? CreatedBy { get; set; }
    public string? UsagePurpose { get; set; }
    public string? OptimizationSuggestions { get; set; }
}

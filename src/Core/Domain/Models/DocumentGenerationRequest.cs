using Enterprise.Documentation.Core.Domain.Enums;

namespace Enterprise.Documentation.Core.Domain.Models;

/// <summary>
/// Request model for document generation
/// </summary>
public class DocumentGenerationRequest
{
    public int? MasterIndexId { get; set; }
    public DocumentType DocumentType { get; set; }
    public string ObjectName { get; set; } = string.Empty;
    public string Schema { get; set; } = "dbo";
    public string DatabaseName { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string TicketNumber { get; set; } = string.Empty;
}

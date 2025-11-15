using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.Enums;

namespace Enterprise.Documentation.Core.Application.Interfaces;

/// <summary>
/// Service for selecting appropriate templates based on document requirements
/// </summary>
public interface ITemplateSelector
{
    /// <summary>
    /// Determines the complexity tier for a document
    /// </summary>
    TemplateComplexity DetermineComplexity(MasterIndex masterIndex);

    /// <summary>
    /// Gets the template filename for a given document type and complexity
    /// </summary>
    string GetTemplateFileName(DocumentType documentType, TemplateComplexity complexity);
}

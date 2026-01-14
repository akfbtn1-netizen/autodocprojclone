// Temporary stub interface to resolve dependency injection
namespace Enterprise.Documentation.Core.Application.Services.Documentation;

/// <summary>
/// Stub interface for stored procedure documentation service (temporarily disabled)
/// </summary>
public interface IStoredProcedureDocumentationService
{
    Task<string> CreateOrUpdateDocumentationAsync(string procedureName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Stub implementation for stored procedure documentation service
/// </summary>
public class StoredProcedureDocumentationService : IStoredProcedureDocumentationService
{
    public async Task<string> CreateOrUpdateDocumentationAsync(string procedureName, CancellationToken cancellationToken = default)
    {
        // Stub implementation
        await Task.CompletedTask;
        return $"Stub documentation for {procedureName}";
    }
}
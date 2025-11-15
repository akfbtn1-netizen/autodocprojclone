using Enterprise.Documentation.Core.Domain.Entities;

namespace Enterprise.Documentation.Core.Application.Interfaces;

/// <summary>
/// Repository interface for MasterIndex entities
/// </summary>
public interface IMasterIndexRepository
{
    Task<MasterIndex?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<MasterIndex>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<MasterIndex> AddAsync(MasterIndex masterIndex, CancellationToken cancellationToken = default);
    Task UpdateAsync(MasterIndex masterIndex, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

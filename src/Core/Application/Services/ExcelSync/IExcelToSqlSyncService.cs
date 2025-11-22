using System.Threading;
using System.Threading.Tasks;

namespace Enterprise.Documentation.Core.Application.Services.ExcelSync;

/// <summary>
/// Background service interface for syncing data from Excel to SQL
/// </summary>
public interface IExcelToSqlSyncService
{
    /// <summary>
    /// Manually trigger a sync operation (can be called outside the background service cycle)
    /// </summary>
    Task SyncExcelToSqlAsync(CancellationToken cancellationToken);
}

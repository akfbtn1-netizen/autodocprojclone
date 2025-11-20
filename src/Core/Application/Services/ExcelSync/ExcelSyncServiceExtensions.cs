using Microsoft.Extensions.DependencyInjection;

namespace Enterprise.Documentation.Core.Application.Services.ExcelSync;

/// <summary>
/// Extension methods for registering Excel sync services.
/// </summary>
public static class ExcelSyncServiceExtensions
{
    /// <summary>
    /// Adds the Excel to SQL sync background service.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddExcelToSqlSync(this IServiceCollection services)
    {
        services.AddHostedService<ExcelToSqlSyncService>();
        return services;
    }
}

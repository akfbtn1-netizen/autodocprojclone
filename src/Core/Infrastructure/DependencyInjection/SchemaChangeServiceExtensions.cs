// ═══════════════════════════════════════════════════════════════════════════
// Agent #4: Schema Change Detector - DI Registration
// ═══════════════════════════════════════════════════════════════════════════

using Enterprise.Documentation.Core.Application.Interfaces.SchemaChange;
using Enterprise.Documentation.Core.Infrastructure.Persistence.Repositories;
using Enterprise.Documentation.Core.Infrastructure.Services.SchemaChange;
using Enterprise.Documentation.Api.Hubs;
using Microsoft.Extensions.DependencyInjection;

namespace Enterprise.Documentation.Core.Infrastructure.DependencyInjection;

public static class SchemaChangeServiceExtensions
{
    /// <summary>
    /// Registers all Agent #4 Schema Change Detector services.
    /// </summary>
    public static IServiceCollection AddSchemaChangeDetector(this IServiceCollection services)
    {
        // Repositories
        services.AddScoped<ISchemaChangeRepository, SchemaChangeRepository>();
        services.AddScoped<IDetectionRunRepository, DetectionRunRepository>();
        services.AddScoped<ISchemaSnapshotRepository, SchemaSnapshotRepository>();

        // Services
        services.AddScoped<ISchemaChangeDetectorService, SchemaChangeDetectorService>();
        services.AddScoped<IImpactAnalysisService, ImpactAnalysisService>();

        // SignalR notifier
        services.AddScoped<ISchemaChangeNotifier, SchemaChangeNotifier>();

        return services;
    }
}

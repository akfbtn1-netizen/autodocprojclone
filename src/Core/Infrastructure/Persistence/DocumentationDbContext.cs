using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Core.Domain.Entities;

namespace Core.Infrastructure.Persistence;

/// <summary>
/// Simple Entity Framework Core database context for the Enterprise Documentation Platform.
/// Provides basic database access and entity configuration for the domain model.
/// </summary>
public class DocumentationDbContext : DbContext
{
    private readonly ILogger<DocumentationDbContext> _logger;

    /// <summary>
    /// Initializes a new database context with the specified options.
    /// </summary>
    /// <param name="options">Database context options</param>
    /// <param name="logger">Logger instance</param>
    public DocumentationDbContext(DbContextOptions<DocumentationDbContext> options, ILogger<DocumentationDbContext> logger)
        : base(options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // Entity Sets
    public DbSet<Document> Documents => Set<Document>();

    /// <summary>
    /// Configures the entity model using Fluent API.
    /// </summary>
    /// <param name="modelBuilder">Model builder instance</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Document entity
        modelBuilder.Entity<Document>(entity =>
        {
            entity.ToTable("Documents");
            entity.HasKey(d => d.Id);
            
            entity.Property(d => d.Title)
                .IsRequired()
                .HasMaxLength(255);
                
            entity.Property(d => d.Description)
                .HasMaxLength(2000);
                
            entity.Property(d => d.Category)
                .IsRequired()
                .HasMaxLength(100);
                
            entity.Property(d => d.Version)
                .IsRequired()
                .HasMaxLength(50);
                
            entity.Property(d => d.ContentType)
                .IsRequired()
                .HasMaxLength(50);
                
            // Configure Tags as JSON (simple approach)
            entity.Property(d => d.Tags)
                .HasConversion(
                    tags => string.Join(",", tags),
                    value => value.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
                    
            // Soft delete filter
            entity.HasQueryFilter(d => !d.IsDeleted);
            
            // Indexes for performance
            entity.HasIndex(d => d.Title);
            entity.HasIndex(d => d.Category);
            entity.HasIndex(d => d.CreatedAt);
        });

        _logger.LogDebug("Entity model configured for DocumentationDbContext");
    }

    /// <summary>
    /// Intercepts SaveChanges to implement audit trail functionality.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of affected rows</returns>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Update audit properties before saving
        UpdateAuditProperties();

        var result = await base.SaveChangesAsync(cancellationToken);
        
        _logger.LogDebug("Saved {ChangeCount} changes to database", result);
        return result;
    }

    /// <summary>
    /// Updates audit properties for tracked entities.
    /// </summary>
    private void UpdateAuditProperties()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is BaseEntity && (e.State == EntityState.Added || e.State == EntityState.Modified));

        foreach (var entry in entries)
        {
            var entity = (BaseEntity)entry.Entity;
            var now = DateTime.UtcNow;

            if (entry.State == EntityState.Added)
            {
                entity.CreatedAt = now;
                entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entity.UpdatedAt = now;
                
                // Prevent modification of CreatedAt
                entry.Property(nameof(BaseEntity.CreatedAt)).IsModified = false;
            }
        }
    }
}
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.ValueObjects;
using DomainVersion = Enterprise.Documentation.Core.Domain.Entities.Version;

namespace Enterprise.Documentation.Core.Infrastructure.Persistence;

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
    public DbSet<User> Users => Set<User>();
    public DbSet<Template> Templates => Set<Template>();
    public DbSet<DomainVersion> Versions => Set<DomainVersion>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<VersionApproval> VersionApprovals => Set<VersionApproval>();

    /// <summary>
    /// Configures the entity model using Fluent API.
    /// </summary>
    /// <param name="modelBuilder">Model builder instance</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Ignore value object types to prevent them from being treated as entities
        IgnoreValueObjects(modelBuilder);
        
        // Configure value converters for all strongly-typed IDs
        ConfigureValueConverters(modelBuilder);

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

            // Configure ApprovalStatus as owned entity
            entity.OwnsOne(d => d.ApprovalStatus, approval =>
            {
                approval.Property(a => a.Status).HasMaxLength(50);
                approval.Property(a => a.Comments).HasMaxLength(2000);
                approval.Property(a => a.StatusChangedAt);
                approval.Property(a => a.ApprovedBy)
                    .HasConversion(
                        id => id != null ? id.Value : (Guid?)null,
                        value => value.HasValue ? new UserId(value.Value) : null);
            });

            // Configure SecurityClassification as owned entity
            entity.OwnsOne(d => d.SecurityClassification, sc =>
            {
                sc.Property(s => s.Level).HasMaxLength(50);
                sc.Property(s => s.RequiresPIIHandling);
                sc.Property(s => s.ClassifiedAt);
                sc.Property(s => s.ClassifiedBy)
                    .HasConversion(id => id.Value, value => new UserId(value));
                sc.Property(s => s.AccessGroups)
                    .HasConversion(
                        groups => string.Join(",", groups),
                        value => value.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
            });
                    
            // Soft delete filter
            entity.HasQueryFilter(d => !d.IsDeleted);
            
            // Indexes for performance (PERFORMANCE FIX)
            entity.HasIndex(d => d.Title);
            entity.HasIndex(d => d.Category);
            entity.HasIndex(d => d.CreatedAt);

            // Composite indexes for common query patterns (PERFORMANCE FIX - 10-100x faster queries)
            entity.HasIndex(d => new { d.Category, d.Status })
                .HasDatabaseName("IX_Documents_Category_Status");
            entity.HasIndex(d => new { d.CreatedBy, d.CreatedAt })
                .HasDatabaseName("IX_Documents_CreatedBy_CreatedAt");
            entity.HasIndex(d => new { d.Status, d.PublishedAt })
                .HasDatabaseName("IX_Documents_Status_Published");
            entity.HasIndex(d => new { d.IsDeleted, d.CreatedAt })
                .HasDatabaseName("IX_Documents_IsDeleted_CreatedAt");
        });

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(u => u.Id);
            
            entity.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(255);
                
            entity.Property(u => u.DisplayName)
                .IsRequired()
                .HasMaxLength(255);
                
            entity.Property(u => u.FirstName)
                .HasMaxLength(100);
                
            entity.Property(u => u.LastName)
                .HasMaxLength(100);
                
            entity.Property(u => u.Department)
                .HasMaxLength(100);
                
            entity.Property(u => u.JobTitle)
                .HasMaxLength(100);

            // Configure SecurityClearance as enum
            entity.Property(u => u.SecurityClearance)
                .HasConversion<string>();

            // Configure Roles as JSON
            entity.Property(u => u.Roles)
                .HasConversion(
                    roles => string.Join(",", roles.Select(r => r.ToString())),
                    value => value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                           .Select(s => Enum.Parse<UserRole>(s)).ToList());

            // Configure UserPreferences as owned entity
            entity.OwnsOne(u => u.Preferences, prefs =>
            {
                prefs.Property(p => p.Theme).HasMaxLength(50);
                prefs.Property(p => p.Language).HasMaxLength(10);
                prefs.Property(p => p.TimeZone).HasMaxLength(100);
                prefs.Property(p => p.EmailNotifications);
                prefs.Property(p => p.PushNotifications);
                prefs.Property(p => p.PageSize);
            });

            // Unique email constraint
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasIndex(u => u.DisplayName);
        });

        // Configure Template entity
        modelBuilder.Entity<Template>(entity =>
        {
            entity.ToTable("Templates");
            entity.HasKey(t => t.Id);
            
            entity.Property(t => t.Name)
                .IsRequired()
                .HasMaxLength(255);
                
            entity.Property(t => t.Description)
                .HasMaxLength(2000);
                
            entity.Property(t => t.Category)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(t => t.Content)
                .HasMaxLength(int.MaxValue);

            // Configure Variables as owned collection
            entity.OwnsMany(t => t.Variables, variable =>
            {
                variable.Property(v => v.Name).HasMaxLength(100);
                variable.Property(v => v.DisplayName).HasMaxLength(200);
                variable.Property(v => v.Description).HasMaxLength(1000);
                variable.Property(v => v.Type).HasConversion<string>();
                variable.Property(v => v.IsRequired);
                variable.Property(v => v.DefaultValue).HasMaxLength(500);
                variable.Property(v => v.AllowedValues)
                    .HasConversion(
                        values => values != null ? string.Join(",", values) : null,
                        value => !string.IsNullOrEmpty(value) ? value.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() : null);
            });

            // Configure SecurityClassification as owned entity
            entity.OwnsOne(t => t.DefaultSecurityClassification, sc =>
            {
                sc.Property(s => s.Level).HasMaxLength(50);
                sc.Property(s => s.RequiresPIIHandling);
                sc.Property(s => s.ClassifiedAt);
                sc.Property(s => s.ClassifiedBy)
                    .HasConversion(id => id.Value, value => new UserId(value));
                sc.Property(s => s.AccessGroups)
                    .HasConversion(
                        groups => string.Join(",", groups),
                        value => value.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
            });

            entity.HasIndex(t => t.Name);
            entity.HasIndex(t => t.Category);

            // Composite index for template queries (PERFORMANCE FIX)
            entity.HasIndex(t => new { t.Category, t.IsActive })
                .HasDatabaseName("IX_Templates_Category_IsActive");
        });

        // Configure Version entity
        modelBuilder.Entity<DomainVersion>(entity =>
        {
            entity.ToTable("Versions");
            entity.HasKey(v => v.Id);
            
            entity.Property(v => v.DocumentId)
                .IsRequired();
                
            entity.Property(v => v.VersionNumber)
                .IsRequired()
                .HasMaxLength(50);
                
            entity.Property(v => v.Content)
                .HasMaxLength(int.MaxValue);

            entity.Property(v => v.SizeBytes)
                .IsRequired();

            entity.HasIndex(v => v.DocumentId);
            entity.HasIndex(v => v.VersionNumber);
            entity.HasIndex(v => v.CreatedAt);

            // Composite index for version queries (PERFORMANCE FIX)
            entity.HasIndex(v => new { v.DocumentId, v.VersionNumber })
                .HasDatabaseName("IX_Versions_Document_Version")
                .IsUnique();

            // Configure relationship with VersionApproval
            entity.HasMany(v => v.Approvals)
                .WithOne()
                .HasForeignKey("VersionId")
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure VersionApproval entity
        modelBuilder.Entity<VersionApproval>(entity =>
        {
            entity.ToTable("VersionApprovals");
            entity.HasKey(va => va.Id);
            
            entity.Property(va => va.Comments)
                .HasMaxLength(4000);

            entity.Property(va => va.VersionId)
                .IsRequired();
        });

        // Configure AuditLog entity
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(a => a.Id);
            
            entity.Property(a => a.Action)
                .IsRequired()
                .HasMaxLength(100);
                
            entity.Property(a => a.EntityType)
                .IsRequired()
                .HasMaxLength(100);
                
            entity.Property(a => a.EntityId)
                .IsRequired()
                .HasMaxLength(100);
                
            entity.Property(a => a.Description)
                .HasMaxLength(4000);

            entity.Property(a => a.OccurredAt)
                .IsRequired();

            entity.Property(a => a.IpAddress)
                .HasMaxLength(45); // IPv6 max length

            entity.Property(a => a.UserAgent)
                .HasMaxLength(500);

            entity.Property(a => a.SessionId)
                .HasMaxLength(100);

            // Configure Metadata as JSON
            entity.Property(a => a.Metadata)
                .HasConversion(
                    metadata => System.Text.Json.JsonSerializer.Serialize(metadata, (System.Text.Json.JsonSerializerOptions?)null),
                    json => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, object>());

            entity.HasIndex(a => a.EntityType);
            entity.HasIndex(a => a.EntityId);
            entity.HasIndex(a => a.OccurredAt);

            // Composite indexes for audit queries (PERFORMANCE FIX)
            entity.HasIndex(a => new { a.EntityType, a.EntityId, a.OccurredAt })
                .HasDatabaseName("IX_AuditLogs_Entity_Time");
            entity.HasIndex(a => new { a.UserId, a.OccurredAt })
                .HasDatabaseName("IX_AuditLogs_User_Time");
        });

        // Configure Agent entity
        modelBuilder.Entity<Agent>(entity =>
        {
            entity.ToTable("Agents");
            entity.HasKey(a => a.Id);
            
            entity.Property(a => a.Name)
                .IsRequired()
                .HasMaxLength(255);
                
            entity.Property(a => a.Description)
                .HasMaxLength(2000);
                
            entity.Property(a => a.Version)
                .IsRequired()
                .HasMaxLength(50);

            // Configure AgentType as enum
            entity.Property(a => a.Type)
                .HasConversion<string>();

            // Configure AgentStatus as enum
            entity.Property(a => a.Status)
                .HasConversion<string>();

            // Configure MaxSecurityClearance as enum
            entity.Property(a => a.MaxSecurityClearance)
                .HasConversion<string>();

            // Configure Capabilities as JSON
            entity.Property(a => a.Capabilities)
                .HasConversion(
                    capabilities => string.Join(",", capabilities.Select(c => c.ToString())),
                    value => value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                           .Select(s => Enum.Parse<AgentCapability>(s)).ToList());

            // Configure AgentConfiguration as owned entity
            entity.OwnsOne(a => a.Configuration, config =>
            {
                config.Property(c => c.Settings)
                    .HasConversion(
                        settings => System.Text.Json.JsonSerializer.Serialize(settings, (System.Text.Json.JsonSerializerOptions?)null),
                        json => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, object>());
                        
                config.Property(c => c.RequestTimeout);
                config.Property(c => c.RetryAttempts);
                config.Property(c => c.RetryDelay);
            });

            entity.HasIndex(a => a.Name);
            entity.HasIndex(a => a.Type);
            entity.HasIndex(a => a.Status);
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
            .Where(e => e.Entity.GetType().BaseType?.IsGenericType == true &&
                        e.Entity.GetType().BaseType?.GetGenericTypeDefinition() == typeof(BaseEntity<>) &&
                        (e.State == EntityState.Added || e.State == EntityState.Modified));

        foreach (var entry in entries)
        {
            var now = DateTime.UtcNow;

            if (entry.State == EntityState.Added)
            {
                entry.Property("CreatedAt").CurrentValue = now;
                entry.Property("ModifiedAt").CurrentValue = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property("ModifiedAt").CurrentValue = now;
                
                // Prevent modification of CreatedAt
                entry.Property("CreatedAt").IsModified = false;
            }
        }
    }

    /// <summary>
    /// Configures EF Core to ignore value object types so they are not treated as entities.
    /// </summary>
    private void IgnoreValueObjects(ModelBuilder modelBuilder)
    {
        // Ignore all strongly-typed ID value objects
        modelBuilder.Ignore<DocumentId>();
        modelBuilder.Ignore<UserId>();
        modelBuilder.Ignore<TemplateId>();
        modelBuilder.Ignore<VersionId>();
        modelBuilder.Ignore<AuditLogId>();
        modelBuilder.Ignore<AgentId>();
        modelBuilder.Ignore<VersionApprovalId>();
        
        // Ignore other value objects  
        modelBuilder.Ignore<SecurityClassification>();
        modelBuilder.Ignore<ApprovalStatus>();
        modelBuilder.Ignore<AgentConfiguration>();
        modelBuilder.Ignore<TemplateVariable>();
        modelBuilder.Ignore<UserPreferences>();
    }

    /// <summary>
    /// Configures value converters for strongly-typed ID value objects.
    /// </summary>
    private void ConfigureValueConverters(ModelBuilder modelBuilder)
    {
        // Configure explicit value converters for all strongly-typed IDs
        modelBuilder.Entity<Document>().Property(d => d.Id)
            .HasConversion(id => id.Value, value => new DocumentId(value));
        modelBuilder.Entity<Document>().Property(d => d.CreatedBy)
            .HasConversion(id => id.Value, value => new UserId(value));
        modelBuilder.Entity<Document>().Property(d => d.ModifiedBy)
            .HasConversion(id => id.Value, value => new UserId(value));
        modelBuilder.Entity<Document>().Property(d => d.TemplateId)
            .HasConversion(
                id => id != null ? id.Value : (Guid?)null,
                value => value.HasValue ? new TemplateId(value.Value) : null);
        
        modelBuilder.Entity<User>().Property(u => u.Id)
            .HasConversion(id => id.Value, value => new UserId(value));
        modelBuilder.Entity<User>().Property(u => u.CreatedBy)
            .HasConversion(id => id.Value, value => new UserId(value));
        modelBuilder.Entity<User>().Property(u => u.ModifiedBy)
            .HasConversion(id => id.Value, value => new UserId(value));
        
        modelBuilder.Entity<Template>().Property(t => t.Id)
            .HasConversion(id => id.Value, value => new TemplateId(value));
        modelBuilder.Entity<Template>().Property(t => t.CreatedBy)
            .HasConversion(id => id.Value, value => new UserId(value));
        modelBuilder.Entity<Template>().Property(t => t.ModifiedBy)
            .HasConversion(id => id.Value, value => new UserId(value));
        
        modelBuilder.Entity<DomainVersion>().Property(v => v.Id)
            .HasConversion(id => id.Value, value => new VersionId(value));
        modelBuilder.Entity<DomainVersion>().Property(v => v.DocumentId)
            .HasConversion(id => id.Value, value => new DocumentId(value));
        modelBuilder.Entity<DomainVersion>().Property(v => v.CreatedBy)
            .HasConversion(id => id.Value, value => new UserId(value));
        modelBuilder.Entity<DomainVersion>().Property(v => v.ModifiedBy)
            .HasConversion(id => id.Value, value => new UserId(value));
        
        modelBuilder.Entity<AuditLog>().Property(a => a.Id)
            .HasConversion(id => id.Value, value => new AuditLogId(value));
        modelBuilder.Entity<AuditLog>().Property(a => a.CreatedBy)
            .HasConversion(id => id.Value, value => new UserId(value));
        modelBuilder.Entity<AuditLog>().Property(a => a.ModifiedBy)
            .HasConversion(id => id.Value, value => new UserId(value));
        
        modelBuilder.Entity<Agent>().Property(a => a.Id)
            .HasConversion(id => id.Value, value => new AgentId(value));
        modelBuilder.Entity<Agent>().Property(a => a.CreatedBy)
            .HasConversion(id => id.Value, value => new UserId(value));
        modelBuilder.Entity<Agent>().Property(a => a.ModifiedBy)
            .HasConversion(id => id.Value, value => new UserId(value));
        
        modelBuilder.Entity<VersionApproval>().Property(va => va.Id)
            .HasConversion(id => id.Value, value => new VersionApprovalId(value));
        modelBuilder.Entity<VersionApproval>().Property(va => va.VersionId)
            .HasConversion(id => id.Value, value => new VersionId(value));
        modelBuilder.Entity<VersionApproval>().Property(va => va.CreatedBy)
            .HasConversion(id => id.Value, value => new UserId(value));
        modelBuilder.Entity<VersionApproval>().Property(va => va.ModifiedBy)
            .HasConversion(id => id.Value, value => new UserId(value));
    }
}
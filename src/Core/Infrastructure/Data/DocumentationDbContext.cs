using Enterprise.Documentation.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Core.Infrastructure.Data;

/// <summary>
/// Entity Framework database context for the Enterprise Documentation Platform
/// </summary>
public class DocumentationDbContext : DbContext
{
    public DocumentationDbContext(DbContextOptions<DocumentationDbContext> options) : base(options)
    {
    }

    // Master entity with 119 columns as specified in implementation guide
    public DbSet<MasterIndex> MasterIndexes { get; set; } = null!;
    
    // Approval workflow entities
    public DbSet<ApprovalEntity> Approvals { get; set; } = null!;
    
    // Excel change tracking
    public DbSet<ExcelChangeEntry> ExcelChanges { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure MasterIndex entity
        modelBuilder.Entity<MasterIndex>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            
            // Basic required fields that exist
            entity.Property(e => e.JiraNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ObjectName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.SchemaName).HasMaxLength(128).IsRequired();
            entity.Property(e => e.DatabaseName).HasMaxLength(128).IsRequired();
            entity.Property(e => e.PhysicalName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ObjectType).HasMaxLength(100).IsRequired();
            
            // Add basic indexes
            entity.HasIndex(e => e.JiraNumber);
            entity.HasIndex(e => new { e.SchemaName, e.ObjectName });
        });

        // Configure ApprovalEntity
        modelBuilder.Entity<ApprovalEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            
            entity.Property(e => e.JiraNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.DocumentType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ObjectName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.SchemaName).HasMaxLength(128).IsRequired();
            entity.Property(e => e.DocumentPath).HasMaxLength(1024);
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Priority).HasMaxLength(20).IsRequired();
            entity.Property(e => e.RequesterEmail).HasMaxLength(255);
            
            // Foreign key to MasterIndex
            entity.HasOne<MasterIndex>()
                .WithMany()
                .HasForeignKey(e => e.MetadataId)
                .OnDelete(DeleteBehavior.SetNull);
            
            // Indexes
            entity.HasIndex(e => e.JiraNumber);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedDate);
            entity.HasIndex(e => e.DueDate);
        });

        // Configure ExcelChangeEntry
        modelBuilder.Entity<ExcelChangeEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            
            entity.Property(e => e.JiraNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.DocumentType).HasMaxLength(100);
            entity.Property(e => e.ObjectName).HasMaxLength(255);
            entity.Property(e => e.SchemaName).HasMaxLength(128);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.RequesterEmail).HasMaxLength(255);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.Priority).HasMaxLength(20);
            entity.Property(e => e.BusinessJustification).HasMaxLength(2000);
            entity.Property(e => e.TechnicalNotes).HasMaxLength(2000);
            entity.Property(e => e.TestingNotes).HasMaxLength(2000);
            entity.Property(e => e.DeploymentNotes).HasMaxLength(2000);
            entity.Property(e => e.RollbackPlan).HasMaxLength(2000);
            entity.Property(e => e.EstimatedEffort).HasMaxLength(100);
            entity.Property(e => e.ActualEffort).HasMaxLength(100);
            entity.Property(e => e.AssignedTo).HasMaxLength(255);
            entity.Property(e => e.ReviewedBy).HasMaxLength(255);
            entity.Property(e => e.ApprovedBy).HasMaxLength(255);
            entity.Property(e => e.CompletedBy).HasMaxLength(255);
            
            // Indexes
            entity.HasIndex(e => e.JiraNumber).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedDate);
            entity.HasIndex(e => e.RequesterEmail);
        });
    }
}

/// <summary>
/// Database context factory for design-time operations
/// </summary>
public class DocumentationDbContextFactory : IDesignTimeDbContextFactory<DocumentationDbContext>
{
    public DocumentationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DocumentationDbContext>();
        
        // Use SQL Server with a default connection string for migrations
        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=EnterpriseDocumentationPlatform;Trusted_Connection=true;");

        return new DocumentationDbContext(optionsBuilder.Options);
    }
}
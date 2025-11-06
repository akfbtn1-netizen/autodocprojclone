using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Core.Domain.Entities;
using Core.Domain.ValueObjects;

namespace Core.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for the Document entity.
/// Configures the Document entity to match actual properties: Title, Description, Category, etc.
/// </summary>
public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    /// <summary>
    /// Configures the Document entity mapping.
    /// </summary>
    /// <param name="builder">Entity type builder</param>
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        // Table configuration
        builder.ToTable("Documents");
        builder.HasKey(d => d.Id);

        // Primary properties - using ACTUAL Document properties
        builder.Property(d => d.Id)
            .ValueGeneratedNever();

        builder.Property(d => d.Title)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(d => d.Description)
            .HasMaxLength(2000);

        builder.Property(d => d.Category)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.Version)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(d => d.ContentType)
            .IsRequired()
            .HasMaxLength(50);

        // Value object conversions
        builder.Property(d => d.PhysicalName)
            .HasConversion(
                physicalName => physicalName.Value,
                value => new PhysicalName(value))
            .HasMaxLength(255);

        builder.Property(d => d.ApprovalStatus)
            .HasConversion(
                status => status.Status.ToString(),
                value => ApprovalStatus.Draft) // TODO: Proper conversion needed
            .HasMaxLength(50);

        // Tags as JSON
        builder.Property(d => d.Tags)
            .HasConversion(
                tags => string.Join(",", tags),
                value => value.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList())
            .HasMaxLength(1000);

        // Audit properties (inherited from BaseEntity)
        builder.Property(d => d.CreatedAt)
            .IsRequired();

        builder.Property(d => d.UpdatedAt)
            .IsRequired();

        builder.Property(d => d.CreatedBy)
            .HasMaxLength(255);

        builder.Property(d => d.UpdatedBy)
            .HasMaxLength(255);

        // Soft delete
        builder.Property(d => d.IsDeleted)
            .HasDefaultValue(false);

        // Indexes - using ACTUAL properties
        builder.HasIndex(d => d.PhysicalName)
            .IsUnique()
            .HasDatabaseName("IX_Documents_PhysicalName");

        builder.HasIndex(d => d.Title)
            .HasDatabaseName("IX_Documents_Title");

        builder.HasIndex(d => d.Category)
            .HasDatabaseName("IX_Documents_Category");

        builder.HasIndex(d => d.ApprovalStatus)
            .HasDatabaseName("IX_Documents_ApprovalStatus");

        builder.HasIndex(d => d.CreatedAt)
            .HasDatabaseName("IX_Documents_CreatedAt");

        builder.HasIndex(d => d.IsDeleted)
            .HasDatabaseName("IX_Documents_IsDeleted");
    }
}

// Note: Only implementing Document configuration for now
// Other entity configurations (DocumentPermission, DocumentChange, etc.) 
// will be added once we verify the base structure works
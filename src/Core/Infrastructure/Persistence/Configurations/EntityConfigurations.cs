using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.ValueObjects;

namespace Core.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for the Document entity.
/// Configures the Document entity with proper value object conversions and indexes.
/// </summary>
public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    /// <summary>
    /// Configures the Document entity mapping.
    /// </summary>
    /// <param name="builder">Entity type builder</param>
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        ConfigureTableAndKeys(builder);
        ConfigureProperties(builder);
        ConfigureValueObjects(builder);
    }

    private static void ConfigureTableAndKeys(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents");
        builder.HasKey(d => d.Id);

        // Configure strongly-typed ID
        builder.Property(d => d.Id)
            .HasConversion(
                id => id.Value,
                value => new DocumentId(value))
            .ValueGeneratedNever();

        // Configure template ID (nullable strongly-typed ID)
        builder.Property(d => d.TemplateId)
            .HasConversion(
                id => id != null ? id.Value : (Guid?)null,
                value => value.HasValue ? new TemplateId(value.Value) : null);
    }

    private static void ConfigureProperties(EntityTypeBuilder<Document> builder)
    {
        builder.Property(d => d.Title).IsRequired().HasMaxLength(255);
        builder.Property(d => d.Description).HasMaxLength(1000);
        builder.Property(d => d.Category).IsRequired().HasMaxLength(100);
        builder.Property(d => d.DocumentVersion).IsRequired().HasMaxLength(20);
        builder.Property(d => d.ContentType).IsRequired().HasMaxLength(50);
        builder.Property(d => d.Content).HasColumnType("text");
    }

    private static void ConfigureValueObjects(EntityTypeBuilder<Document> builder)
    {
        // Tags as JSON array
        builder.Property(d => d.Tags)
            .HasConversion(
                tags => string.Join(",", tags),
                value => value.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());

        // Complex value objects - store as simple string for now
        builder.Property(d => d.ApprovalStatus)
            .HasConversion(
                status => status.Status,
                value => ParseApprovalStatus(value))
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(d => d.SecurityClassification)
            .HasConversion(
                classification => classification.Level,
                value => ParseSecurityClassification(value))
            .IsRequired()
            .HasMaxLength(50);

        // Indexes
        builder.HasIndex(d => d.Title)
            .HasDatabaseName("IX_Documents_Title");

        builder.HasIndex(d => d.Category)
            .HasDatabaseName("IX_Documents_Category");

        builder.HasIndex(d => d.CreatedAt)
            .HasDatabaseName("IX_Documents_CreatedAt");

        builder.HasIndex(d => d.DocumentVersion)
            .HasDatabaseName("IX_Documents_Version");

        builder.HasIndex(d => d.IsDeleted)
            .HasDatabaseName("IX_Documents_IsDeleted");
    }

    private static ApprovalStatus ParseApprovalStatus(string value)
    {
        // Simple parsing for now - in production would use proper serialization
        return value switch
        {
            "NotRequired" => ApprovalStatus.NotRequired(),
            "Pending" => ApprovalStatus.Pending(),
            "Approved" => ApprovalStatus.Approved(UserId.ForTesting()),
            "Rejected" => ApprovalStatus.Rejected(UserId.ForTesting()),
            _ => ApprovalStatus.Pending()
        };
    }

    private static SecurityClassification ParseSecurityClassification(string value)
    {
        // Simple parsing for now - in production would use proper serialization
        return value switch
        {
            "Public" => SecurityClassification.Public(UserId.ForTesting()),
            "Internal" => SecurityClassification.Internal(UserId.ForTesting()),
            "Confidential" => SecurityClassification.Confidential(UserId.ForTesting(), new List<string> { "Managers" }),
            "Restricted" => SecurityClassification.Restricted(UserId.ForTesting(), new List<string> { "Executives" }),
            _ => SecurityClassification.Internal(UserId.ForTesting())
        };
    }
}
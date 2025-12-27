using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Domain.Entities.Security;

namespace USP.Infrastructure.Persistence.Configurations.Security;

/// <summary>
/// Entity configuration for Policy
/// </summary>
public class PolicyConfiguration : IEntityTypeConfiguration<Policy>
{
    public void Configure(EntityTypeBuilder<Policy> builder)
    {
        builder.ToTable("policies");

        // Primary key
        builder.HasKey(p => p.Id);

        // Properties
        builder.Property(p => p.Id)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(p => p.Description)
            .HasMaxLength(1000);

        builder.Property(p => p.Type)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(p => p.Content)
            .HasColumnType("text");

        builder.Property(p => p.Effect)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("allow");

        builder.Property(p => p.Priority)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(p => p.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(p => p.IsSystemPolicy)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(p => p.Version)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(p => p.Metadata)
            .HasColumnType("jsonb");

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.Property(p => p.CreatedBy)
            .HasMaxLength(255);

        builder.Property(p => p.UpdatedAt)
            .IsRequired();

        builder.Property(p => p.UpdatedBy)
            .HasMaxLength(255);

        builder.Property(p => p.DeletedAt);

        // Indexes
        builder.HasIndex(p => p.Name)
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.HasIndex(p => p.Type);

        builder.HasIndex(p => p.IsActive);

        builder.HasIndex(p => p.Priority);

        builder.HasIndex(p => p.CreatedAt);

        builder.HasIndex(p => p.DeletedAt);
    }
}

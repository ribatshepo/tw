using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Domain.Entities.Security;

namespace USP.Infrastructure.Persistence.Configurations.Security;

/// <summary>
/// Entity configuration for AccessPolicy
/// </summary>
public class AccessPolicyConfiguration : IEntityTypeConfiguration<AccessPolicy>
{
    public void Configure(EntityTypeBuilder<AccessPolicy> builder)
    {
        builder.ToTable("access_policies");

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

        builder.Property(p => p.Effect)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("allow");

        builder.Property(p => p.Subjects)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("{}");

        builder.Property(p => p.Resources)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("{}");

        builder.Property(p => p.Actions)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("[]");

        builder.Property(p => p.Conditions)
            .HasColumnType("jsonb");

        builder.Property(p => p.Priority)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(p => p.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .IsRequired();

        builder.Property(p => p.DeletedAt);

        // Indexes
        builder.HasIndex(p => p.Name)
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.HasIndex(p => p.IsActive);

        builder.HasIndex(p => p.Priority);

        builder.HasIndex(p => p.CreatedAt);

        builder.HasIndex(p => p.DeletedAt);
    }
}

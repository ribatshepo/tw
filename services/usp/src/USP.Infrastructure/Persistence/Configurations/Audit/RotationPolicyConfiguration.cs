using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Domain.Entities.Audit;

namespace USP.Infrastructure.Persistence.Configurations.Audit;

/// <summary>
/// Entity configuration for RotationPolicy
/// </summary>
public class RotationPolicyConfiguration : IEntityTypeConfiguration<RotationPolicy>
{
    public void Configure(EntityTypeBuilder<RotationPolicy> builder)
    {
        builder.ToTable("rotation_policies");

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

        builder.Property(p => p.IntervalDays)
            .IsRequired();

        builder.Property(p => p.Enabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(p => p.CronSchedule)
            .HasMaxLength(100);

        builder.Property(p => p.Configuration)
            .HasColumnType("jsonb");

        builder.Property(p => p.LastExecutedAt);

        builder.Property(p => p.NextExecutionAt);

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .IsRequired();

        builder.Property(p => p.DeletedAt);

        // Indexes
        builder.HasIndex(p => p.Name)
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.HasIndex(p => p.Type);

        builder.Property(p => p.Enabled);

        builder.HasIndex(p => p.NextExecutionAt);

        builder.HasIndex(p => new { p.Enabled, p.NextExecutionAt });

        builder.HasIndex(p => p.CreatedAt);

        builder.HasIndex(p => p.DeletedAt);
    }
}

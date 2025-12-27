using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Domain.Entities.Audit;

namespace USP.Infrastructure.Persistence.Configurations.Audit;

/// <summary>
/// Entity configuration for RotationJob
/// </summary>
public class RotationJobConfiguration : IEntityTypeConfiguration<RotationJob>
{
    public void Configure(EntityTypeBuilder<RotationJob> builder)
    {
        builder.ToTable("rotation_jobs");

        // Primary key
        builder.HasKey(j => j.Id);

        // Properties
        builder.Property(j => j.Id)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(j => j.Type)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(j => j.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(j => j.TargetResource)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(j => j.TargetCredentialId)
            .HasMaxLength(255);

        builder.Property(j => j.PolicyId)
            .HasMaxLength(255);

        builder.Property(j => j.ErrorMessage)
            .HasColumnType("text");

        builder.Property(j => j.ScheduledAt)
            .IsRequired();

        builder.Property(j => j.StartedAt);

        builder.Property(j => j.CompletedAt);

        builder.Property(j => j.Duration);

        builder.Property(j => j.CreatedAt)
            .IsRequired();

        builder.Property(j => j.UpdatedAt)
            .IsRequired();

        // Indexes
        builder.HasIndex(j => j.Type);

        builder.HasIndex(j => j.Status);

        builder.HasIndex(j => j.TargetResource);

        builder.HasIndex(j => j.PolicyId);

        builder.HasIndex(j => j.ScheduledAt);

        builder.HasIndex(j => new { j.Status, j.ScheduledAt });

        builder.HasIndex(j => j.CreatedAt);
    }
}

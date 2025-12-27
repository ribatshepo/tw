using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Domain.Entities.Identity;

namespace USP.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity configuration for MFADevice
/// </summary>
public class MFADeviceConfiguration : IEntityTypeConfiguration<MFADevice>
{
    public void Configure(EntityTypeBuilder<MFADevice> builder)
    {
        builder.ToTable("MFADevices");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasMaxLength(36)
            .IsRequired();

        builder.Property(d => d.UserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(d => d.Method)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.DeviceName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(d => d.DeviceData)
            .HasColumnType("text");

        builder.Property(d => d.IsVerified)
            .IsRequired();

        builder.Property(d => d.IsPrimary)
            .IsRequired();

        builder.Property(d => d.UsageCount)
            .HasDefaultValue(0);

        builder.Property(d => d.LastUsedAt);

        builder.Property(d => d.EnrolledAt)
            .IsRequired();

        builder.Property(d => d.ExpiresAt);

        builder.Property(d => d.CreatedAt)
            .IsRequired();

        builder.Property(d => d.UpdatedAt)
            .IsRequired();

        builder.Property(d => d.DeletedAt);

        // Foreign key relationship
        builder.HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(d => d.UserId);
        builder.HasIndex(d => new { d.UserId, d.Method });
        builder.HasIndex(d => d.IsPrimary);
        builder.HasIndex(d => d.DeletedAt);
    }
}

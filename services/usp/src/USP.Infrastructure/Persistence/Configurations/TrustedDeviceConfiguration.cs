using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Domain.Entities.Identity;

namespace USP.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity configuration for TrustedDevice
/// </summary>
public class TrustedDeviceConfiguration : IEntityTypeConfiguration<TrustedDevice>
{
    public void Configure(EntityTypeBuilder<TrustedDevice> builder)
    {
        builder.ToTable("TrustedDevices");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasMaxLength(36)
            .IsRequired();

        builder.Property(d => d.UserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(d => d.DeviceFingerprint)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(d => d.DeviceName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(d => d.DeviceType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.UserAgent)
            .HasMaxLength(500);

        builder.Property(d => d.IpAddress)
            .HasMaxLength(45)
            .IsRequired();

        builder.Property(d => d.Location)
            .HasMaxLength(255);

        builder.Property(d => d.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(d => d.UsageCount)
            .HasDefaultValue(0);

        builder.Property(d => d.LastUsedAt);

        builder.Property(d => d.TrustedAt)
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
        builder.HasIndex(d => new { d.UserId, d.DeviceFingerprint });
        builder.HasIndex(d => d.IsActive);
        builder.HasIndex(d => d.ExpiresAt);
        builder.HasIndex(d => d.DeletedAt);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Domain.Entities.Identity;

namespace USP.Infrastructure.Persistence.Configurations.Identity;

/// <summary>
/// Entity configuration for TrustedDevice
/// </summary>
public class TrustedDeviceConfiguration : IEntityTypeConfiguration<TrustedDevice>
{
    public void Configure(EntityTypeBuilder<TrustedDevice> builder)
    {
        builder.ToTable("trusted_devices");

        // Primary key
        builder.HasKey(d => d.Id);

        // Properties
        builder.Property(d => d.Id)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(d => d.UserId)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(d => d.DeviceFingerprint)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(d => d.DeviceName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(d => d.DeviceType)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(d => d.UserAgent)
            .HasMaxLength(500);

        builder.Property(d => d.IpAddress)
            .IsRequired()
            .HasMaxLength(45);

        builder.Property(d => d.Location)
            .HasMaxLength(255);

        builder.Property(d => d.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(d => d.UsageCount)
            .IsRequired()
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

        // Indexes
        builder.HasIndex(d => d.UserId);

        builder.HasIndex(d => d.DeviceFingerprint);

        builder.HasIndex(d => new { d.UserId, d.DeviceFingerprint })
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.HasIndex(d => d.IsActive);

        builder.HasIndex(d => d.ExpiresAt);

        builder.HasIndex(d => d.CreatedAt);

        builder.HasIndex(d => d.DeletedAt);

        // Navigation properties
        builder.HasOne(d => d.User)
            .WithMany(u => u.TrustedDevices)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

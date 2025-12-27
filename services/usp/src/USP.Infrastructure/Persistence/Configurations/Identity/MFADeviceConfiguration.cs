using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Domain.Entities.Identity;

namespace USP.Infrastructure.Persistence.Configurations.Identity;

/// <summary>
/// Entity configuration for MFADevice
/// </summary>
public class MFADeviceConfiguration : IEntityTypeConfiguration<MFADevice>
{
    public void Configure(EntityTypeBuilder<MFADevice> builder)
    {
        builder.ToTable("mfa_devices");

        // Primary key
        builder.HasKey(d => d.Id);

        // Properties
        builder.Property(d => d.Id)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(d => d.UserId)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(d => d.Method)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(d => d.DeviceName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(d => d.DeviceData)
            .HasColumnType("text");

        builder.Property(d => d.IsVerified)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(d => d.IsPrimary)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(d => d.UsageCount)
            .IsRequired()
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

        // Indexes
        builder.HasIndex(d => d.UserId);

        builder.HasIndex(d => d.Method);

        builder.HasIndex(d => new { d.UserId, d.IsPrimary });

        builder.HasIndex(d => d.CreatedAt);

        builder.HasIndex(d => d.DeletedAt);

        // Navigation properties
        builder.HasOne(d => d.User)
            .WithMany(u => u.MFADevices)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

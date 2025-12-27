using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Domain.Entities.Identity;

namespace USP.Infrastructure.Persistence.Configurations.Identity;

/// <summary>
/// Entity configuration for ApplicationUser
/// </summary>
public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("users");

        // Properties
        builder.Property(u => u.FirstName)
            .HasMaxLength(255);

        builder.Property(u => u.LastName)
            .HasMaxLength(255);

        builder.Property(u => u.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(u => u.MfaEnabled)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(u => u.MfaSecret)
            .HasMaxLength(255);

        builder.Property(u => u.FailedLoginAttempts)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(u => u.LastFailedLogin);

        builder.Property(u => u.LockedUntil);

        builder.Property(u => u.PasswordChangedAt)
            .IsRequired();

        builder.Property(u => u.RiskScore)
            .IsRequired()
            .HasPrecision(5, 2)
            .HasDefaultValue(0);

        builder.Property(u => u.RiskScoreUpdatedAt);

        builder.Property(u => u.RequireReauthentication)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(u => u.LastLoginAt);

        builder.Property(u => u.LastLoginIp)
            .HasMaxLength(45);

        builder.Property(u => u.LastLoginLocation)
            .HasMaxLength(255);

        builder.Property(u => u.MaxConcurrentSessions);

        builder.Property(u => u.Metadata)
            .HasColumnType("jsonb");

        builder.Property(u => u.CreatedAt)
            .IsRequired();

        builder.Property(u => u.UpdatedAt)
            .IsRequired();

        builder.Property(u => u.DeletedAt);

        // Indexes
        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.HasIndex(u => u.UserName)
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.HasIndex(u => u.Status);

        builder.HasIndex(u => u.RiskScore);

        builder.HasIndex(u => u.CreatedAt);

        builder.HasIndex(u => u.DeletedAt);

        // Navigation properties
        builder.HasMany(u => u.MFADevices)
            .WithOne(d => d.User)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.TrustedDevices)
            .WithOne(d => d.User)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.Sessions)
            .WithOne(s => s.User)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ignore computed properties
        builder.Ignore(u => u.FullName);
    }
}

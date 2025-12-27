using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Domain.Entities.Identity;

namespace USP.Infrastructure.Persistence.Configurations.Identity;

/// <summary>
/// Entity configuration for Session
/// </summary>
public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions");

        // Primary key
        builder.HasKey(s => s.Id);

        // Properties
        builder.Property(s => s.Id)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(s => s.UserId)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(s => s.AccessToken)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(s => s.RefreshToken)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(s => s.AccessTokenExpiresAt)
            .IsRequired();

        builder.Property(s => s.RefreshTokenExpiresAt)
            .IsRequired();

        builder.Property(s => s.IpAddress)
            .IsRequired()
            .HasMaxLength(45);

        builder.Property(s => s.UserAgent)
            .HasMaxLength(500);

        builder.Property(s => s.DeviceFingerprint)
            .HasMaxLength(255);

        builder.Property(s => s.Location)
            .HasMaxLength(255);

        builder.Property(s => s.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(s => s.LastActivityAt)
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.ExpiresAt)
            .IsRequired();

        builder.Property(s => s.RevokedAt);

        builder.Property(s => s.RevocationReason)
            .HasMaxLength(255);

        builder.Property(s => s.Metadata)
            .HasColumnType("jsonb");

        // Indexes
        builder.HasIndex(s => s.UserId);

        builder.HasIndex(s => s.IsActive);

        builder.HasIndex(s => s.ExpiresAt);

        builder.HasIndex(s => s.LastActivityAt);

        builder.HasIndex(s => s.RevokedAt);

        builder.HasIndex(s => s.CreatedAt);

        builder.HasIndex(s => new { s.UserId, s.IsActive });

        // Navigation properties
        builder.HasOne(s => s.User)
            .WithMany(u => u.Sessions)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ignore computed properties
        builder.Ignore(s => s.IsRevoked);
    }
}

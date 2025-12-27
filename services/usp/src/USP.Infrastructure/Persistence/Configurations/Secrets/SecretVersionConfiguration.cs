using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Domain.Entities.Secrets;

namespace USP.Infrastructure.Persistence.Configurations.Secrets;

/// <summary>
/// Entity configuration for SecretVersion
/// </summary>
public class SecretVersionConfiguration : IEntityTypeConfiguration<SecretVersion>
{
    public void Configure(EntityTypeBuilder<SecretVersion> builder)
    {
        builder.ToTable("secret_versions");

        // Primary key
        builder.HasKey(v => v.Id);

        // Properties
        builder.Property(v => v.Id)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(v => v.SecretId)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(v => v.Version)
            .IsRequired();

        builder.Property(v => v.EncryptedData)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(v => v.EncryptionKeyId)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(v => v.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(v => v.IsDestroyed)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(v => v.TimeToLive);

        builder.Property(v => v.ExpiresAt);

        builder.Property(v => v.CreatedAt)
            .IsRequired();

        builder.Property(v => v.CreatedBy)
            .HasMaxLength(255);

        builder.Property(v => v.DeletedAt);

        builder.Property(v => v.DestroyedAt);

        // Indexes
        builder.HasIndex(v => v.SecretId);

        builder.HasIndex(v => new { v.SecretId, v.Version })
            .IsUnique();

        builder.HasIndex(v => v.IsDeleted);

        builder.HasIndex(v => v.IsDestroyed);

        builder.HasIndex(v => v.ExpiresAt);

        builder.HasIndex(v => v.CreatedAt);

        // Navigation properties
        builder.HasOne(v => v.Secret)
            .WithMany(s => s.Versions)
            .HasForeignKey(v => v.SecretId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

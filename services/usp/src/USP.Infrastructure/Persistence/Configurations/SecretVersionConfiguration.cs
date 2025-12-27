using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Domain.Entities.Secrets;

namespace USP.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity configuration for SecretVersion
/// </summary>
public class SecretVersionConfiguration : IEntityTypeConfiguration<SecretVersion>
{
    public void Configure(EntityTypeBuilder<SecretVersion> builder)
    {
        builder.ToTable("SecretVersions");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id)
            .HasMaxLength(36)
            .IsRequired();

        builder.Property(v => v.SecretId)
            .HasMaxLength(36)
            .IsRequired();

        builder.Property(v => v.Version)
            .IsRequired();

        builder.Property(v => v.EncryptedData)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(v => v.EncryptionKeyId)
            .HasMaxLength(36)
            .IsRequired();

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
            .HasMaxLength(450);

        builder.Property(v => v.DeletedAt);

        builder.Property(v => v.DestroyedAt);

        // Relationships configured in SecretConfiguration

        // Indexes
        builder.HasIndex(v => v.SecretId);
        builder.HasIndex(v => new { v.SecretId, v.Version })
            .IsUnique();
        builder.HasIndex(v => v.IsDeleted);
        builder.HasIndex(v => v.IsDestroyed);
        builder.HasIndex(v => v.ExpiresAt);
        builder.HasIndex(v => v.CreatedAt);
    }
}

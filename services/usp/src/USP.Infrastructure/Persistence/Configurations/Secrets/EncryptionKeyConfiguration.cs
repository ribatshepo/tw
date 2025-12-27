using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Domain.Entities.Secrets;

namespace USP.Infrastructure.Persistence.Configurations.Secrets;

/// <summary>
/// Entity configuration for EncryptionKey
/// </summary>
public class EncryptionKeyConfiguration : IEntityTypeConfiguration<EncryptionKey>
{
    public void Configure(EntityTypeBuilder<EncryptionKey> builder)
    {
        builder.ToTable("encryption_keys");

        // Primary key
        builder.HasKey(k => k.Id);

        // Properties
        builder.Property(k => k.Id)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(k => k.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(k => k.Algorithm)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(k => k.CurrentVersion)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(k => k.MinDecryptionVersion)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(k => k.AllowPlaintextBackup)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(k => k.Exportable)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(k => k.DeletionAllowed)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(k => k.ConvergentEncryption)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(k => k.CreatedAt)
            .IsRequired();

        builder.Property(k => k.UpdatedAt)
            .IsRequired();

        builder.Property(k => k.DeletedAt);

        // Indexes
        builder.HasIndex(k => k.Name)
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.HasIndex(k => k.Algorithm);

        builder.HasIndex(k => k.CreatedAt);

        builder.HasIndex(k => k.DeletedAt);
    }
}

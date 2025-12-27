using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Domain.Entities.Secrets;

namespace USP.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity configuration for EncryptionKey
/// </summary>
public class EncryptionKeyConfiguration : IEntityTypeConfiguration<EncryptionKey>
{
    public void Configure(EntityTypeBuilder<EncryptionKey> builder)
    {
        builder.ToTable("EncryptionKeys");

        builder.HasKey(k => k.Id);

        builder.Property(k => k.Id)
            .HasMaxLength(36)
            .IsRequired();

        builder.Property(k => k.Name)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(k => k.Algorithm)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

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
            .IsUnique();
        builder.HasIndex(k => k.DeletedAt);
        builder.HasIndex(k => k.CreatedAt);
    }
}

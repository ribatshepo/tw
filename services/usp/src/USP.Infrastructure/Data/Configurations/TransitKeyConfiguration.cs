using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Models.Entities;

namespace USP.Infrastructure.Data.Configurations;

public class TransitKeyConfiguration : IEntityTypeConfiguration<TransitKey>
{
    public void Configure(EntityTypeBuilder<TransitKey> builder)
    {
        builder.ToTable("transit_keys");
        builder.HasKey(tk => tk.Id);

        builder.Property(tk => tk.Id).HasColumnName("id");
        builder.Property(tk => tk.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(tk => tk.Type).HasColumnName("type").HasMaxLength(50).IsRequired();
        builder.Property(tk => tk.LatestVersion).HasColumnName("latest_version");
        builder.Property(tk => tk.MinDecryptionVersion).HasColumnName("min_decryption_version");
        builder.Property(tk => tk.MinEncryptionVersion).HasColumnName("min_encryption_version");
        builder.Property(tk => tk.DeletionAllowed).HasColumnName("deletion_allowed");
        builder.Property(tk => tk.Exportable).HasColumnName("exportable");
        builder.Property(tk => tk.AllowPlaintextBackup).HasColumnName("allow_plaintext_backup");
        builder.Property(tk => tk.ConvergentEncryption).HasColumnName("convergent_encryption");
        builder.Property(tk => tk.ConvergentVersion).HasColumnName("convergent_version");
        builder.Property(tk => tk.Derived).HasColumnName("derived");
        builder.Property(tk => tk.EncryptionCount).HasColumnName("encryption_count");
        builder.Property(tk => tk.DecryptionCount).HasColumnName("decryption_count");
        builder.Property(tk => tk.SigningCount).HasColumnName("signing_count");
        builder.Property(tk => tk.VerificationCount).HasColumnName("verification_count");
        builder.Property(tk => tk.CreatedBy).HasColumnName("created_by");
        builder.Property(tk => tk.CreatedAt).HasColumnName("created_at");
        builder.Property(tk => tk.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(tk => tk.Creator)
            .WithMany()
            .HasForeignKey(tk => tk.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(tk => tk.Name).HasDatabaseName("idx_transit_keys_name").IsUnique();
        builder.HasIndex(tk => tk.CreatedBy).HasDatabaseName("idx_transit_keys_created_by");
    }
}

public class TransitKeyVersionConfiguration : IEntityTypeConfiguration<TransitKeyVersion>
{
    public void Configure(EntityTypeBuilder<TransitKeyVersion> builder)
    {
        builder.ToTable("transit_key_versions");
        builder.HasKey(tkv => tkv.Id);

        builder.Property(tkv => tkv.Id).HasColumnName("id");
        builder.Property(tkv => tkv.TransitKeyId).HasColumnName("transit_key_id");
        builder.Property(tkv => tkv.Version).HasColumnName("version");
        builder.Property(tkv => tkv.EncryptedKeyMaterial).HasColumnName("encrypted_key_material").IsRequired();
        builder.Property(tkv => tkv.PublicKey).HasColumnName("public_key");
        builder.Property(tkv => tkv.CreatedAt).HasColumnName("created_at");
        builder.Property(tkv => tkv.CreatedBy).HasColumnName("created_by");
        builder.Property(tkv => tkv.ArchivedAt).HasColumnName("archived_at");
        builder.Property(tkv => tkv.DestroyedAt).HasColumnName("destroyed_at");

        builder.HasOne(tkv => tkv.TransitKey)
            .WithMany(tk => tk.Versions)
            .HasForeignKey(tkv => tkv.TransitKeyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tkv => tkv.Creator)
            .WithMany()
            .HasForeignKey(tkv => tkv.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(tkv => tkv.TransitKeyId).HasDatabaseName("idx_transit_key_versions_transit_key_id");
        builder.HasIndex(tkv => new { tkv.TransitKeyId, tkv.Version })
            .HasDatabaseName("idx_transit_key_versions_transit_key_id_version")
            .IsUnique();
    }
}

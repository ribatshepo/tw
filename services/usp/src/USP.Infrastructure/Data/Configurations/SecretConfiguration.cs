using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Models.Entities;

namespace USP.Infrastructure.Data.Configurations;

public class SecretConfiguration : IEntityTypeConfiguration<Secret>
{
    public void Configure(EntityTypeBuilder<Secret> builder)
    {
        builder.ToTable("secrets");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id");

        builder.Property(s => s.Path)
            .HasColumnName("path")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(s => s.EncryptedValue)
            .HasColumnName("encrypted_value")
            .IsRequired();

        builder.Property(s => s.EncryptionKeyVersion)
            .HasColumnName("encryption_key_version");

        builder.Property(s => s.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb");

        builder.Property(s => s.Version)
            .HasColumnName("version");

        builder.Property(s => s.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(s => s.DeletedAt)
            .HasColumnName("deleted_at");

        // Relationships
        builder.HasOne(s => s.Creator)
            .WithMany(u => u.CreatedSecrets)
            .HasForeignKey(s => s.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(s => s.Path).HasDatabaseName("idx_secrets_path").IsUnique();
        builder.HasIndex(s => s.CreatedBy).HasDatabaseName("idx_secrets_created_by");
    }
}

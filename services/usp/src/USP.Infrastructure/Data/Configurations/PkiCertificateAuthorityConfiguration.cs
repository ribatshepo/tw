using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Models.Entities;

namespace USP.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework Core configuration for PkiCertificateAuthority entity
/// Defines database schema with snake_case naming convention
/// </summary>
public class PkiCertificateAuthorityConfiguration : IEntityTypeConfiguration<PkiCertificateAuthority>
{
    public void Configure(EntityTypeBuilder<PkiCertificateAuthority> builder)
    {
        builder.ToTable("pki_certificate_authorities");

        // Primary Key
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("id");

        // Unique Name
        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(255)
            .HasColumnName("name");

        builder.HasIndex(e => e.Name)
            .IsUnique()
            .HasDatabaseName("idx_pki_cas_name");

        // Type
        builder.Property(e => e.Type)
            .IsRequired()
            .HasMaxLength(50)
            .HasColumnName("type");

        // Subject DN
        builder.Property(e => e.SubjectDn)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnName("subject_dn");

        // Certificate PEM
        builder.Property(e => e.CertificatePem)
            .IsRequired()
            .HasColumnType("text")
            .HasColumnName("certificate_pem");

        // Serial Number
        builder.Property(e => e.SerialNumber)
            .IsRequired()
            .HasMaxLength(255)
            .HasColumnName("serial_number");

        // Encrypted Private Key (CRITICAL: Never expose in queries)
        builder.Property(e => e.EncryptedPrivateKey)
            .IsRequired()
            .HasColumnType("text")
            .HasColumnName("encrypted_private_key");

        // Key Type
        builder.Property(e => e.KeyType)
            .IsRequired()
            .HasMaxLength(50)
            .HasColumnName("key_type");

        // Validity Dates
        builder.Property(e => e.NotBefore)
            .IsRequired()
            .HasColumnName("not_before");

        builder.Property(e => e.NotAfter)
            .IsRequired()
            .HasColumnName("not_after");

        // Path Length
        builder.Property(e => e.MaxPathLength)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("max_path_length");

        // CA Hierarchy
        builder.Property(e => e.ParentCaId)
            .HasColumnName("parent_ca_id");

        builder.HasOne(e => e.ParentCa)
            .WithMany()
            .HasForeignKey(e => e.ParentCaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.ParentCaId)
            .HasDatabaseName("idx_pki_cas_parent_ca_id");

        // Status
        builder.Property(e => e.Revoked)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("revoked");

        builder.Property(e => e.RevokedAt)
            .HasColumnName("revoked_at");

        // Statistics
        builder.Property(e => e.IssuedCertificateCount)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("issued_certificate_count");

        // Audit Fields
        builder.Property(e => e.CreatedBy)
            .IsRequired()
            .HasColumnName("created_by");

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at");

        // Navigation - Issued Certificates
        builder.HasMany(e => e.IssuedCertificates)
            .WithOne(c => c.CertificateAuthority)
            .HasForeignKey(c => c.CertificateAuthorityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

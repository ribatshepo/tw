using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Models.Entities;

namespace USP.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework Core configuration for PkiIssuedCertificate entity
/// Defines database schema with snake_case naming convention
/// </summary>
public class PkiIssuedCertificateConfiguration : IEntityTypeConfiguration<PkiIssuedCertificate>
{
    public void Configure(EntityTypeBuilder<PkiIssuedCertificate> builder)
    {
        builder.ToTable("pki_issued_certificates");

        // Primary Key
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("id");

        // Unique Serial Number
        builder.Property(e => e.SerialNumber)
            .IsRequired()
            .HasMaxLength(255)
            .HasColumnName("serial_number");

        builder.HasIndex(e => e.SerialNumber)
            .IsUnique()
            .HasDatabaseName("idx_pki_issued_certs_serial");

        // Certificate Authority
        builder.Property(e => e.CertificateAuthorityId)
            .IsRequired()
            .HasColumnName("certificate_authority_id");

        builder.HasOne(e => e.CertificateAuthority)
            .WithMany(ca => ca.IssuedCertificates)
            .HasForeignKey(e => e.CertificateAuthorityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.CertificateAuthorityId)
            .HasDatabaseName("idx_pki_issued_certs_ca_id");

        // Role (optional - null if signed from CSR without role)
        builder.Property(e => e.RoleId)
            .HasColumnName("role_id");

        builder.HasOne(e => e.Role)
            .WithMany()
            .HasForeignKey(e => e.RoleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => e.RoleId)
            .HasDatabaseName("idx_pki_issued_certs_role_id");

        // Certificate Details
        builder.Property(e => e.SubjectDn)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnName("subject_dn");

        builder.Property(e => e.CertificatePem)
            .IsRequired()
            .HasColumnType("text")
            .HasColumnName("certificate_pem");

        builder.Property(e => e.NotBefore)
            .IsRequired()
            .HasColumnName("not_before");

        builder.Property(e => e.NotAfter)
            .IsRequired()
            .HasColumnName("not_after");

        // Revocation
        builder.Property(e => e.Revoked)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("revoked");

        builder.Property(e => e.RevokedAt)
            .HasColumnName("revoked_at");

        builder.HasIndex(e => e.Revoked)
            .HasDatabaseName("idx_pki_issued_certs_revoked");

        // Audit Fields
        builder.Property(e => e.IssuedBy)
            .IsRequired()
            .HasColumnName("issued_by");

        builder.Property(e => e.IssuedAt)
            .IsRequired()
            .HasColumnName("issued_at");
    }
}

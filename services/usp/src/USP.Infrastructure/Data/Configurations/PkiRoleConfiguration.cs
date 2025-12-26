using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Models.Entities;

namespace USP.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework Core configuration for PkiRole entity
/// Defines database schema with snake_case naming convention
/// </summary>
public class PkiRoleConfiguration : IEntityTypeConfiguration<PkiRole>
{
    public void Configure(EntityTypeBuilder<PkiRole> builder)
    {
        builder.ToTable("pki_roles");

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
            .HasDatabaseName("idx_pki_roles_name");

        // Certificate Authority
        builder.Property(e => e.CertificateAuthorityId)
            .IsRequired()
            .HasColumnName("certificate_authority_id");

        builder.HasOne(e => e.CertificateAuthority)
            .WithMany()
            .HasForeignKey(e => e.CertificateAuthorityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.CertificateAuthorityId)
            .HasDatabaseName("idx_pki_roles_ca_id");

        // Certificate Configuration
        builder.Property(e => e.KeyType)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("rsa-2048")
            .HasColumnName("key_type");

        builder.Property(e => e.TtlDays)
            .IsRequired()
            .HasDefaultValue(365)
            .HasColumnName("ttl_days");

        builder.Property(e => e.MaxTtlDays)
            .IsRequired()
            .HasDefaultValue(3650)
            .HasColumnName("max_ttl_days");

        builder.Property(e => e.AllowLocalhost)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnName("allow_localhost");

        builder.Property(e => e.AllowBareDomains)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("allow_bare_domains");

        builder.Property(e => e.AllowSubdomains)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("allow_subdomains");

        builder.Property(e => e.AllowWildcards)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("allow_wildcards");

        builder.Property(e => e.AllowIpSans)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnName("allow_ip_sans");

        // Allowed Domains (JSON)
        builder.Property(e => e.AllowedDomains)
            .IsRequired()
            .HasColumnType("text")
            .HasDefaultValue("[]")
            .HasColumnName("allowed_domains");

        // Key Usage Extensions
        builder.Property(e => e.ServerAuth)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnName("server_auth");

        builder.Property(e => e.ClientAuth)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("client_auth");

        builder.Property(e => e.CodeSigning)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("code_signing");

        builder.Property(e => e.EmailProtection)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("email_protection");

        // Audit Fields
        builder.Property(e => e.CreatedBy)
            .IsRequired()
            .HasColumnName("created_by");

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");
    }
}

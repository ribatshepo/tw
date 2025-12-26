using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Models.Entities;

namespace USP.Infrastructure.Data.Configurations;

public class SamlIdentityProviderConfiguration : IEntityTypeConfiguration<SamlIdentityProvider>
{
    public void Configure(EntityTypeBuilder<SamlIdentityProvider> builder)
    {
        builder.ToTable("saml_identity_providers");

        builder.HasKey(idp => idp.Id);

        builder.Property(idp => idp.Id)
            .HasColumnName("id");

        builder.Property(idp => idp.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(idp => idp.EntityId)
            .HasColumnName("entity_id")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(idp => idp.SsoServiceUrl)
            .HasColumnName("sso_service_url")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(idp => idp.SloServiceUrl)
            .HasColumnName("slo_service_url")
            .HasMaxLength(1000);

        builder.Property(idp => idp.SigningCertificate)
            .HasColumnName("signing_certificate")
            .IsRequired();

        builder.Property(idp => idp.MetadataXml)
            .HasColumnName("metadata_xml");

        builder.Property(idp => idp.SignAuthnRequests)
            .HasColumnName("sign_authn_requests");

        builder.Property(idp => idp.RequireSignedAssertions)
            .HasColumnName("require_signed_assertions");

        builder.Property(idp => idp.EnableJitProvisioning)
            .HasColumnName("enable_jit_provisioning");

        builder.Property(idp => idp.EmailAttributeName)
            .HasColumnName("email_attribute_name")
            .HasMaxLength(255);

        builder.Property(idp => idp.FirstNameAttributeName)
            .HasColumnName("first_name_attribute_name")
            .HasMaxLength(255);

        builder.Property(idp => idp.LastNameAttributeName)
            .HasColumnName("last_name_attribute_name")
            .HasMaxLength(255);

        builder.Property(idp => idp.GroupsAttributeName)
            .HasColumnName("groups_attribute_name")
            .HasMaxLength(255);

        builder.Property(idp => idp.RoleMapping)
            .HasColumnName("role_mapping")
            .HasColumnType("jsonb");

        builder.Property(idp => idp.DefaultRoleId)
            .HasColumnName("default_role_id");

        builder.Property(idp => idp.IsEnabled)
            .HasColumnName("is_enabled");

        builder.Property(idp => idp.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(idp => idp.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(idp => idp.UpdatedAt)
            .HasColumnName("updated_at");

        // Relationships
        builder.HasOne(idp => idp.Creator)
            .WithMany()
            .HasForeignKey(idp => idp.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(idp => idp.DefaultRole)
            .WithMany()
            .HasForeignKey(idp => idp.DefaultRoleId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(idp => idp.Name)
            .IsUnique()
            .HasDatabaseName("idx_saml_idps_name");

        builder.HasIndex(idp => idp.EntityId)
            .IsUnique()
            .HasDatabaseName("idx_saml_idps_entity_id");

        builder.HasIndex(idp => idp.IsEnabled)
            .HasDatabaseName("idx_saml_idps_is_enabled");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Models.Entities;

namespace USP.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for BusinessAssociateAgreement entity
/// </summary>
public class BusinessAssociateAgreementConfiguration : IEntityTypeConfiguration<BusinessAssociateAgreement>
{
    public void Configure(EntityTypeBuilder<BusinessAssociateAgreement> builder)
    {
        builder.ToTable("business_associate_agreements");
        builder.HasKey(baa => baa.Id);

        builder.Property(baa => baa.Id).HasColumnName("id");
        builder.Property(baa => baa.PartnerId).HasColumnName("partner_id").HasMaxLength(255).IsRequired();
        builder.Property(baa => baa.PartnerName).HasColumnName("partner_name").HasMaxLength(500).IsRequired();
        builder.Property(baa => baa.PartnerContactEmail).HasColumnName("partner_contact_email").HasMaxLength(255);
        builder.Property(baa => baa.PartnerContactPhone).HasColumnName("partner_contact_phone").HasMaxLength(50);
        builder.Property(baa => baa.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
        builder.Property(baa => baa.EffectiveDate).HasColumnName("effective_date").IsRequired();
        builder.Property(baa => baa.ExpirationDate).HasColumnName("expiration_date").IsRequired();
        builder.Property(baa => baa.DocumentUrl).HasColumnName("document_url").HasMaxLength(2000);
        builder.Property(baa => baa.DocumentPath).HasColumnName("document_path").HasMaxLength(1000);
        builder.Property(baa => baa.DocumentHash).HasColumnName("document_hash").HasMaxLength(128);
        builder.Property(baa => baa.LastReviewedAt).HasColumnName("last_reviewed_at");
        builder.Property(baa => baa.LastReviewedBy).HasColumnName("last_reviewed_by");
        builder.Property(baa => baa.ReviewNotes).HasColumnName("review_notes");
        builder.Property(baa => baa.ServicesProvided).HasColumnName("services_provided");
        builder.Property(baa => baa.PhiCategories).HasColumnName("phi_categories");
        builder.Property(baa => baa.RequiresAnnualReview).HasColumnName("requires_annual_review").IsRequired();
        builder.Property(baa => baa.NotifyDaysBeforeExpiration).HasColumnName("notify_days_before_expiration").IsRequired();
        builder.Property(baa => baa.RenewalRequestedAt).HasColumnName("renewal_requested_at");
        builder.Property(baa => baa.RenewalCompletedAt).HasColumnName("renewal_completed_at");
        builder.Property(baa => baa.ComplianceNotes).HasColumnName("compliance_notes");
        builder.Property(baa => baa.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(baa => baa.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasOne(baa => baa.LastReviewedByUser)
            .WithMany()
            .HasForeignKey(baa => baa.LastReviewedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(baa => baa.PartnerId).HasDatabaseName("idx_baa_partner_id");
        builder.HasIndex(baa => baa.PartnerName).HasDatabaseName("idx_baa_partner_name");
        builder.HasIndex(baa => baa.Status).HasDatabaseName("idx_baa_status");
        builder.HasIndex(baa => baa.ExpirationDate).HasDatabaseName("idx_baa_expiration_date");
        builder.HasIndex(baa => new { baa.Status, baa.ExpirationDate })
            .HasDatabaseName("idx_baa_status_expiration");
    }
}

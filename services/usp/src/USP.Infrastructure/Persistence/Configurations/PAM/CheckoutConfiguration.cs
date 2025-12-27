using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Domain.Entities.PAM;

namespace USP.Infrastructure.Persistence.Configurations.PAM;

/// <summary>
/// Entity configuration for Checkout
/// </summary>
public class CheckoutConfiguration : IEntityTypeConfiguration<Checkout>
{
    public void Configure(EntityTypeBuilder<Checkout> builder)
    {
        builder.ToTable("checkouts");

        // Primary key
        builder.HasKey(c => c.Id);

        // Properties
        builder.Property(c => c.Id)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(c => c.AccountId)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(c => c.UserId)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(c => c.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(c => c.Reason)
            .HasMaxLength(1000);

        builder.Property(c => c.RequestedAt)
            .IsRequired();

        builder.Property(c => c.ApprovedAt);

        builder.Property(c => c.ApprovedBy)
            .HasMaxLength(255);

        builder.Property(c => c.CheckedOutAt);

        builder.Property(c => c.CheckedInAt);

        builder.Property(c => c.ExpiresAt);

        builder.Property(c => c.PasswordRotatedOnCheckin)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .IsRequired();

        // Indexes
        builder.HasIndex(c => c.AccountId);

        builder.HasIndex(c => c.UserId);

        builder.HasIndex(c => c.Status);

        builder.HasIndex(c => new { c.AccountId, c.Status });

        builder.HasIndex(c => c.ExpiresAt);

        builder.HasIndex(c => c.RequestedAt);

        builder.HasIndex(c => c.CreatedAt);

        // Navigation properties
        builder.HasOne(c => c.Account)
            .WithMany(a => a.Checkouts)
            .HasForeignKey(c => c.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Domain.Entities.Integration;

namespace USP.Infrastructure.Persistence.Configurations.Integration;

/// <summary>
/// Entity configuration for Webhook
/// </summary>
public class WebhookConfiguration : IEntityTypeConfiguration<Webhook>
{
    public void Configure(EntityTypeBuilder<Webhook> builder)
    {
        builder.ToTable("webhooks");

        // Primary key
        builder.HasKey(w => w.Id);

        // Properties
        builder.Property(w => w.Id)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(w => w.UserId)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(w => w.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(w => w.Url)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(w => w.Events)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("[]");

        builder.Property(w => w.SecretKey)
            .HasMaxLength(255);

        builder.Property(w => w.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(w => w.MaxRetries)
            .IsRequired()
            .HasDefaultValue(3);

        builder.Property(w => w.TimeoutSeconds)
            .IsRequired()
            .HasDefaultValue(30);

        builder.Property(w => w.CreatedAt)
            .IsRequired();

        builder.Property(w => w.UpdatedAt)
            .IsRequired();

        builder.Property(w => w.DeletedAt);

        // Indexes
        builder.HasIndex(w => w.UserId);

        builder.HasIndex(w => w.IsActive);

        builder.HasIndex(w => new { w.UserId, w.IsActive });

        builder.HasIndex(w => w.CreatedAt);

        builder.HasIndex(w => w.DeletedAt);

        // Navigation properties
        builder.HasMany(w => w.Deliveries)
            .WithOne(d => d.Webhook)
            .HasForeignKey(d => d.WebhookId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

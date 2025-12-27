using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Domain.Entities.Integration;

namespace USP.Infrastructure.Persistence.Configurations.Integration;

/// <summary>
/// Entity configuration for WebhookDelivery
/// </summary>
public class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.ToTable("webhook_deliveries");

        // Primary key
        builder.HasKey(d => d.Id);

        // Properties
        builder.Property(d => d.Id)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(d => d.WebhookId)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(d => d.EventType)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(d => d.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(d => d.Payload)
            .HasColumnType("jsonb");

        builder.Property(d => d.AttemptCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(d => d.ResponseStatusCode);

        builder.Property(d => d.ResponseBody)
            .HasColumnType("text");

        builder.Property(d => d.ErrorMessage)
            .HasColumnType("text");

        builder.Property(d => d.NextRetryAt);

        builder.Property(d => d.CreatedAt)
            .IsRequired();

        builder.Property(d => d.DeliveredAt);

        // Indexes
        builder.HasIndex(d => d.WebhookId);

        builder.HasIndex(d => d.EventType);

        builder.HasIndex(d => d.Status);

        builder.HasIndex(d => new { d.WebhookId, d.Status });

        builder.HasIndex(d => new { d.Status, d.NextRetryAt });

        builder.HasIndex(d => d.CreatedAt);

        // Navigation properties
        builder.HasOne(d => d.Webhook)
            .WithMany(w => w.Deliveries)
            .HasForeignKey(d => d.WebhookId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

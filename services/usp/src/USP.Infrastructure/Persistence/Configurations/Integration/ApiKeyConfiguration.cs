using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Domain.Entities.Integration;

namespace USP.Infrastructure.Persistence.Configurations.Integration;

/// <summary>
/// Entity configuration for ApiKey
/// </summary>
public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("api_keys");

        // Primary key
        builder.HasKey(k => k.Id);

        // Properties
        builder.Property(k => k.Id)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(k => k.UserId)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(k => k.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(k => k.KeyHash)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(k => k.KeyPrefix)
            .HasMaxLength(100);

        builder.Property(k => k.Scopes)
            .HasColumnType("jsonb");

        builder.Property(k => k.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(k => k.RateLimitPerMinute);

        builder.Property(k => k.AllowedIps)
            .HasColumnType("jsonb");

        builder.Property(k => k.ExpiresAt);

        builder.Property(k => k.LastUsedAt);

        builder.Property(k => k.UsageCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(k => k.CreatedAt)
            .IsRequired();

        builder.Property(k => k.UpdatedAt)
            .IsRequired();

        builder.Property(k => k.DeletedAt);

        // Indexes
        builder.HasIndex(k => k.UserId);

        builder.HasIndex(k => k.KeyHash)
            .IsUnique();

        builder.HasIndex(k => k.KeyPrefix);

        builder.HasIndex(k => k.IsActive);

        builder.HasIndex(k => k.ExpiresAt);

        builder.HasIndex(k => new { k.UserId, k.IsActive });

        builder.HasIndex(k => k.CreatedAt);

        builder.HasIndex(k => k.DeletedAt);
    }
}

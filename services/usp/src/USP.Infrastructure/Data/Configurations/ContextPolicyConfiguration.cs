using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Models.Entities;

namespace USP.Infrastructure.Data.Configurations;

public class ContextPolicyConfiguration : IEntityTypeConfiguration<ContextPolicy>
{
    public void Configure(EntityTypeBuilder<ContextPolicy> builder)
    {
        builder.ToTable("ContextPolicies");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.ResourceType)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Action)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("*");

        builder.Property(c => c.AllowedDaysOfWeek)
            .HasMaxLength(200);

        builder.Property(c => c.AllowedCountries)
            .HasColumnType("text[]");

        builder.Property(c => c.DeniedCountries)
            .HasColumnType("text[]");

        builder.Property(c => c.AllowedNetworkZones)
            .HasColumnType("text[]");

        builder.Property(c => c.AllowedDeviceTypes)
            .HasColumnType("text[]");

        builder.Property(c => c.IsActive)
            .HasDefaultValue(true);

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .IsRequired();

        // Indexes for performance
        builder.HasIndex(c => new { c.ResourceType, c.Action, c.IsActive })
            .HasDatabaseName("IX_ContextPolicies_Resource_Action_Active");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Models.Entities;

namespace USP.Infrastructure.Data.Configurations;

public class AccessPolicyConfiguration : IEntityTypeConfiguration<AccessPolicy>
{
    public void Configure(EntityTypeBuilder<AccessPolicy> builder)
    {
        builder.ToTable("access_policies");

        builder.HasKey(ap => ap.Id);

        builder.Property(ap => ap.Id)
            .HasColumnName("id");

        builder.Property(ap => ap.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(ap => ap.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(ap => ap.PolicyType)
            .HasColumnName("policy_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(ap => ap.Policy)
            .HasColumnName("policy")
            .IsRequired();

        builder.Property(ap => ap.IsActive)
            .HasColumnName("is_active");

        builder.Property(ap => ap.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(ap => ap.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(ap => ap.UpdatedAt)
            .HasColumnName("updated_at");

        // Legacy ABAC fields (optional)
        builder.Property(ap => ap.Effect)
            .HasColumnName("effect")
            .HasMaxLength(50);

        builder.Property(ap => ap.Subjects)
            .HasColumnName("subjects")
            .HasColumnType("jsonb");

        builder.Property(ap => ap.Resources)
            .HasColumnName("resources")
            .HasColumnType("jsonb");

        builder.Property(ap => ap.Actions)
            .HasColumnName("actions");

        builder.Property(ap => ap.Conditions)
            .HasColumnName("conditions")
            .HasColumnType("jsonb");

        builder.Property(ap => ap.Priority)
            .HasColumnName("priority");

        // Indexes
        builder.HasIndex(ap => ap.Name).IsUnique().HasDatabaseName("idx_access_policies_name");
        builder.HasIndex(ap => ap.PolicyType).HasDatabaseName("idx_access_policies_policy_type");
        builder.HasIndex(ap => ap.IsActive).HasDatabaseName("idx_access_policies_is_active");
    }
}

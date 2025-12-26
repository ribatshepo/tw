using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Models.Entities;

namespace USP.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework configuration for Workspace entity
/// </summary>
public class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.ToTable("workspaces");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Id).HasColumnName("id");
        builder.Property(w => w.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(w => w.Slug).HasColumnName("slug").HasMaxLength(50).IsRequired();
        builder.Property(w => w.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(w => w.OwnerId).HasColumnName("owner_id").IsRequired();
        builder.Property(w => w.ParentWorkspaceId).HasColumnName("parent_workspace_id");
        builder.Property(w => w.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
        builder.Property(w => w.Settings).HasColumnName("settings").HasColumnType("jsonb");
        builder.Property(w => w.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
        builder.Property(w => w.SubscriptionTier).HasColumnName("subscription_tier").HasMaxLength(50).IsRequired();
        builder.Property(w => w.CustomDomain).HasColumnName("custom_domain").HasMaxLength(100);
        builder.Property(w => w.RequireMfa).HasColumnName("require_mfa");
        builder.Property(w => w.MinPasswordLength).HasColumnName("min_password_length");
        builder.Property(w => w.IpWhitelist).HasColumnName("ip_whitelist").HasColumnType("jsonb");
        builder.Property(w => w.SessionTimeoutMinutes).HasColumnName("session_timeout_minutes");
        builder.Property(w => w.IsBillable).HasColumnName("is_billable");
        builder.Property(w => w.MonthlyCostCents).HasColumnName("monthly_cost_cents");
        builder.Property(w => w.CreatedAt).HasColumnName("created_at");
        builder.Property(w => w.UpdatedAt).HasColumnName("updated_at");
        builder.Property(w => w.DeletedAt).HasColumnName("deleted_at");

        // Relationships
        builder.HasOne(w => w.Owner)
            .WithMany()
            .HasForeignKey(w => w.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.ParentWorkspace)
            .WithMany(w => w.ChildWorkspaces)
            .HasForeignKey(w => w.ParentWorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.Quota)
            .WithOne(q => q.Workspace)
            .HasForeignKey<WorkspaceQuota>(q => q.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(w => w.Usage)
            .WithOne(u => u.Workspace)
            .HasForeignKey<WorkspaceUsage>(u => u.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(w => w.Slug).HasDatabaseName("idx_workspaces_slug").IsUnique();
        builder.HasIndex(w => w.OwnerId).HasDatabaseName("idx_workspaces_owner_id");
        builder.HasIndex(w => w.ParentWorkspaceId).HasDatabaseName("idx_workspaces_parent_id");
        builder.HasIndex(w => w.Status).HasDatabaseName("idx_workspaces_status");
        builder.HasIndex(w => w.CustomDomain).HasDatabaseName("idx_workspaces_custom_domain");
        builder.HasIndex(w => w.DeletedAt).HasDatabaseName("idx_workspaces_deleted_at");
    }
}

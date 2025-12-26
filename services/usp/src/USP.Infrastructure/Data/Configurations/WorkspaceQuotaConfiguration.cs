using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Models.Entities;

namespace USP.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework configuration for WorkspaceQuota entity
/// </summary>
public class WorkspaceQuotaConfiguration : IEntityTypeConfiguration<WorkspaceQuota>
{
    public void Configure(EntityTypeBuilder<WorkspaceQuota> builder)
    {
        builder.ToTable("workspace_quotas");
        builder.HasKey(wq => wq.Id);

        builder.Property(wq => wq.Id).HasColumnName("id");
        builder.Property(wq => wq.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(wq => wq.MaxUsers).HasColumnName("max_users");
        builder.Property(wq => wq.MaxSecrets).HasColumnName("max_secrets");
        builder.Property(wq => wq.MaxPrivilegedAccounts).HasColumnName("max_privileged_accounts");
        builder.Property(wq => wq.MaxPamSessions).HasColumnName("max_pam_sessions");
        builder.Property(wq => wq.MaxApiRequestsPerHour).HasColumnName("max_api_requests_per_hour");
        builder.Property(wq => wq.MaxStorageMb).HasColumnName("max_storage_mb");
        builder.Property(wq => wq.MaxChildWorkspaces).HasColumnName("max_child_workspaces");
        builder.Property(wq => wq.AuditRetentionDays).HasColumnName("audit_retention_days");
        builder.Property(wq => wq.SessionRecordingEnabled).HasColumnName("session_recording_enabled");
        builder.Property(wq => wq.AdvancedComplianceEnabled).HasColumnName("advanced_compliance_enabled");
        builder.Property(wq => wq.CustomAuthMethodsEnabled).HasColumnName("custom_auth_methods_enabled");
        builder.Property(wq => wq.CreatedAt).HasColumnName("created_at");
        builder.Property(wq => wq.UpdatedAt).HasColumnName("updated_at");

        // Indexes
        builder.HasIndex(wq => wq.WorkspaceId).HasDatabaseName("idx_workspace_quotas_workspace_id").IsUnique();
    }
}

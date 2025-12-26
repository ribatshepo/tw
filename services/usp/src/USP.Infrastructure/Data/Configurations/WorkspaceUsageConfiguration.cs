using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Models.Entities;

namespace USP.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework configuration for WorkspaceUsage entity
/// </summary>
public class WorkspaceUsageConfiguration : IEntityTypeConfiguration<WorkspaceUsage>
{
    public void Configure(EntityTypeBuilder<WorkspaceUsage> builder)
    {
        builder.ToTable("workspace_usages");
        builder.HasKey(wu => wu.Id);

        builder.Property(wu => wu.Id).HasColumnName("id");
        builder.Property(wu => wu.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(wu => wu.CurrentUsers).HasColumnName("current_users");
        builder.Property(wu => wu.CurrentSecrets).HasColumnName("current_secrets");
        builder.Property(wu => wu.CurrentPrivilegedAccounts).HasColumnName("current_privileged_accounts");
        builder.Property(wu => wu.CurrentPamSessions).HasColumnName("current_pam_sessions");
        builder.Property(wu => wu.ApiRequestsThisHour).HasColumnName("api_requests_this_hour");
        builder.Property(wu => wu.ApiRequestsResetAt).HasColumnName("api_requests_reset_at");
        builder.Property(wu => wu.CurrentStorageMb).HasColumnName("current_storage_mb");
        builder.Property(wu => wu.CurrentChildWorkspaces).HasColumnName("current_child_workspaces");
        builder.Property(wu => wu.TotalApiRequests).HasColumnName("total_api_requests");
        builder.Property(wu => wu.TotalAuditLogs).HasColumnName("total_audit_logs");
        builder.Property(wu => wu.TotalSessionRecordings).HasColumnName("total_session_recordings");
        builder.Property(wu => wu.CreatedAt).HasColumnName("created_at");
        builder.Property(wu => wu.UpdatedAt).HasColumnName("updated_at");

        // Indexes
        builder.HasIndex(wu => wu.WorkspaceId).HasDatabaseName("idx_workspace_usages_workspace_id").IsUnique();
    }
}

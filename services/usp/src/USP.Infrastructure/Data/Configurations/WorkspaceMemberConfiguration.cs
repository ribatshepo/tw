using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Models.Entities;

namespace USP.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework configuration for WorkspaceMember entity
/// </summary>
public class WorkspaceMemberConfiguration : IEntityTypeConfiguration<WorkspaceMember>
{
    public void Configure(EntityTypeBuilder<WorkspaceMember> builder)
    {
        builder.ToTable("workspace_members");
        builder.HasKey(wm => wm.Id);

        builder.Property(wm => wm.Id).HasColumnName("id");
        builder.Property(wm => wm.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(wm => wm.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(wm => wm.Role).HasColumnName("role").HasMaxLength(50).IsRequired();
        builder.Property(wm => wm.IsActive).HasColumnName("is_active");
        builder.Property(wm => wm.InvitationStatus).HasColumnName("invitation_status").HasMaxLength(50);
        builder.Property(wm => wm.InvitationToken).HasColumnName("invitation_token").HasMaxLength(500);
        builder.Property(wm => wm.InvitationExpiresAt).HasColumnName("invitation_expires_at");
        builder.Property(wm => wm.InvitedBy).HasColumnName("invited_by");
        builder.Property(wm => wm.JoinedAt).HasColumnName("joined_at");
        builder.Property(wm => wm.LeftAt).HasColumnName("left_at");
        builder.Property(wm => wm.LastAccessedAt).HasColumnName("last_accessed_at");

        // Relationships
        builder.HasOne(wm => wm.Workspace)
            .WithMany(w => w.Members)
            .HasForeignKey(wm => wm.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(wm => wm.User)
            .WithMany()
            .HasForeignKey(wm => wm.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(wm => wm.Inviter)
            .WithMany()
            .HasForeignKey(wm => wm.InvitedBy)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(wm => wm.WorkspaceId).HasDatabaseName("idx_workspace_members_workspace_id");
        builder.HasIndex(wm => wm.UserId).HasDatabaseName("idx_workspace_members_user_id");
        builder.HasIndex(wm => new { wm.WorkspaceId, wm.UserId })
            .HasDatabaseName("idx_workspace_members_workspace_user").IsUnique();
        builder.HasIndex(wm => wm.InvitationToken).HasDatabaseName("idx_workspace_members_invitation_token");
        builder.HasIndex(wm => wm.IsActive).HasDatabaseName("idx_workspace_members_is_active");
    }
}

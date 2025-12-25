using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Models.Entities;

namespace USP.Infrastructure.Data.Configurations;

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles");

        builder.HasKey(ur => new { ur.UserId, ur.RoleId });

        builder.Property(ur => ur.UserId)
            .HasColumnName("user_id");

        builder.Property(ur => ur.RoleId)
            .HasColumnName("role_id");

        builder.Property(ur => ur.GrantedAt)
            .HasColumnName("granted_at");

        builder.Property(ur => ur.GrantedBy)
            .HasColumnName("granted_by");

        builder.Property(ur => ur.ExpiresAt)
            .HasColumnName("expires_at");

        // Relationships
        builder.HasOne(ur => ur.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ur => ur.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(ur => ur.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ur => ur.GrantedByUser)
            .WithMany()
            .HasForeignKey(ur => ur.GrantedBy)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(ur => ur.UserId).HasDatabaseName("idx_user_roles_user_id");
        builder.HasIndex(ur => ur.RoleId).HasDatabaseName("idx_user_roles_role_id");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Models.Entities;

namespace USP.Infrastructure.Data.Configurations;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id");

        builder.Property(p => p.Resource)
            .HasColumnName("resource")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(p => p.Action)
            .HasColumnName("action")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasColumnName("description");

        // Unique constraint
        builder.HasIndex(p => new { p.Resource, p.Action })
            .HasDatabaseName("unique_permission")
            .IsUnique();

        builder.HasIndex(p => p.Resource).HasDatabaseName("idx_permissions_resource");
        builder.HasIndex(p => p.Action).HasDatabaseName("idx_permissions_action");
    }
}

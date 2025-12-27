using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Domain.Entities.Security;

namespace USP.Infrastructure.Persistence.Configurations.Security;

/// <summary>
/// Entity configuration for Permission
/// </summary>
public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");

        // Primary key
        builder.HasKey(p => p.Id);

        // Properties
        builder.Property(p => p.Id)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(p => p.Resource)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(p => p.Action)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Description)
            .HasMaxLength(500);

        builder.Property(p => p.IsSystemPermission)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .IsRequired();

        builder.Property(p => p.DeletedAt);

        // Indexes
        builder.HasIndex(p => new { p.Resource, p.Action })
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.HasIndex(p => p.Resource);

        builder.HasIndex(p => p.IsSystemPermission);

        builder.HasIndex(p => p.CreatedAt);

        builder.HasIndex(p => p.DeletedAt);

        // Navigation properties - Many-to-many with ApplicationRole
        builder.HasMany(p => p.Roles)
            .WithMany(r => r.Permissions)
            .UsingEntity(j => j.ToTable("role_permissions"));

        // Ignore computed properties
        builder.Ignore(p => p.FullPermission);
    }
}

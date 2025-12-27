using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Domain.Entities.Identity;

namespace USP.Infrastructure.Persistence.Configurations.Identity;

/// <summary>
/// Entity configuration for ApplicationRole
/// </summary>
public class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.ToTable("roles");

        // Properties
        builder.Property(r => r.Description)
            .HasMaxLength(500);

        builder.Property(r => r.IsSystemRole)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(r => r.Priority)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(r => r.Metadata)
            .HasColumnType("jsonb");

        builder.Property(r => r.CreatedAt)
            .IsRequired();

        builder.Property(r => r.UpdatedAt)
            .IsRequired();

        builder.Property(r => r.DeletedAt);

        // Indexes
        builder.HasIndex(r => r.Name)
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.HasIndex(r => r.Priority);

        builder.HasIndex(r => r.IsSystemRole);

        builder.HasIndex(r => r.CreatedAt);

        builder.HasIndex(r => r.DeletedAt);

        // Navigation properties - Many-to-many with Permission
        builder.HasMany(r => r.Permissions)
            .WithMany(p => p.Roles)
            .UsingEntity(j => j.ToTable("role_permissions"));
    }
}

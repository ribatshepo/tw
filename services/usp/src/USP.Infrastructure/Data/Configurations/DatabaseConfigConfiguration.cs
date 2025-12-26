using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Models.Entities;

namespace USP.Infrastructure.Data.Configurations;

public class DatabaseConfigConfiguration : IEntityTypeConfiguration<DatabaseConfig>
{
    public void Configure(EntityTypeBuilder<DatabaseConfig> builder)
    {
        builder.ToTable("database_configs");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.Plugin)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(d => d.EncryptedConnectionUrl)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(d => d.EncryptedUsername)
            .HasMaxLength(1000);

        builder.Property(d => d.EncryptedPassword)
            .HasMaxLength(1000);

        builder.Property(d => d.MaxOpenConnections)
            .HasDefaultValue(4);

        builder.Property(d => d.MaxIdleConnections)
            .HasDefaultValue(2);

        builder.Property(d => d.MaxConnectionLifetimeSeconds)
            .HasDefaultValue(3600);

        builder.Property(d => d.AdditionalConfig)
            .HasColumnType("jsonb");

        builder.Property(d => d.ConfiguredAt)
            .IsRequired();

        builder.Property(d => d.ConfiguredBy)
            .IsRequired();

        builder.Property(d => d.IsDeleted)
            .HasDefaultValue(false);

        // Indexes
        builder.HasIndex(d => d.Name)
            .IsUnique()
            .HasFilter("is_deleted = false");

        builder.HasIndex(d => d.Plugin);
        builder.HasIndex(d => d.ConfiguredAt);

        // Relationships
        builder.HasMany(d => d.Roles)
            .WithOne(r => r.DatabaseConfig)
            .HasForeignKey(r => r.DatabaseConfigId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(d => d.Leases)
            .WithOne(l => l.DatabaseConfig)
            .HasForeignKey(l => l.DatabaseConfigId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DatabaseRoleConfiguration : IEntityTypeConfiguration<DatabaseRole>
{
    public void Configure(EntityTypeBuilder<DatabaseRole> builder)
    {
        builder.ToTable("database_roles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.RoleName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.CreationStatements)
            .IsRequired()
            .HasMaxLength(10000);

        builder.Property(r => r.RevocationStatements)
            .HasMaxLength(10000);

        builder.Property(r => r.RenewStatements)
            .HasMaxLength(10000);

        builder.Property(r => r.RollbackStatements)
            .HasMaxLength(10000);

        builder.Property(r => r.DefaultTtlSeconds)
            .HasDefaultValue(3600);

        builder.Property(r => r.MaxTtlSeconds)
            .HasDefaultValue(86400);

        builder.Property(r => r.CreatedAt)
            .IsRequired();

        builder.Property(r => r.CreatedBy)
            .IsRequired();

        builder.Property(r => r.IsDeleted)
            .HasDefaultValue(false);

        // Indexes
        builder.HasIndex(r => new { r.DatabaseConfigId, r.RoleName })
            .IsUnique()
            .HasFilter("is_deleted = false");

        builder.HasIndex(r => r.CreatedAt);

        // Relationships
        builder.HasMany(r => r.Leases)
            .WithOne(l => l.DatabaseRole)
            .HasForeignKey(l => l.DatabaseRoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DatabaseLeaseConfiguration : IEntityTypeConfiguration<DatabaseLease>
{
    public void Configure(EntityTypeBuilder<DatabaseLease> builder)
    {
        builder.ToTable("database_leases");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.LeaseId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(l => l.GeneratedUsername)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(l => l.EncryptedPassword)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(l => l.CreatedAt)
            .IsRequired();

        builder.Property(l => l.CreatedBy)
            .IsRequired();

        builder.Property(l => l.ExpiresAt)
            .IsRequired();

        builder.Property(l => l.IsRevoked)
            .HasDefaultValue(false);

        builder.Property(l => l.RenewalCount)
            .HasDefaultValue(0);

        // Indexes
        builder.HasIndex(l => l.LeaseId)
            .IsUnique();

        builder.HasIndex(l => l.ExpiresAt);
        builder.HasIndex(l => l.IsRevoked);
        builder.HasIndex(l => new { l.DatabaseConfigId, l.IsRevoked });
        builder.HasIndex(l => l.CreatedAt);
    }
}

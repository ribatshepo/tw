using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Domain.Entities.Integration;

namespace USP.Infrastructure.Persistence.Configurations.Integration;

/// <summary>
/// Entity configuration for Workspace
/// </summary>
public class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.ToTable("workspaces");

        // Primary key
        builder.HasKey(w => w.Id);

        // Properties
        builder.Property(w => w.Id)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(w => w.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(w => w.Slug)
            .HasMaxLength(100);

        builder.Property(w => w.Description)
            .HasMaxLength(1000);

        builder.Property(w => w.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(w => w.Settings)
            .HasColumnType("jsonb");

        builder.Property(w => w.Metadata)
            .HasColumnType("jsonb");

        builder.Property(w => w.CreatedAt)
            .IsRequired();

        builder.Property(w => w.CreatedBy)
            .HasMaxLength(255);

        builder.Property(w => w.UpdatedAt)
            .IsRequired();

        builder.Property(w => w.DeletedAt);

        // Indexes
        builder.HasIndex(w => w.Name)
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.HasIndex(w => w.Slug)
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL AND \"Slug\" IS NOT NULL");

        builder.HasIndex(w => w.IsActive);

        builder.HasIndex(w => w.CreatedAt);

        builder.HasIndex(w => w.DeletedAt);
    }
}

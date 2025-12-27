using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Domain.Entities.Secrets;

namespace USP.Infrastructure.Persistence.Configurations.Secrets;

/// <summary>
/// Entity configuration for Secret
/// </summary>
public class SecretConfiguration : IEntityTypeConfiguration<Secret>
{
    public void Configure(EntityTypeBuilder<Secret> builder)
    {
        builder.ToTable("secrets");

        // Primary key
        builder.HasKey(s => s.Id);

        // Properties
        builder.Property(s => s.Id)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(s => s.Path)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(s => s.Type)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(s => s.CurrentVersion)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(s => s.MaxVersions)
            .IsRequired()
            .HasDefaultValue(10);

        builder.Property(s => s.CasRequired)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.Metadata)
            .HasColumnType("jsonb");

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.CreatedBy)
            .HasMaxLength(255);

        builder.Property(s => s.UpdatedAt)
            .IsRequired();

        builder.Property(s => s.UpdatedBy)
            .HasMaxLength(255);

        builder.Property(s => s.DeletedAt);

        // Indexes
        builder.HasIndex(s => s.Path)
            .IsUnique();

        builder.HasIndex(s => s.Type);

        builder.HasIndex(s => s.IsDeleted);

        builder.HasIndex(s => s.CreatedAt);

        builder.HasIndex(s => s.DeletedAt);

        // Navigation properties
        builder.HasMany(s => s.Versions)
            .WithOne(v => v.Secret)
            .HasForeignKey(v => v.SecretId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

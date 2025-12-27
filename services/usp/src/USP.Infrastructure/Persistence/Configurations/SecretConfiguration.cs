using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Domain.Entities.Secrets;

namespace USP.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity configuration for Secret
/// </summary>
public class SecretConfiguration : IEntityTypeConfiguration<Secret>
{
    public void Configure(EntityTypeBuilder<Secret> builder)
    {
        builder.ToTable("Secrets");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasMaxLength(36)
            .IsRequired();

        builder.Property(s => s.Path)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(s => s.Type)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

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
            .HasMaxLength(450);

        builder.Property(s => s.UpdatedAt)
            .IsRequired();

        builder.Property(s => s.UpdatedBy)
            .HasMaxLength(450);

        builder.Property(s => s.DeletedAt);

        // Relationships
        builder.HasMany(s => s.Versions)
            .WithOne(v => v.Secret)
            .HasForeignKey(v => v.SecretId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(s => s.Path)
            .IsUnique();
        builder.HasIndex(s => s.Type);
        builder.HasIndex(s => s.IsDeleted);
        builder.HasIndex(s => s.CreatedAt);
        builder.HasIndex(s => s.UpdatedAt);
    }
}

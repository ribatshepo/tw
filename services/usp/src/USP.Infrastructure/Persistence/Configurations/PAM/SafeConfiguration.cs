using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Domain.Entities.PAM;

namespace USP.Infrastructure.Persistence.Configurations.PAM;

/// <summary>
/// Entity configuration for Safe
/// </summary>
public class SafeConfiguration : IEntityTypeConfiguration<Safe>
{
    public void Configure(EntityTypeBuilder<Safe> builder)
    {
        builder.ToTable("safes");

        // Primary key
        builder.HasKey(s => s.Id);

        // Properties
        builder.Property(s => s.Id)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(s => s.Description)
            .HasMaxLength(1000);

        builder.Property(s => s.Metadata)
            .HasColumnType("jsonb");

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.CreatedBy)
            .HasMaxLength(255);

        builder.Property(s => s.UpdatedAt)
            .IsRequired();

        builder.Property(s => s.DeletedAt);

        // Indexes
        builder.HasIndex(s => s.Name)
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.HasIndex(s => s.CreatedAt);

        builder.HasIndex(s => s.DeletedAt);

        // Navigation properties
        builder.HasMany(s => s.Accounts)
            .WithOne(a => a.Safe)
            .HasForeignKey(a => a.SafeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

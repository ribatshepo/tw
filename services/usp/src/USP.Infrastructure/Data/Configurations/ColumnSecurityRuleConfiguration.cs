using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Models.Entities;

namespace USP.Infrastructure.Data.Configurations;

public class ColumnSecurityRuleConfiguration : IEntityTypeConfiguration<ColumnSecurityRule>
{
    public void Configure(EntityTypeBuilder<ColumnSecurityRule> builder)
    {
        builder.ToTable("ColumnSecurityRules");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.TableName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.ColumnName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Operation)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.RestrictionType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.MaskingPattern)
            .HasMaxLength(100);

        builder.Property(c => c.AllowedRoles)
            .HasColumnType("text[]");

        builder.Property(c => c.DeniedRoles)
            .HasColumnType("text[]");

        builder.Property(c => c.Condition)
            .HasColumnType("text");

        builder.Property(c => c.Priority)
            .HasDefaultValue(0);

        builder.Property(c => c.IsActive)
            .HasDefaultValue(true);

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .IsRequired();

        // Indexes for performance
        builder.HasIndex(c => new { c.TableName, c.ColumnName, c.Operation })
            .HasDatabaseName("IX_ColumnSecurityRules_Table_Column_Operation");

        builder.HasIndex(c => c.IsActive)
            .HasDatabaseName("IX_ColumnSecurityRules_IsActive");
    }
}

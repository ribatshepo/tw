using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Domain.Entities.PAM;

namespace USP.Infrastructure.Persistence.Configurations.PAM;

/// <summary>
/// Entity configuration for PrivilegedAccount
/// </summary>
public class PrivilegedAccountConfiguration : IEntityTypeConfiguration<PrivilegedAccount>
{
    public void Configure(EntityTypeBuilder<PrivilegedAccount> builder)
    {
        builder.ToTable("privileged_accounts");

        // Primary key
        builder.HasKey(a => a.Id);

        // Properties
        builder.Property(a => a.Id)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(a => a.SafeId)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(a => a.AccountName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(a => a.Platform)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(a => a.Address)
            .HasMaxLength(500);

        builder.Property(a => a.EncryptedPassword)
            .HasColumnType("text");

        builder.Property(a => a.EncryptedSSHKey)
            .HasColumnType("text");

        builder.Property(a => a.AutoRotationEnabled)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(a => a.RotationIntervalDays);

        builder.Property(a => a.LastRotatedAt);

        builder.Property(a => a.NextRotationAt);

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.Property(a => a.UpdatedAt)
            .IsRequired();

        builder.Property(a => a.DeletedAt);

        // Indexes
        builder.HasIndex(a => a.SafeId);

        builder.HasIndex(a => new { a.SafeId, a.AccountName, a.Platform })
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.HasIndex(a => a.Platform);

        builder.HasIndex(a => a.AutoRotationEnabled);

        builder.HasIndex(a => a.NextRotationAt);

        builder.HasIndex(a => a.CreatedAt);

        builder.HasIndex(a => a.DeletedAt);

        // Navigation properties
        builder.HasOne(a => a.Safe)
            .WithMany(s => s.Accounts)
            .HasForeignKey(a => a.SafeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.Checkouts)
            .WithOne(c => c.Account)
            .HasForeignKey(c => c.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

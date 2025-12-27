using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Domain.Entities.Secrets;

namespace USP.Infrastructure.Persistence.Configurations.Secrets;

/// <summary>
/// Entity configuration for Certificate
/// </summary>
public class CertificateConfiguration : IEntityTypeConfiguration<Certificate>
{
    public void Configure(EntityTypeBuilder<Certificate> builder)
    {
        builder.ToTable("certificates");

        // Primary key
        builder.HasKey(c => c.Id);

        // Properties
        builder.Property(c => c.Id)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(c => c.SerialNumber)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(c => c.Subject)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(c => c.Issuer)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(c => c.CertificateData)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(c => c.PrivateKeyData)
            .HasColumnType("text");

        builder.Property(c => c.NotBefore)
            .IsRequired();

        builder.Property(c => c.NotAfter)
            .IsRequired();

        builder.Property(c => c.IsRevoked)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(c => c.RevokedAt);

        builder.Property(c => c.RevocationReason)
            .HasMaxLength(255);

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.DeletedAt);

        // Indexes
        builder.HasIndex(c => c.SerialNumber)
            .IsUnique();

        builder.HasIndex(c => c.Subject);

        builder.HasIndex(c => c.NotAfter);

        builder.HasIndex(c => c.IsRevoked);

        builder.HasIndex(c => c.CreatedAt);

        builder.HasIndex(c => c.DeletedAt);
    }
}

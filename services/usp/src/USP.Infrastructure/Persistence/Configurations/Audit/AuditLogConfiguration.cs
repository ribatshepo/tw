using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Domain.Entities.Audit;

namespace USP.Infrastructure.Persistence.Configurations.Audit;

/// <summary>
/// Entity configuration for AuditLog
/// </summary>
public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        // Primary key
        builder.HasKey(a => a.Id);

        // Properties
        builder.Property(a => a.Id)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(a => a.EventType)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(a => a.UserId)
            .HasMaxLength(255);

        builder.Property(a => a.UserName)
            .HasMaxLength(255);

        builder.Property(a => a.IpAddress)
            .HasMaxLength(45);

        builder.Property(a => a.UserAgent)
            .HasMaxLength(500);

        builder.Property(a => a.Resource)
            .HasMaxLength(255);

        builder.Property(a => a.Action)
            .HasMaxLength(100);

        builder.Property(a => a.Success)
            .IsRequired();

        builder.Property(a => a.Details)
            .HasColumnType("jsonb");

        builder.Property(a => a.EncryptedData)
            .HasColumnType("text");

        builder.Property(a => a.CorrelationId)
            .HasMaxLength(255);

        builder.Property(a => a.Timestamp)
            .IsRequired();

        // Indexes
        builder.HasIndex(a => a.EventType);

        builder.HasIndex(a => a.UserId);

        builder.HasIndex(a => a.Resource);

        builder.HasIndex(a => a.Action);

        builder.HasIndex(a => a.Success);

        builder.HasIndex(a => a.CorrelationId);

        builder.HasIndex(a => a.Timestamp);

        builder.HasIndex(a => new { a.UserId, a.EventType, a.Timestamp });
    }
}

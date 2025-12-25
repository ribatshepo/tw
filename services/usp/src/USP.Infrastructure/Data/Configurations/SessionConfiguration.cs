using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using USP.Core.Models.Entities;

namespace USP.Infrastructure.Data.Configurations;

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id");

        builder.Property(s => s.UserId)
            .HasColumnName("user_id");

        builder.Property(s => s.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(s => s.RefreshTokenHash)
            .HasColumnName("refresh_token_hash")
            .HasMaxLength(255);

        builder.Property(s => s.IpAddress)
            .HasColumnName("ip_address")
            .HasColumnType("inet");

        builder.Property(s => s.UserAgent)
            .HasColumnName("user_agent");

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(s => s.ExpiresAt)
            .HasColumnName("expires_at");

        builder.Property(s => s.LastActivity)
            .HasColumnName("last_activity");

        builder.Property(s => s.Revoked)
            .HasColumnName("revoked");

        builder.Property(s => s.RevokedAt)
            .HasColumnName("revoked_at");

        // Relationships
        builder.HasOne(s => s.User)
            .WithMany(u => u.Sessions)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(s => s.UserId).HasDatabaseName("idx_sessions_user_id");
        builder.HasIndex(s => s.TokenHash).HasDatabaseName("idx_sessions_token_hash").IsUnique();
        builder.HasIndex(s => s.ExpiresAt).HasDatabaseName("idx_sessions_expires_at");
        builder.HasIndex(s => s.Revoked).HasDatabaseName("idx_sessions_revoked");
    }
}

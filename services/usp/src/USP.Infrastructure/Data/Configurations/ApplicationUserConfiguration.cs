using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Models.Entities;

namespace USP.Infrastructure.Data.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("users");

        builder.Property(u => u.Id)
            .HasColumnName("id");

        builder.Property(u => u.UserName)
            .HasColumnName("username")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(u => u.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(u => u.FirstName)
            .HasColumnName("first_name")
            .HasMaxLength(255);

        builder.Property(u => u.LastName)
            .HasColumnName("last_name")
            .HasMaxLength(255);

        builder.Property(u => u.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(u => u.MfaEnabled)
            .HasColumnName("mfa_enabled");

        builder.Property(u => u.MfaSecret)
            .HasColumnName("mfa_secret")
            .HasMaxLength(255);

        builder.Property(u => u.FailedLoginAttempts)
            .HasColumnName("failed_login_attempts");

        builder.Property(u => u.LastFailedLogin)
            .HasColumnName("last_failed_login");

        builder.Property(u => u.LockedUntil)
            .HasColumnName("locked_until");

        builder.Property(u => u.PasswordChangedAt)
            .HasColumnName("password_changed_at");

        builder.Property(u => u.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(u => u.UpdatedAt)
            .HasColumnName("updated_at");

        // Indexes
        builder.HasIndex(u => u.UserName).HasDatabaseName("idx_users_username");
        builder.HasIndex(u => u.Email).HasDatabaseName("idx_users_email");
        builder.HasIndex(u => u.Status).HasDatabaseName("idx_users_status");

        // Ignore Identity properties not in schema
        builder.Ignore(u => u.NormalizedUserName);
        builder.Ignore(u => u.NormalizedEmail);
        builder.Ignore(u => u.EmailConfirmed);
        builder.Ignore(u => u.SecurityStamp);
        builder.Ignore(u => u.ConcurrencyStamp);
        builder.Ignore(u => u.PhoneNumber);
        builder.Ignore(u => u.PhoneNumberConfirmed);
        builder.Ignore(u => u.TwoFactorEnabled);
        builder.Ignore(u => u.LockoutEnd);
        builder.Ignore(u => u.LockoutEnabled);
        builder.Ignore(u => u.AccessFailedCount);
    }
}

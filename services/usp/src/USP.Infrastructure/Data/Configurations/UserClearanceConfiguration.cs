using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Models.Entities;

namespace USP.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for UserClearance entity
/// </summary>
public class UserClearanceConfiguration : IEntityTypeConfiguration<UserClearance>
{
    public void Configure(EntityTypeBuilder<UserClearance> builder)
    {
        builder.ToTable("user_clearances");
        builder.HasKey(uc => uc.Id);

        builder.Property(uc => uc.Id).HasColumnName("id");
        builder.Property(uc => uc.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(uc => uc.ClearanceType).HasColumnName("clearance_type").HasMaxLength(100).IsRequired();
        builder.Property(uc => uc.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
        builder.Property(uc => uc.GrantedAt).HasColumnName("granted_at").IsRequired();
        builder.Property(uc => uc.ExpiresAt).HasColumnName("expires_at");
        builder.Property(uc => uc.GrantedBy).HasColumnName("granted_by").IsRequired();
        builder.Property(uc => uc.ClearanceLevel).HasColumnName("clearance_level").HasMaxLength(50);
        builder.Property(uc => uc.BackgroundCheckDetails).HasColumnName("background_check_details");
        builder.Property(uc => uc.BackgroundCheckProvider).HasColumnName("background_check_provider").HasMaxLength(255);
        builder.Property(uc => uc.BackgroundCheckCompletedAt).HasColumnName("background_check_completed_at");
        builder.Property(uc => uc.TrainingCompleted).HasColumnName("training_completed");
        builder.Property(uc => uc.LastReviewedAt).HasColumnName("last_reviewed_at");
        builder.Property(uc => uc.LastReviewedBy).HasColumnName("last_reviewed_by");
        builder.Property(uc => uc.ReviewNotes).HasColumnName("review_notes");
        builder.Property(uc => uc.DocumentationPath).HasColumnName("documentation_path").HasMaxLength(1000);
        builder.Property(uc => uc.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(uc => uc.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasOne(uc => uc.User)
            .WithMany()
            .HasForeignKey(uc => uc.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(uc => uc.GrantedByUser)
            .WithMany()
            .HasForeignKey(uc => uc.GrantedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(uc => uc.LastReviewedByUser)
            .WithMany()
            .HasForeignKey(uc => uc.LastReviewedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(uc => uc.UserId).HasDatabaseName("idx_user_clearances_user_id");
        builder.HasIndex(uc => uc.ClearanceType).HasDatabaseName("idx_user_clearances_type");
        builder.HasIndex(uc => uc.Status).HasDatabaseName("idx_user_clearances_status");
        builder.HasIndex(uc => uc.ExpiresAt).HasDatabaseName("idx_user_clearances_expires_at");
        builder.HasIndex(uc => new { uc.UserId, uc.ClearanceType, uc.Status })
            .HasDatabaseName("idx_user_clearances_user_type_status");
    }
}

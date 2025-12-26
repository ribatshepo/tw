using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USP.Core.Models.Entities;

namespace USP.Infrastructure.Data.Configurations;

public class LdapConfigurationConfiguration : IEntityTypeConfiguration<LdapConfiguration>
{
    public void Configure(EntityTypeBuilder<LdapConfiguration> builder)
    {
        builder.ToTable("ldap_configurations");

        builder.HasKey(ldap => ldap.Id);

        // Column mappings (snake_case)
        builder.Property(ldap => ldap.Id)
            .HasColumnName("id");

        builder.Property(ldap => ldap.Name)
            .HasColumnName("name")
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(ldap => ldap.ServerUrl)
            .HasColumnName("server_url")
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(ldap => ldap.Port)
            .HasColumnName("port");

        builder.Property(ldap => ldap.UseSsl)
            .HasColumnName("use_ssl");

        builder.Property(ldap => ldap.UseTls)
            .HasColumnName("use_tls");

        builder.Property(ldap => ldap.BaseDn)
            .HasColumnName("base_dn")
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(ldap => ldap.BindDn)
            .HasColumnName("bind_dn")
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(ldap => ldap.BindPassword)
            .HasColumnName("bind_password")
            .IsRequired();

        builder.Property(ldap => ldap.UserSearchFilter)
            .HasColumnName("user_search_filter")
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(ldap => ldap.UserSearchBase)
            .HasColumnName("user_search_base")
            .HasMaxLength(500);

        builder.Property(ldap => ldap.GroupSearchFilter)
            .HasColumnName("group_search_filter")
            .HasMaxLength(500);

        builder.Property(ldap => ldap.GroupSearchBase)
            .HasColumnName("group_search_base")
            .HasMaxLength(500);

        builder.Property(ldap => ldap.EmailAttribute)
            .HasColumnName("email_attribute")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(ldap => ldap.FirstNameAttribute)
            .HasColumnName("first_name_attribute")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(ldap => ldap.LastNameAttribute)
            .HasColumnName("last_name_attribute")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(ldap => ldap.UsernameAttribute)
            .HasColumnName("username_attribute")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(ldap => ldap.GroupMembershipAttribute)
            .HasColumnName("group_membership_attribute")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(ldap => ldap.EnableJitProvisioning)
            .HasColumnName("enable_jit_provisioning");

        builder.Property(ldap => ldap.DefaultRoleId)
            .HasColumnName("default_role_id");

        builder.Property(ldap => ldap.SyncGroupsAsRoles)
            .HasColumnName("sync_groups_as_roles");

        builder.Property(ldap => ldap.UpdateUserOnLogin)
            .HasColumnName("update_user_on_login");

        builder.Property(ldap => ldap.EnableGroupSync)
            .HasColumnName("enable_group_sync");

        builder.Property(ldap => ldap.GroupSyncIntervalMinutes)
            .HasColumnName("group_sync_interval_minutes");

        builder.Property(ldap => ldap.LastGroupSync)
            .HasColumnName("last_group_sync");

        builder.Property(ldap => ldap.NestedGroupsEnabled)
            .HasColumnName("nested_groups_enabled");

        builder.Property(ldap => ldap.GroupRoleMapping)
            .HasColumnName("group_role_mapping")
            .HasColumnType("jsonb");

        builder.Property(ldap => ldap.IsActive)
            .HasColumnName("is_active");

        builder.Property(ldap => ldap.LastTestResult)
            .HasColumnName("last_test_result");

        builder.Property(ldap => ldap.LastTestedAt)
            .HasColumnName("last_tested_at");

        builder.Property(ldap => ldap.CreatedBy)
            .HasColumnName("created_by")
            .IsRequired();

        builder.Property(ldap => ldap.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(ldap => ldap.UpdatedAt)
            .HasColumnName("updated_at");

        // Indexes
        builder.HasIndex(ldap => ldap.Name)
            .IsUnique()
            .HasDatabaseName("idx_ldap_configs_name");

        builder.HasIndex(ldap => ldap.IsActive)
            .HasDatabaseName("idx_ldap_configs_is_active");

        // Relationships
        builder.HasOne(ldap => ldap.Creator)
            .WithMany()
            .HasForeignKey(ldap => ldap.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

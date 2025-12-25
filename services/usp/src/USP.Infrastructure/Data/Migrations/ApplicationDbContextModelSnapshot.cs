using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using USP.Infrastructure.Data;

#nullable disable

namespace USP.Infrastructure.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    partial class ApplicationDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            modelBuilder.UseIdentityByDefaultColumns();

            // ApplicationUser
            modelBuilder.Entity("USP.Core.Models.Entities.ApplicationUser", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<int>("AccessFailedCount")
                    .HasColumnType("integer")
                    .HasColumnName("access_failed_count");

                b.Property<string>("ConcurrencyStamp")
                    .IsConcurrencyToken()
                    .HasColumnType("text")
                    .HasColumnName("concurrency_stamp");

                b.Property<DateTime>("CreatedAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("created_at");

                b.Property<string>("Email")
                    .HasMaxLength(256)
                    .HasColumnType("character varying(256)")
                    .HasColumnName("email");

                b.Property<bool>("EmailConfirmed")
                    .HasColumnType("boolean")
                    .HasColumnName("email_confirmed");

                b.Property<bool>("IsActive")
                    .HasColumnType("boolean")
                    .HasColumnName("is_active");

                b.Property<DateTime?>("LastLoginAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("last_login_at");

                b.Property<bool>("LockoutEnabled")
                    .HasColumnType("boolean")
                    .HasColumnName("lockout_enabled");

                b.Property<DateTimeOffset?>("LockoutEnd")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("lockout_end");

                b.Property<bool>("MfaEnabled")
                    .HasColumnType("boolean")
                    .HasColumnName("mfa_enabled");

                b.Property<string>("MfaSecret")
                    .HasMaxLength(255)
                    .HasColumnType("character varying(255)")
                    .HasColumnName("mfa_secret");

                b.Property<string>("NormalizedEmail")
                    .HasMaxLength(256)
                    .HasColumnType("character varying(256)")
                    .HasColumnName("normalized_email");

                b.Property<string>("NormalizedUserName")
                    .HasMaxLength(256)
                    .HasColumnType("character varying(256)")
                    .HasColumnName("normalized_user_name");

                b.Property<string>("PasswordHash")
                    .HasColumnType("text")
                    .HasColumnName("password_hash");

                b.Property<string>("PhoneNumber")
                    .HasColumnType("text")
                    .HasColumnName("phone_number");

                b.Property<bool>("PhoneNumberConfirmed")
                    .HasColumnType("boolean")
                    .HasColumnName("phone_number_confirmed");

                b.Property<string>("SecurityStamp")
                    .HasColumnType("text")
                    .HasColumnName("security_stamp");

                b.Property<bool>("TwoFactorEnabled")
                    .HasColumnType("boolean")
                    .HasColumnName("two_factor_enabled");

                b.Property<DateTime>("UpdatedAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("updated_at");

                b.Property<string>("UserName")
                    .HasMaxLength(256)
                    .HasColumnType("character varying(256)")
                    .HasColumnName("user_name");

                b.HasKey("Id")
                    .HasName("pk_users");

                b.HasIndex("Email")
                    .IsUnique()
                    .HasDatabaseName("idx_users_email");

                b.HasIndex("NormalizedEmail")
                    .HasDatabaseName("EmailIndex");

                b.HasIndex("NormalizedUserName")
                    .IsUnique()
                    .HasDatabaseName("UserNameIndex");

                b.ToTable("users", (string)null);
            });

            // Role
            modelBuilder.Entity("USP.Core.Models.Entities.Role", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<string>("ConcurrencyStamp")
                    .IsConcurrencyToken()
                    .HasColumnType("text")
                    .HasColumnName("concurrency_stamp");

                b.Property<DateTime>("CreatedAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("created_at");

                b.Property<string>("Description")
                    .HasMaxLength(500)
                    .HasColumnType("character varying(500)")
                    .HasColumnName("description");

                b.Property<bool>("IsSystemRole")
                    .HasColumnType("boolean")
                    .HasColumnName("is_system_role");

                b.Property<string>("Name")
                    .HasMaxLength(256)
                    .HasColumnType("character varying(256)")
                    .HasColumnName("name");

                b.Property<string>("NormalizedName")
                    .HasMaxLength(256)
                    .HasColumnType("character varying(256)")
                    .HasColumnName("normalized_name");

                b.HasKey("Id")
                    .HasName("pk_roles");

                b.HasIndex("NormalizedName")
                    .IsUnique()
                    .HasDatabaseName("RoleNameIndex");

                b.ToTable("roles", (string)null);
            });

            // Permission
            modelBuilder.Entity("USP.Core.Models.Entities.Permission", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<string>("Action")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("character varying(100)")
                    .HasColumnName("action");

                b.Property<DateTime>("CreatedAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("created_at");

                b.Property<string>("Description")
                    .HasMaxLength(500)
                    .HasColumnType("character varying(500)")
                    .HasColumnName("description");

                b.Property<string>("Resource")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("character varying(100)")
                    .HasColumnName("resource");

                b.HasKey("Id")
                    .HasName("pk_permissions");

                b.HasIndex("Resource", "Action")
                    .IsUnique()
                    .HasDatabaseName("idx_permissions_resource_action");

                b.ToTable("permissions", (string)null);
            });

            // UserRole
            modelBuilder.Entity("USP.Core.Models.Entities.UserRole", b =>
            {
                b.Property<Guid>("UserId")
                    .HasColumnType("uuid")
                    .HasColumnName("user_id");

                b.Property<Guid>("RoleId")
                    .HasColumnType("uuid")
                    .HasColumnName("role_id");

                b.Property<DateTime>("AssignedAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("assigned_at");

                b.HasKey("UserId", "RoleId")
                    .HasName("pk_user_roles");

                b.HasIndex("RoleId")
                    .HasDatabaseName("ix_user_roles_role_id");

                b.ToTable("user_roles", (string)null);
            });

            // RolePermission
            modelBuilder.Entity("USP.Core.Models.Entities.RolePermission", b =>
            {
                b.Property<Guid>("RoleId")
                    .HasColumnType("uuid")
                    .HasColumnName("role_id");

                b.Property<Guid>("PermissionId")
                    .HasColumnType("uuid")
                    .HasColumnName("permission_id");

                b.Property<DateTime>("GrantedAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("granted_at");

                b.HasKey("RoleId", "PermissionId")
                    .HasName("pk_role_permissions");

                b.HasIndex("PermissionId")
                    .HasDatabaseName("ix_role_permissions_permission_id");

                b.ToTable("role_permissions", (string)null);
            });

            // AccessPolicy
            modelBuilder.Entity("USP.Core.Models.Entities.AccessPolicy", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<DateTime>("CreatedAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("created_at");

                b.Property<Guid>("CreatedBy")
                    .HasColumnType("uuid")
                    .HasColumnName("created_by");

                b.Property<string>("Description")
                    .HasMaxLength(1000)
                    .HasColumnType("character varying(1000)")
                    .HasColumnName("description");

                b.Property<bool>("IsActive")
                    .HasColumnType("boolean")
                    .HasColumnName("is_active");

                b.Property<string>("Name")
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnType("character varying(255)")
                    .HasColumnName("name");

                b.Property<string>("Policy")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("policy");

                b.Property<string>("PolicyType")
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasColumnType("character varying(50)")
                    .HasColumnName("policy_type");

                b.Property<DateTime>("UpdatedAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("updated_at");

                b.HasKey("Id")
                    .HasName("pk_access_policies");

                b.HasIndex("Name")
                    .IsUnique()
                    .HasDatabaseName("idx_access_policies_name");

                b.HasIndex("PolicyType")
                    .HasDatabaseName("idx_access_policies_policy_type");

                b.ToTable("access_policies", (string)null);
            });

            // Secret
            modelBuilder.Entity("USP.Core.Models.Entities.Secret", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<DateTime>("CreatedAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("created_at");

                b.Property<Guid>("CreatedBy")
                    .HasColumnType("uuid")
                    .HasColumnName("created_by");

                b.Property<DateTime?>("DeletedAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("deleted_at");

                b.Property<string>("EncryptedData")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("encrypted_data");

                b.Property<bool>("IsDeleted")
                    .HasColumnType("boolean")
                    .HasColumnName("is_deleted");

                b.Property<bool>("IsDestroyed")
                    .HasColumnType("boolean")
                    .HasColumnName("is_destroyed");

                b.Property<string>("Metadata")
                    .HasColumnType("jsonb")
                    .HasColumnName("metadata");

                b.Property<string>("Path")
                    .IsRequired()
                    .HasMaxLength(500)
                    .HasColumnType("character varying(500)")
                    .HasColumnName("path");

                b.Property<DateTime>("UpdatedAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("updated_at");

                b.Property<int>("Version")
                    .HasColumnType("integer")
                    .HasColumnName("version");

                b.HasKey("Id")
                    .HasName("pk_secrets");

                b.HasIndex("Path", "Version")
                    .HasDatabaseName("idx_secrets_path_version");

                b.ToTable("secrets", (string)null);
            });

            // SecretAccessLog
            modelBuilder.Entity("USP.Core.Models.Entities.SecretAccessLog", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<DateTime>("AccessedAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("accessed_at");

                b.Property<Guid>("AccessedBy")
                    .HasColumnType("uuid")
                    .HasColumnName("accessed_by");

                b.Property<string>("AccessType")
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasColumnType("character varying(50)")
                    .HasColumnName("access_type");

                b.Property<string>("IpAddress")
                    .HasColumnType("inet")
                    .HasColumnName("ip_address");

                b.Property<Guid>("SecretId")
                    .HasColumnType("uuid")
                    .HasColumnName("secret_id");

                b.Property<string>("UserAgent")
                    .HasColumnType("text")
                    .HasColumnName("user_agent");

                b.HasKey("Id")
                    .HasName("pk_secret_access_log");

                b.HasIndex("AccessedAt")
                    .HasDatabaseName("idx_secret_access_log_accessed_at");

                b.HasIndex("SecretId")
                    .HasDatabaseName("idx_secret_access_log_secret_id");

                b.ToTable("secret_access_log", (string)null);
            });

            // Session
            modelBuilder.Entity("USP.Core.Models.Entities.Session", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<DateTime>("CreatedAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("created_at");

                b.Property<string>("DeviceFingerprint")
                    .HasMaxLength(255)
                    .HasColumnType("character varying(255)")
                    .HasColumnName("device_fingerprint");

                b.Property<DateTime?>("ExpiresAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("expires_at");

                b.Property<string>("IpAddress")
                    .HasColumnType("inet")
                    .HasColumnName("ip_address");

                b.Property<bool>("IsActive")
                    .HasColumnType("boolean")
                    .HasColumnName("is_active");

                b.Property<DateTime?>("LastActivityAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("last_activity_at");

                b.Property<string>("RefreshToken")
                    .IsRequired()
                    .HasMaxLength(500)
                    .HasColumnType("character varying(500)")
                    .HasColumnName("refresh_token");

                b.Property<string>("UserAgent")
                    .HasColumnType("text")
                    .HasColumnName("user_agent");

                b.Property<Guid>("UserId")
                    .HasColumnType("uuid")
                    .HasColumnName("user_id");

                b.HasKey("Id")
                    .HasName("pk_sessions");

                b.HasIndex("RefreshToken")
                    .IsUnique()
                    .HasDatabaseName("idx_sessions_refresh_token");

                b.HasIndex("UserId")
                    .HasDatabaseName("idx_sessions_user_id");

                b.ToTable("sessions", (string)null);
            });

            // AuditLog
            modelBuilder.Entity("USP.Core.Models.Entities.AuditLog", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<string>("Action")
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnType("character varying(255)")
                    .HasColumnName("action");

                b.Property<DateTime>("CreatedAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("created_at");

                b.Property<string>("ErrorMessage")
                    .HasColumnType("text")
                    .HasColumnName("error_message");

                b.Property<string>("IpAddress")
                    .HasColumnType("inet")
                    .HasColumnName("ip_address");

                b.Property<string>("NewValue")
                    .HasColumnType("jsonb")
                    .HasColumnName("new_value");

                b.Property<string>("OldValue")
                    .HasColumnType("jsonb")
                    .HasColumnName("old_value");

                b.Property<string>("ResourceId")
                    .HasMaxLength(500)
                    .HasColumnType("character varying(500)")
                    .HasColumnName("resource_id");

                b.Property<string>("ResourceType")
                    .HasMaxLength(255)
                    .HasColumnType("character varying(255)")
                    .HasColumnName("resource_type");

                b.Property<string>("Status")
                    .HasMaxLength(50)
                    .HasColumnType("character varying(50)")
                    .HasColumnName("status");

                b.Property<string>("UserAgent")
                    .HasColumnType("text")
                    .HasColumnName("user_agent");

                b.Property<Guid?>("UserId")
                    .HasColumnType("uuid")
                    .HasColumnName("user_id");

                b.HasKey("Id")
                    .HasName("pk_audit_logs");

                b.HasIndex("Action")
                    .HasDatabaseName("idx_audit_logs_action");

                b.HasIndex("CreatedAt")
                    .HasDatabaseName("idx_audit_logs_created_at");

                b.HasIndex("ResourceType")
                    .HasDatabaseName("idx_audit_logs_resource_type");

                b.HasIndex("UserId")
                    .HasDatabaseName("idx_audit_logs_user_id");

                b.ToTable("audit_logs", (string)null);
            });

            // ApiKey
            modelBuilder.Entity("USP.Core.Models.Entities.ApiKey", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<DateTime>("CreatedAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("created_at");

                b.Property<DateTime?>("ExpiresAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("expires_at");

                b.Property<string>("KeyHash")
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnType("character varying(255)")
                    .HasColumnName("key_hash");

                b.Property<string>("KeyPrefix")
                    .IsRequired()
                    .HasMaxLength(20)
                    .HasColumnType("character varying(20)")
                    .HasColumnName("key_prefix");

                b.Property<DateTime?>("LastUsedAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("last_used_at");

                b.Property<string>("Name")
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnType("character varying(255)")
                    .HasColumnName("name");

                b.Property<bool>("Revoked")
                    .HasColumnType("boolean")
                    .HasColumnName("revoked");

                b.Property<DateTime?>("RevokedAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("revoked_at");

                b.Property<string>("Scopes")
                    .HasColumnType("text")
                    .HasColumnName("scopes");

                b.Property<Guid>("UserId")
                    .HasColumnType("uuid")
                    .HasColumnName("user_id");

                b.HasKey("Id")
                    .HasName("pk_api_keys");

                b.HasIndex("KeyHash")
                    .IsUnique()
                    .HasDatabaseName("idx_api_keys_key_hash");

                b.HasIndex("Revoked")
                    .HasDatabaseName("idx_api_keys_revoked");

                b.HasIndex("UserId")
                    .HasDatabaseName("idx_api_keys_user_id");

                b.ToTable("api_keys", (string)null);
            });

            // MfaDevice
            modelBuilder.Entity("USP.Core.Models.Entities.MfaDevice", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<string>("DeviceName")
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnType("character varying(255)")
                    .HasColumnName("device_name");

                b.Property<string>("DeviceType")
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasColumnType("character varying(50)")
                    .HasColumnName("device_type");

                b.Property<bool>("IsActive")
                    .HasColumnType("boolean")
                    .HasColumnName("is_active");

                b.Property<bool>("IsPrimary")
                    .HasColumnType("boolean")
                    .HasColumnName("is_primary");

                b.Property<DateTime?>("LastUsedAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("last_used_at");

                b.Property<DateTime>("RegisteredAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("registered_at");

                b.Property<Guid>("UserId")
                    .HasColumnType("uuid")
                    .HasColumnName("user_id");

                b.HasKey("Id")
                    .HasName("pk_mfa_devices");

                b.HasIndex("UserId")
                    .HasDatabaseName("idx_mfa_devices_user_id");

                b.ToTable("mfa_devices", (string)null);
            });

            // MfaBackupCode
            modelBuilder.Entity("USP.Core.Models.Entities.MfaBackupCode", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<string>("CodeHash")
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnType("character varying(255)")
                    .HasColumnName("code_hash");

                b.Property<DateTime>("CreatedAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("created_at");

                b.Property<bool>("IsUsed")
                    .HasColumnType("boolean")
                    .HasColumnName("is_used");

                b.Property<DateTime?>("UsedAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("used_at");

                b.Property<Guid>("UserId")
                    .HasColumnType("uuid")
                    .HasColumnName("user_id");

                b.HasKey("Id")
                    .HasName("pk_mfa_backup_codes");

                b.HasIndex("UserId")
                    .HasDatabaseName("idx_mfa_backup_codes_user_id");

                b.ToTable("mfa_backup_codes", (string)null);
            });

            // TrustedDevice
            modelBuilder.Entity("USP.Core.Models.Entities.TrustedDevice", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<string>("DeviceFingerprint")
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnType("character varying(255)")
                    .HasColumnName("device_fingerprint");

                b.Property<string>("DeviceName")
                    .HasMaxLength(255)
                    .HasColumnType("character varying(255)")
                    .HasColumnName("device_name");

                b.Property<string>("DeviceType")
                    .HasMaxLength(100)
                    .HasColumnType("character varying(100)")
                    .HasColumnName("device_type");

                b.Property<bool>("IsActive")
                    .HasColumnType("boolean")
                    .HasColumnName("is_active");

                b.Property<DateTime?>("LastUsedAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("last_used_at");

                b.Property<DateTime>("RegisteredAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("registered_at");

                b.Property<Guid>("UserId")
                    .HasColumnType("uuid")
                    .HasColumnName("user_id");

                b.HasKey("Id")
                    .HasName("pk_trusted_devices");

                b.HasIndex("UserId")
                    .HasDatabaseName("idx_trusted_devices_user_id");

                b.HasIndex("DeviceFingerprint")
                    .HasDatabaseName("idx_trusted_devices_fingerprint");

                b.ToTable("trusted_devices", (string)null);
            });

            // SealConfiguration
            modelBuilder.Entity("USP.Core.Models.Entities.SealConfiguration", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<DateTime>("CreatedAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("created_at");

                b.Property<string>("EncryptedMasterKey")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("encrypted_master_key");

                b.Property<bool>("Initialized")
                    .HasColumnType("boolean")
                    .HasColumnName("initialized");

                b.Property<DateTime?>("LastUnsealedAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("last_unsealed_at");

                b.Property<int>("SecretShares")
                    .HasColumnType("integer")
                    .HasColumnName("secret_shares");

                b.Property<int>("SecretThreshold")
                    .HasColumnType("integer")
                    .HasColumnName("secret_threshold");

                b.Property<int>("Version")
                    .HasColumnType("integer")
                    .HasColumnName("version");

                b.HasKey("Id")
                    .HasName("pk_seal_configurations");

                b.ToTable("seal_configurations", (string)null);
            });

            // Configure relationships
            modelBuilder.Entity("USP.Core.Models.Entities.UserRole", b =>
            {
                b.HasOne("USP.Core.Models.Entities.Role", "Role")
                    .WithMany("UserRoles")
                    .HasForeignKey("RoleId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                b.HasOne("USP.Core.Models.Entities.ApplicationUser", "User")
                    .WithMany("UserRoles")
                    .HasForeignKey("UserId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                b.Navigation("Role");
                b.Navigation("User");
            });

            modelBuilder.Entity("USP.Core.Models.Entities.RolePermission", b =>
            {
                b.HasOne("USP.Core.Models.Entities.Permission", "Permission")
                    .WithMany("RolePermissions")
                    .HasForeignKey("PermissionId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                b.HasOne("USP.Core.Models.Entities.Role", "Role")
                    .WithMany("RolePermissions")
                    .HasForeignKey("RoleId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                b.Navigation("Permission");
                b.Navigation("Role");
            });

            modelBuilder.Entity("USP.Core.Models.Entities.SecretAccessLog", b =>
            {
                b.HasOne("USP.Core.Models.Entities.ApplicationUser", "Accessor")
                    .WithMany("SecretAccessLogs")
                    .HasForeignKey("AccessedBy")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();

                b.HasOne("USP.Core.Models.Entities.Secret", "Secret")
                    .WithMany("AccessLogs")
                    .HasForeignKey("SecretId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                b.Navigation("Accessor");
                b.Navigation("Secret");
            });

            modelBuilder.Entity("USP.Core.Models.Entities.AuditLog", b =>
            {
                b.HasOne("USP.Core.Models.Entities.ApplicationUser", "User")
                    .WithMany("AuditLogs")
                    .HasForeignKey("UserId")
                    .OnDelete(DeleteBehavior.Restrict);

                b.Navigation("User");
            });

            modelBuilder.Entity("USP.Core.Models.Entities.ApiKey", b =>
            {
                b.HasOne("USP.Core.Models.Entities.ApplicationUser", "User")
                    .WithMany("ApiKeys")
                    .HasForeignKey("UserId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                b.Navigation("User");
            });

            modelBuilder.Entity("USP.Core.Models.Entities.ApplicationUser", b =>
            {
                b.Navigation("ApiKeys");
                b.Navigation("AuditLogs");
                b.Navigation("SecretAccessLogs");
                b.Navigation("UserRoles");
            });

            modelBuilder.Entity("USP.Core.Models.Entities.Role", b =>
            {
                b.Navigation("RolePermissions");
                b.Navigation("UserRoles");
            });

            modelBuilder.Entity("USP.Core.Models.Entities.Permission", b =>
            {
                b.Navigation("RolePermissions");
            });

            modelBuilder.Entity("USP.Core.Models.Entities.Secret", b =>
            {
                b.Navigation("AccessLogs");
            });

#pragma warning restore 612, 618
        }
    }
}

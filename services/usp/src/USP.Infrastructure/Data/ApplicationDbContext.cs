using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using USP.Core.Models.Entities;
using USP.Infrastructure.Data.Configurations;

namespace USP.Infrastructure.Data;

/// <summary>
/// Application database context for USP (Unified Security Platform)
/// Maps to existing PostgreSQL schema in config/postgres/init-scripts/05-usp-schema.sql
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, Role, Guid, IdentityUserClaim<Guid>, UserRole, IdentityUserLogin<Guid>, IdentityRoleClaim<Guid>, IdentityUserToken<Guid>>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // DbSets for entities
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }
    public DbSet<AccessPolicy> AccessPolicies { get; set; }
    public DbSet<Secret> Secrets { get; set; }
    public DbSet<SecretAccessLog> SecretAccessLogs { get; set; }
    public DbSet<Session> Sessions { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<ApiKey> ApiKeys { get; set; }
    public DbSet<MfaDevice> MfaDevices { get; set; }
    public DbSet<MfaBackupCode> MfaBackupCodes { get; set; }
    public DbSet<TrustedDevice> TrustedDevices { get; set; }
    public DbSet<SealConfiguration> SealConfigurations { get; set; }
    public DbSet<AuthorizationFlow> AuthorizationFlows { get; set; }
    public DbSet<AuthorizationFlowInstance> AuthorizationFlowInstances { get; set; }
    public DbSet<FlowApproval> FlowApprovals { get; set; }
    public DbSet<WebAuthnCredential> WebAuthnCredentials { get; set; }
    public DbSet<OAuth2Client> OAuth2Clients { get; set; }
    public DbSet<OAuth2AuthorizationCode> OAuth2AuthorizationCodes { get; set; }
    public DbSet<MagicLink> MagicLinks { get; set; }
    public DbSet<SamlIdentityProvider> SamlIdentityProviders { get; set; }
    public DbSet<LdapConfiguration> LdapConfigurations { get; set; }
    public DbSet<RiskAssessment> RiskAssessments { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply entity configurations
        builder.ApplyConfiguration(new ApplicationUserConfiguration());
        builder.ApplyConfiguration(new RoleConfiguration());
        builder.ApplyConfiguration(new PermissionConfiguration());
        builder.ApplyConfiguration(new UserRoleConfiguration());
        builder.ApplyConfiguration(new AccessPolicyConfiguration());
        builder.ApplyConfiguration(new SecretConfiguration());
        builder.ApplyConfiguration(new SessionConfiguration());

        // Configure RolePermission
        builder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("role_permissions");
            entity.HasKey(rp => new { rp.RoleId, rp.PermissionId });

            entity.Property(rp => rp.RoleId).HasColumnName("role_id");
            entity.Property(rp => rp.PermissionId).HasColumnName("permission_id");
            entity.Property(rp => rp.GrantedAt).HasColumnName("granted_at");

            entity.HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure SecretAccessLog
        builder.Entity<SecretAccessLog>(entity =>
        {
            entity.ToTable("secret_access_log");
            entity.HasKey(sal => sal.Id);

            entity.Property(sal => sal.Id).HasColumnName("id");
            entity.Property(sal => sal.SecretId).HasColumnName("secret_id");
            entity.Property(sal => sal.AccessedBy).HasColumnName("accessed_by");
            entity.Property(sal => sal.AccessType).HasColumnName("access_type").HasMaxLength(50);
            entity.Property(sal => sal.IpAddress).HasColumnName("ip_address").HasColumnType("inet");
            entity.Property(sal => sal.UserAgent).HasColumnName("user_agent");
            entity.Property(sal => sal.AccessedAt).HasColumnName("accessed_at");

            entity.HasOne(sal => sal.Secret)
                .WithMany(s => s.AccessLogs)
                .HasForeignKey(sal => sal.SecretId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(sal => sal.Accessor)
                .WithMany(u => u.SecretAccessLogs)
                .HasForeignKey(sal => sal.AccessedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(sal => sal.SecretId).HasDatabaseName("idx_secret_access_log_secret_id");
            entity.HasIndex(sal => sal.AccessedAt).HasDatabaseName("idx_secret_access_log_accessed_at");
        });

        // Configure AuditLog
        builder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(al => al.Id);

            entity.Property(al => al.Id).HasColumnName("id");
            entity.Property(al => al.UserId).HasColumnName("user_id");
            entity.Property(al => al.Action).HasColumnName("action").HasMaxLength(255);
            entity.Property(al => al.ResourceType).HasColumnName("resource_type").HasMaxLength(255);
            entity.Property(al => al.ResourceId).HasColumnName("resource_id").HasMaxLength(500);
            entity.Property(al => al.OldValue).HasColumnName("old_value").HasColumnType("jsonb");
            entity.Property(al => al.NewValue).HasColumnName("new_value").HasColumnType("jsonb");
            entity.Property(al => al.IpAddress).HasColumnName("ip_address").HasColumnType("inet");
            entity.Property(al => al.UserAgent).HasColumnName("user_agent");
            entity.Property(al => al.Status).HasColumnName("status").HasMaxLength(50);
            entity.Property(al => al.ErrorMessage).HasColumnName("error_message");
            entity.Property(al => al.CreatedAt).HasColumnName("created_at");

            entity.HasOne(al => al.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(al => al.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(al => al.UserId).HasDatabaseName("idx_audit_logs_user_id");
            entity.HasIndex(al => al.Action).HasDatabaseName("idx_audit_logs_action");
            entity.HasIndex(al => al.ResourceType).HasDatabaseName("idx_audit_logs_resource_type");
            entity.HasIndex(al => al.CreatedAt).HasDatabaseName("idx_audit_logs_created_at");
        });

        // Configure ApiKey
        builder.Entity<ApiKey>(entity =>
        {
            entity.ToTable("api_keys");
            entity.HasKey(ak => ak.Id);

            entity.Property(ak => ak.Id).HasColumnName("id");
            entity.Property(ak => ak.UserId).HasColumnName("user_id");
            entity.Property(ak => ak.Name).HasColumnName("name").HasMaxLength(255);
            entity.Property(ak => ak.KeyHash).HasColumnName("key_hash").HasMaxLength(255);
            entity.Property(ak => ak.KeyPrefix).HasColumnName("key_prefix").HasMaxLength(20);
            entity.Property(ak => ak.Scopes).HasColumnName("scopes");
            entity.Property(ak => ak.LastUsedAt).HasColumnName("last_used_at");
            entity.Property(ak => ak.CreatedAt).HasColumnName("created_at");
            entity.Property(ak => ak.ExpiresAt).HasColumnName("expires_at");
            entity.Property(ak => ak.Revoked).HasColumnName("revoked");
            entity.Property(ak => ak.RevokedAt).HasColumnName("revoked_at");

            entity.HasOne(ak => ak.User)
                .WithMany(u => u.ApiKeys)
                .HasForeignKey(ak => ak.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(ak => ak.UserId).HasDatabaseName("idx_api_keys_user_id");
            entity.HasIndex(ak => ak.KeyHash).HasDatabaseName("idx_api_keys_key_hash").IsUnique();
            entity.HasIndex(ak => ak.Revoked).HasDatabaseName("idx_api_keys_revoked");
        });

        // Ignore Identity tables we don't need
        builder.Ignore<IdentityUserClaim<Guid>>();
        builder.Ignore<IdentityUserLogin<Guid>>();
        builder.Ignore<IdentityRoleClaim<Guid>>();
        builder.Ignore<IdentityUserToken<Guid>>();
    }
}

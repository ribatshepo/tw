using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using USP.Core.Models.Entities;
using USP.Infrastructure.Data.Configurations;

namespace USP.Infrastructure.Data;

/// <summary>
/// Application database context for USP (Unified Security Platform)
/// Maps to existing PostgreSQL schema in config/postgres/init-scripts/05-usp-schema.sql
/// Supports multi-tenancy with automatic query filtering
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, Role, Guid, IdentityUserClaim<Guid>, UserRole, IdentityUserLogin<Guid>, IdentityRoleClaim<Guid>, IdentityUserToken<Guid>>
{
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        IHttpContextAccessor httpContextAccessor)
        : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
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
    public DbSet<UserRiskProfile> UserRiskProfiles { get; set; }
    public DbSet<ComplianceReport> ComplianceReports { get; set; }
    public DbSet<ComplianceControl> ComplianceControls { get; set; }
    public DbSet<Core.Models.Entities.Webhook> Webhooks { get; set; }
    public DbSet<WebhookDelivery> WebhookDeliveries { get; set; }
    public DbSet<PrivilegedSafe> PrivilegedSafes { get; set; }
    public DbSet<PrivilegedAccount> PrivilegedAccounts { get; set; }
    public DbSet<AccountCheckout> AccountCheckouts { get; set; }
    public DbSet<AccessApproval> AccessApprovals { get; set; }
    public DbSet<PrivilegedSession> PrivilegedSessions { get; set; }
    public DbSet<SessionCommand> SessionCommands { get; set; }
    public DbSet<JitAccess> JitAccesses { get; set; }
    public DbSet<JitAccessTemplate> JitAccessTemplates { get; set; }
    public DbSet<BreakGlassAccess> BreakGlassAccesses { get; set; }
    public DbSet<BreakGlassPolicy> BreakGlassPolicies { get; set; }
    public DbSet<TransitKey> TransitKeys { get; set; }
    public DbSet<TransitKeyVersion> TransitKeyVersions { get; set; }
    public DbSet<PkiCertificateAuthority> PkiCertificateAuthorities { get; set; }
    public DbSet<PkiRole> PkiRoles { get; set; }
    public DbSet<PkiIssuedCertificate> PkiIssuedCertificates { get; set; }
    public DbSet<DatabaseConfig> DatabaseConfigs { get; set; }
    public DbSet<DatabaseRole> DatabaseRoles { get; set; }
    public DbSet<DatabaseLease> DatabaseLeases { get; set; }

    // Workspace/Multi-tenancy DbSets
    public DbSet<USP.Core.Models.Entities.Workspace> Workspaces { get; set; }
    public DbSet<WorkspaceMember> WorkspaceMembers { get; set; }
    public DbSet<WorkspaceQuota> WorkspaceQuotas { get; set; }
    public DbSet<WorkspaceUsage> WorkspaceUsages { get; set; }

    // HIPAA Compliance DbSets
    public DbSet<UserClearance> UserClearances { get; set; }
    public DbSet<BusinessAssociateAgreement> BusinessAssociateAgreements { get; set; }

    // Authorization Security DbSets
    public DbSet<ColumnSecurityRule> ColumnSecurityRules { get; set; }
    public DbSet<ContextPolicy> ContextPolicies { get; set; }

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
        builder.ApplyConfiguration(new TransitKeyConfiguration());
        builder.ApplyConfiguration(new TransitKeyVersionConfiguration());
        builder.ApplyConfiguration(new PkiCertificateAuthorityConfiguration());
        builder.ApplyConfiguration(new PkiRoleConfiguration());
        builder.ApplyConfiguration(new PkiIssuedCertificateConfiguration());
        builder.ApplyConfiguration(new SamlIdentityProviderConfiguration());
        builder.ApplyConfiguration(new LdapConfigurationConfiguration());
        builder.ApplyConfiguration(new DatabaseConfigConfiguration());
        builder.ApplyConfiguration(new DatabaseRoleConfiguration());
        builder.ApplyConfiguration(new DatabaseLeaseConfiguration());

        // Apply workspace configurations
        builder.ApplyConfiguration(new WorkspaceConfiguration());
        builder.ApplyConfiguration(new WorkspaceMemberConfiguration());
        builder.ApplyConfiguration(new WorkspaceQuotaConfiguration());
        builder.ApplyConfiguration(new WorkspaceUsageConfiguration());

        // Apply HIPAA compliance configurations
        builder.ApplyConfiguration(new UserClearanceConfiguration());
        builder.ApplyConfiguration(new BusinessAssociateAgreementConfiguration());

        // Apply authorization security configurations
        builder.ApplyConfiguration(new ColumnSecurityRuleConfiguration());
        builder.ApplyConfiguration(new ContextPolicyConfiguration());

        // Apply global query filters for tenant isolation
        ApplyTenantQueryFilters(builder);

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
            entity.Property(sal => sal.IpAddress).HasColumnName("ip_address");
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
            entity.Property(al => al.OldValue).HasColumnName("old_value");
            entity.Property(al => al.NewValue).HasColumnName("new_value");
            entity.Property(al => al.IpAddress).HasColumnName("ip_address");
            entity.Property(al => al.UserAgent).HasColumnName("user_agent");
            entity.Property(al => al.Status).HasColumnName("status").HasMaxLength(50);
            entity.Property(al => al.ErrorMessage).HasColumnName("error_message");
            entity.Property(al => al.CreatedAt).HasColumnName("created_at");
            entity.Property(al => al.PreviousHash).HasColumnName("previous_hash").HasMaxLength(500);
            entity.Property(al => al.CurrentHash).HasColumnName("current_hash").HasMaxLength(500);
            entity.Property(al => al.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100);

            entity.HasOne(al => al.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(al => al.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(al => al.UserId).HasDatabaseName("idx_audit_logs_user_id");
            entity.HasIndex(al => al.Action).HasDatabaseName("idx_audit_logs_action");
            entity.HasIndex(al => al.ResourceType).HasDatabaseName("idx_audit_logs_resource_type");
            entity.HasIndex(al => al.CreatedAt).HasDatabaseName("idx_audit_logs_created_at");
            entity.HasIndex(al => al.CorrelationId).HasDatabaseName("idx_audit_logs_correlation_id");
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

        // Configure ComplianceReport
        builder.Entity<ComplianceReport>(entity =>
        {
            entity.ToTable("compliance_reports");
            entity.HasKey(cr => cr.Id);

            entity.Property(cr => cr.Id).HasColumnName("id");
            entity.Property(cr => cr.Framework).HasColumnName("framework").HasMaxLength(100);
            entity.Property(cr => cr.ReportType).HasColumnName("report_type").HasMaxLength(100);
            entity.Property(cr => cr.GeneratedAt).HasColumnName("generated_at");
            entity.Property(cr => cr.PeriodStart).HasColumnName("period_start");
            entity.Property(cr => cr.PeriodEnd).HasColumnName("period_end");
            entity.Property(cr => cr.GeneratedBy).HasColumnName("generated_by");
            entity.Property(cr => cr.Status).HasColumnName("status").HasMaxLength(50);
            entity.Property(cr => cr.ReportPath).HasColumnName("report_path").HasMaxLength(1000);
            entity.Property(cr => cr.Format).HasColumnName("format").HasMaxLength(50);
            entity.Property(cr => cr.TotalControls).HasColumnName("total_controls");
            entity.Property(cr => cr.ImplementedControls).HasColumnName("implemented_controls");
            entity.Property(cr => cr.PartialControls).HasColumnName("partial_controls");
            entity.Property(cr => cr.NotImplementedControls).HasColumnName("not_implemented_controls");
            entity.Property(cr => cr.ComplianceScore).HasColumnName("compliance_score");
            entity.Property(cr => cr.Summary).HasColumnName("summary");
            entity.Property(cr => cr.Recommendations).HasColumnName("recommendations");

            entity.HasOne(cr => cr.GeneratedByUser)
                .WithMany()
                .HasForeignKey(cr => cr.GeneratedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(cr => cr.Framework).HasDatabaseName("idx_compliance_reports_framework");
            entity.HasIndex(cr => cr.GeneratedAt).HasDatabaseName("idx_compliance_reports_generated_at");
            entity.HasIndex(cr => cr.Status).HasDatabaseName("idx_compliance_reports_status");
        });

        // Configure ComplianceControl
        builder.Entity<ComplianceControl>(entity =>
        {
            entity.ToTable("compliance_controls");
            entity.HasKey(cc => cc.Id);

            entity.Property(cc => cc.Id).HasColumnName("id");
            entity.Property(cc => cc.ReportId).HasColumnName("report_id");
            entity.Property(cc => cc.ControlId).HasColumnName("control_id").HasMaxLength(100);
            entity.Property(cc => cc.ControlName).HasColumnName("control_name").HasMaxLength(500);
            entity.Property(cc => cc.ControlDescription).HasColumnName("control_description");
            entity.Property(cc => cc.Category).HasColumnName("category").HasMaxLength(200);
            entity.Property(cc => cc.Status).HasColumnName("status").HasMaxLength(50);
            entity.Property(cc => cc.Implementation).HasColumnName("implementation");
            entity.Property(cc => cc.Evidence).HasColumnName("evidence");
            entity.Property(cc => cc.Gaps).HasColumnName("gaps");
            entity.Property(cc => cc.LastAssessed).HasColumnName("last_assessed");
            entity.Property(cc => cc.AssessedBy).HasColumnName("assessed_by");

            entity.HasOne(cc => cc.Report)
                .WithMany(cr => cr.Controls)
                .HasForeignKey(cc => cc.ReportId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(cc => cc.AssessedByUser)
                .WithMany()
                .HasForeignKey(cc => cc.AssessedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(cc => cc.ReportId).HasDatabaseName("idx_compliance_controls_report_id");
            entity.HasIndex(cc => cc.ControlId).HasDatabaseName("idx_compliance_controls_control_id");
            entity.HasIndex(cc => cc.Status).HasDatabaseName("idx_compliance_controls_status");
        });

        // Configure Webhook
        builder.Entity<Core.Models.Entities.Webhook>(entity =>
        {
            entity.ToTable("webhooks");
            entity.HasKey(w => w.Id);

            entity.Property(w => w.Id).HasColumnName("id");
            entity.Property(w => w.UserId).HasColumnName("user_id");
            entity.Property(w => w.Name).HasColumnName("name").HasMaxLength(255);
            entity.Property(w => w.Url).HasColumnName("url").HasMaxLength(2000);
            entity.Property(w => w.Description).HasColumnName("description");
            entity.Property(w => w.Events).HasColumnName("events").HasColumnType("jsonb");
            entity.Property(w => w.Active).HasColumnName("active");
            entity.Property(w => w.AuthenticationType).HasColumnName("authentication_type").HasMaxLength(50);
            entity.Property(w => w.SecretToken).HasColumnName("secret_token").HasMaxLength(500);
            entity.Property(w => w.OAuth2ClientId).HasColumnName("oauth2_client_id").HasMaxLength(255);
            entity.Property(w => w.OAuth2ClientSecret).HasColumnName("oauth2_client_secret").HasMaxLength(500);
            entity.Property(w => w.OAuth2TokenUrl).HasColumnName("oauth2_token_url").HasMaxLength(2000);
            entity.Property(w => w.CustomHeaders).HasColumnName("custom_headers").HasColumnType("jsonb");
            entity.Property(w => w.PayloadTemplate).HasColumnName("payload_template");
            entity.Property(w => w.MaxRetries).HasColumnName("max_retries");
            entity.Property(w => w.TimeoutSeconds).HasColumnName("timeout_seconds");
            entity.Property(w => w.VerifySsl).HasColumnName("verify_ssl");
            entity.Property(w => w.CircuitBreakerState).HasColumnName("circuit_breaker_state").HasMaxLength(50);
            entity.Property(w => w.ConsecutiveFailures).HasColumnName("consecutive_failures");
            entity.Property(w => w.CircuitBreakerOpenedAt).HasColumnName("circuit_breaker_opened_at");
            entity.Property(w => w.CircuitBreakerThreshold).HasColumnName("circuit_breaker_threshold");
            entity.Property(w => w.CircuitBreakerResetMinutes).HasColumnName("circuit_breaker_reset_minutes");
            entity.Property(w => w.TotalDeliveries).HasColumnName("total_deliveries");
            entity.Property(w => w.SuccessfulDeliveries).HasColumnName("successful_deliveries");
            entity.Property(w => w.FailedDeliveries).HasColumnName("failed_deliveries");
            entity.Property(w => w.LastTriggeredAt).HasColumnName("last_triggered_at");
            entity.Property(w => w.LastSuccessAt).HasColumnName("last_success_at");
            entity.Property(w => w.LastFailureAt).HasColumnName("last_failure_at");
            entity.Property(w => w.CreatedAt).HasColumnName("created_at");
            entity.Property(w => w.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(w => w.User)
                .WithMany()
                .HasForeignKey(w => w.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(w => w.UserId).HasDatabaseName("idx_webhooks_user_id");
            entity.HasIndex(w => w.Active).HasDatabaseName("idx_webhooks_active");
            entity.HasIndex(w => w.Events).HasDatabaseName("idx_webhooks_events").HasMethod("gin");
        });

        // Configure WebhookDelivery
        builder.Entity<WebhookDelivery>(entity =>
        {
            entity.ToTable("webhook_deliveries");
            entity.HasKey(wd => wd.Id);

            entity.Property(wd => wd.Id).HasColumnName("id");
            entity.Property(wd => wd.WebhookId).HasColumnName("webhook_id");
            entity.Property(wd => wd.EventType).HasColumnName("event_type").HasMaxLength(255);
            entity.Property(wd => wd.Payload).HasColumnName("payload").HasColumnType("jsonb");
            entity.Property(wd => wd.Status).HasColumnName("status").HasMaxLength(50);
            entity.Property(wd => wd.AttemptCount).HasColumnName("attempt_count");
            entity.Property(wd => wd.ResponseStatus).HasColumnName("response_status");
            entity.Property(wd => wd.ResponseBody).HasColumnName("response_body");
            entity.Property(wd => wd.ErrorMessage).HasColumnName("error_message");
            entity.Property(wd => wd.DurationMs).HasColumnName("duration_ms");
            entity.Property(wd => wd.HmacSignature).HasColumnName("hmac_signature").HasMaxLength(500);
            entity.Property(wd => wd.CreatedAt).HasColumnName("created_at");
            entity.Property(wd => wd.DeliveredAt).HasColumnName("delivered_at");
            entity.Property(wd => wd.NextRetryAt).HasColumnName("next_retry_at");

            entity.HasOne(wd => wd.Webhook)
                .WithMany(w => w.Deliveries)
                .HasForeignKey(wd => wd.WebhookId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(wd => wd.WebhookId).HasDatabaseName("idx_webhook_deliveries_webhook_id");
            entity.HasIndex(wd => wd.EventType).HasDatabaseName("idx_webhook_deliveries_event_type");
            entity.HasIndex(wd => wd.Status).HasDatabaseName("idx_webhook_deliveries_status");
            entity.HasIndex(wd => wd.CreatedAt).HasDatabaseName("idx_webhook_deliveries_created_at");
            entity.HasIndex(wd => wd.NextRetryAt).HasDatabaseName("idx_webhook_deliveries_next_retry_at");
        });

        // Configure PrivilegedSafe
        builder.Entity<PrivilegedSafe>(entity =>
        {
            entity.ToTable("privileged_safes");
            entity.HasKey(ps => ps.Id);

            entity.Property(ps => ps.Id).HasColumnName("id");
            entity.Property(ps => ps.Name).HasColumnName("name").HasMaxLength(255);
            entity.Property(ps => ps.Description).HasColumnName("description");
            entity.Property(ps => ps.OwnerId).HasColumnName("owner_id");
            entity.Property(ps => ps.SafeType).HasColumnName("safe_type").HasMaxLength(50);
            entity.Property(ps => ps.AccessControl).HasColumnName("access_control").HasColumnType("jsonb");
            entity.Property(ps => ps.RequireApproval).HasColumnName("require_approval");
            entity.Property(ps => ps.RequireDualControl).HasColumnName("require_dual_control");
            entity.Property(ps => ps.MaxCheckoutDurationMinutes).HasColumnName("max_checkout_duration_minutes");
            entity.Property(ps => ps.RotateOnCheckin).HasColumnName("rotate_on_checkin");
            entity.Property(ps => ps.SessionRecordingEnabled).HasColumnName("session_recording_enabled");
            entity.Property(ps => ps.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
            entity.Property(ps => ps.CreatedAt).HasColumnName("created_at");
            entity.Property(ps => ps.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(ps => ps.Owner)
                .WithMany()
                .HasForeignKey(ps => ps.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(ps => ps.OwnerId).HasDatabaseName("idx_privileged_safes_owner_id");
            entity.HasIndex(ps => ps.SafeType).HasDatabaseName("idx_privileged_safes_safe_type");
        });

        // Configure PrivilegedAccount
        builder.Entity<PrivilegedAccount>(entity =>
        {
            entity.ToTable("privileged_accounts");
            entity.HasKey(pa => pa.Id);

            entity.Property(pa => pa.Id).HasColumnName("id");
            entity.Property(pa => pa.SafeId).HasColumnName("safe_id");
            entity.Property(pa => pa.AccountName).HasColumnName("account_name").HasMaxLength(255);
            entity.Property(pa => pa.Username).HasColumnName("username").HasMaxLength(255);
            entity.Property(pa => pa.EncryptedPassword).HasColumnName("encrypted_password");
            entity.Property(pa => pa.Platform).HasColumnName("platform").HasMaxLength(100);
            entity.Property(pa => pa.HostAddress).HasColumnName("host_address").HasMaxLength(500);
            entity.Property(pa => pa.Port).HasColumnName("port");
            entity.Property(pa => pa.DatabaseName).HasColumnName("database_name").HasMaxLength(255);
            entity.Property(pa => pa.ConnectionDetails).HasColumnName("connection_details").HasColumnType("jsonb");
            entity.Property(pa => pa.RotationPolicy).HasColumnName("rotation_policy").HasMaxLength(50);
            entity.Property(pa => pa.RotationIntervalDays).HasColumnName("rotation_interval_days");
            entity.Property(pa => pa.LastRotated).HasColumnName("last_rotated");
            entity.Property(pa => pa.NextRotation).HasColumnName("next_rotation");
            entity.Property(pa => pa.Status).HasColumnName("status").HasMaxLength(50);
            entity.Property(pa => pa.PasswordComplexity).HasColumnName("password_complexity");
            entity.Property(pa => pa.RequireMfa).HasColumnName("require_mfa");
            entity.Property(pa => pa.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
            entity.Property(pa => pa.CreatedAt).HasColumnName("created_at");
            entity.Property(pa => pa.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(pa => pa.Safe)
                .WithMany(ps => ps.Accounts)
                .HasForeignKey(pa => pa.SafeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(pa => pa.SafeId).HasDatabaseName("idx_privileged_accounts_safe_id");
            entity.HasIndex(pa => pa.Platform).HasDatabaseName("idx_privileged_accounts_platform");
            entity.HasIndex(pa => pa.Status).HasDatabaseName("idx_privileged_accounts_status");
            entity.HasIndex(pa => pa.NextRotation).HasDatabaseName("idx_privileged_accounts_next_rotation");
        });

        // Configure AccountCheckout
        builder.Entity<AccountCheckout>(entity =>
        {
            entity.ToTable("account_checkouts");
            entity.HasKey(ac => ac.Id);

            entity.Property(ac => ac.Id).HasColumnName("id");
            entity.Property(ac => ac.AccountId).HasColumnName("account_id");
            entity.Property(ac => ac.UserId).HasColumnName("user_id");
            entity.Property(ac => ac.CheckedOutAt).HasColumnName("checked_out_at");
            entity.Property(ac => ac.CheckedInAt).HasColumnName("checked_in_at");
            entity.Property(ac => ac.ExpiresAt).HasColumnName("expires_at");
            entity.Property(ac => ac.Reason).HasColumnName("reason");
            entity.Property(ac => ac.ApprovalId).HasColumnName("approval_id");
            entity.Property(ac => ac.Status).HasColumnName("status").HasMaxLength(50);
            entity.Property(ac => ac.RotateOnCheckin).HasColumnName("rotate_on_checkin");
            entity.Property(ac => ac.WasRotated).HasColumnName("was_rotated");
            entity.Property(ac => ac.SessionRecordingPath).HasColumnName("session_recording_path").HasMaxLength(1000);
            entity.Property(ac => ac.IpAddress).HasColumnName("ip_address");
            entity.Property(ac => ac.UserAgent).HasColumnName("user_agent");
            entity.Property(ac => ac.Metadata).HasColumnName("metadata").HasColumnType("jsonb");

            entity.HasOne(ac => ac.Account)
                .WithMany(pa => pa.Checkouts)
                .HasForeignKey(ac => ac.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ac => ac.User)
                .WithMany()
                .HasForeignKey(ac => ac.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ac => ac.Approval)
                .WithMany(aa => aa.Checkouts)
                .HasForeignKey(ac => ac.ApprovalId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(ac => ac.AccountId).HasDatabaseName("idx_account_checkouts_account_id");
            entity.HasIndex(ac => ac.UserId).HasDatabaseName("idx_account_checkouts_user_id");
            entity.HasIndex(ac => ac.Status).HasDatabaseName("idx_account_checkouts_status");
            entity.HasIndex(ac => ac.ExpiresAt).HasDatabaseName("idx_account_checkouts_expires_at");
        });

        // Configure AccessApproval
        builder.Entity<AccessApproval>(entity =>
        {
            entity.ToTable("access_approvals");
            entity.HasKey(aa => aa.Id);

            entity.Property(aa => aa.Id).HasColumnName("id");
            entity.Property(aa => aa.RequesterId).HasColumnName("requester_id");
            entity.Property(aa => aa.ResourceType).HasColumnName("resource_type").HasMaxLength(100);
            entity.Property(aa => aa.ResourceId).HasColumnName("resource_id");
            entity.Property(aa => aa.Reason).HasColumnName("reason");
            entity.Property(aa => aa.Status).HasColumnName("status").HasMaxLength(50);
            entity.Property(aa => aa.ApprovalPolicy).HasColumnName("approval_policy").HasMaxLength(50);
            entity.Property(aa => aa.RequiredApprovals).HasColumnName("required_approvals");
            entity.Property(aa => aa.CurrentApprovals).HasColumnName("current_approvals");
            entity.Property(aa => aa.Approvers).HasColumnName("approvers").HasColumnType("jsonb");
            entity.Property(aa => aa.ApprovedBy).HasColumnName("approved_by").HasColumnType("jsonb");
            entity.Property(aa => aa.DeniedBy).HasColumnName("denied_by").HasMaxLength(100);
            entity.Property(aa => aa.DenialReason).HasColumnName("denial_reason");
            entity.Property(aa => aa.RequestedAt).HasColumnName("requested_at");
            entity.Property(aa => aa.ExpiresAt).HasColumnName("expires_at");
            entity.Property(aa => aa.ApprovedAt).HasColumnName("approved_at");
            entity.Property(aa => aa.DeniedAt).HasColumnName("denied_at");
            entity.Property(aa => aa.Metadata).HasColumnName("metadata").HasColumnType("jsonb");

            entity.HasOne(aa => aa.Requester)
                .WithMany()
                .HasForeignKey(aa => aa.RequesterId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(aa => aa.RequesterId).HasDatabaseName("idx_access_approvals_requester_id");
            entity.HasIndex(aa => aa.Status).HasDatabaseName("idx_access_approvals_status");
            entity.HasIndex(aa => aa.ResourceType).HasDatabaseName("idx_access_approvals_resource_type");
            entity.HasIndex(aa => aa.ExpiresAt).HasDatabaseName("idx_access_approvals_expires_at");
        });

        // Configure PrivilegedSession
        builder.Entity<PrivilegedSession>(entity =>
        {
            entity.ToTable("privileged_sessions");
            entity.HasKey(ps => ps.Id);

            entity.Property(ps => ps.Id).HasColumnName("id");
            entity.Property(ps => ps.AccountCheckoutId).HasColumnName("account_checkout_id");
            entity.Property(ps => ps.AccountId).HasColumnName("account_id");
            entity.Property(ps => ps.UserId).HasColumnName("user_id");
            entity.Property(ps => ps.StartTime).HasColumnName("start_time");
            entity.Property(ps => ps.EndTime).HasColumnName("end_time");
            entity.Property(ps => ps.Protocol).HasColumnName("protocol").HasMaxLength(50);
            entity.Property(ps => ps.Platform).HasColumnName("platform").HasMaxLength(100);
            entity.Property(ps => ps.HostAddress).HasColumnName("host_address").HasMaxLength(500);
            entity.Property(ps => ps.Port).HasColumnName("port");
            entity.Property(ps => ps.RecordingPath).HasColumnName("recording_path").HasMaxLength(1000);
            entity.Property(ps => ps.RecordingSize).HasColumnName("recording_size");
            entity.Property(ps => ps.SessionType).HasColumnName("session_type").HasMaxLength(50);
            entity.Property(ps => ps.CommandCount).HasColumnName("command_count");
            entity.Property(ps => ps.QueryCount).HasColumnName("query_count");
            entity.Property(ps => ps.SuspiciousActivityDetected).HasColumnName("suspicious_activity_detected");
            entity.Property(ps => ps.SuspiciousActivityDetails).HasColumnName("suspicious_activity_details");
            entity.Property(ps => ps.Status).HasColumnName("status").HasMaxLength(50);
            entity.Property(ps => ps.IpAddress).HasColumnName("ip_address");
            entity.Property(ps => ps.UserAgent).HasColumnName("user_agent");
            entity.Property(ps => ps.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
            entity.Property(ps => ps.CreatedAt).HasColumnName("created_at");

            entity.HasOne(ps => ps.Checkout)
                .WithMany()
                .HasForeignKey(ps => ps.AccountCheckoutId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ps => ps.Account)
                .WithMany()
                .HasForeignKey(ps => ps.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ps => ps.User)
                .WithMany()
                .HasForeignKey(ps => ps.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(ps => ps.AccountCheckoutId).HasDatabaseName("idx_privileged_sessions_checkout_id");
            entity.HasIndex(ps => ps.AccountId).HasDatabaseName("idx_privileged_sessions_account_id");
            entity.HasIndex(ps => ps.UserId).HasDatabaseName("idx_privileged_sessions_user_id");
            entity.HasIndex(ps => ps.Status).HasDatabaseName("idx_privileged_sessions_status");
            entity.HasIndex(ps => ps.StartTime).HasDatabaseName("idx_privileged_sessions_start_time");
            entity.HasIndex(ps => ps.SuspiciousActivityDetected).HasDatabaseName("idx_privileged_sessions_suspicious");
        });

        // Configure SessionCommand
        builder.Entity<SessionCommand>(entity =>
        {
            entity.ToTable("session_commands");
            entity.HasKey(sc => sc.Id);

            entity.Property(sc => sc.Id).HasColumnName("id");
            entity.Property(sc => sc.SessionId).HasColumnName("session_id");
            entity.Property(sc => sc.ExecutedAt).HasColumnName("executed_at");
            entity.Property(sc => sc.CommandType).HasColumnName("command_type").HasMaxLength(50);
            entity.Property(sc => sc.Command).HasColumnName("command");
            entity.Property(sc => sc.Response).HasColumnName("response");
            entity.Property(sc => sc.ResponseSize).HasColumnName("response_size");
            entity.Property(sc => sc.Success).HasColumnName("success");
            entity.Property(sc => sc.ErrorMessage).HasColumnName("error_message");
            entity.Property(sc => sc.ExecutionTimeMs).HasColumnName("execution_time_ms");
            entity.Property(sc => sc.IsSuspicious).HasColumnName("is_suspicious");
            entity.Property(sc => sc.SuspiciousReason).HasColumnName("suspicious_reason");
            entity.Property(sc => sc.SequenceNumber).HasColumnName("sequence_number");

            entity.HasOne(sc => sc.Session)
                .WithMany(ps => ps.Commands)
                .HasForeignKey(sc => sc.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(sc => sc.SessionId).HasDatabaseName("idx_session_commands_session_id");
            entity.HasIndex(sc => sc.ExecutedAt).HasDatabaseName("idx_session_commands_executed_at");
            entity.HasIndex(sc => sc.IsSuspicious).HasDatabaseName("idx_session_commands_suspicious");
            entity.HasIndex(sc => sc.SequenceNumber).HasDatabaseName("idx_session_commands_sequence");
        });

        // Configure JitAccess
        builder.Entity<JitAccess>(entity =>
        {
            entity.ToTable("jit_accesses");
            entity.HasKey(j => j.Id);

            entity.Property(j => j.Id).HasColumnName("id");
            entity.Property(j => j.UserId).HasColumnName("user_id");
            entity.Property(j => j.ResourceType).HasColumnName("resource_type").HasMaxLength(100);
            entity.Property(j => j.ResourceId).HasColumnName("resource_id");
            entity.Property(j => j.ResourceName).HasColumnName("resource_name").HasMaxLength(500);
            entity.Property(j => j.AccessLevel).HasColumnName("access_level").HasMaxLength(50);
            entity.Property(j => j.Justification).HasColumnName("justification");
            entity.Property(j => j.TemplateId).HasColumnName("template_id");
            entity.Property(j => j.ApprovalId).HasColumnName("approval_id");
            entity.Property(j => j.RequestedAt).HasColumnName("requested_at");
            entity.Property(j => j.GrantedAt).HasColumnName("granted_at");
            entity.Property(j => j.ExpiresAt).HasColumnName("expires_at");
            entity.Property(j => j.RevokedAt).HasColumnName("revoked_at");
            entity.Property(j => j.RevokedBy).HasColumnName("revoked_by");
            entity.Property(j => j.RevocationReason).HasColumnName("revocation_reason");
            entity.Property(j => j.Status).HasColumnName("status").HasMaxLength(50);
            entity.Property(j => j.DurationMinutes).HasColumnName("duration_minutes");
            entity.Property(j => j.AutoProvisioningCompleted).HasColumnName("auto_provisioning_completed");
            entity.Property(j => j.AutoDeprovisioningCompleted).HasColumnName("auto_deprovisioning_completed");
            entity.Property(j => j.ProvisioningDetails).HasColumnName("provisioning_details");
            entity.Property(j => j.DeprovisioningDetails).HasColumnName("deprovisioning_details");
            entity.Property(j => j.IpAddress).HasColumnName("ip_address");
            entity.Property(j => j.UserAgent).HasColumnName("user_agent");
            entity.Property(j => j.Metadata).HasColumnName("metadata").HasColumnType("jsonb");

            entity.HasOne(j => j.User)
                .WithMany()
                .HasForeignKey(j => j.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(j => j.Template)
                .WithMany(t => t.AccessGrants)
                .HasForeignKey(j => j.TemplateId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(j => j.Approval)
                .WithMany()
                .HasForeignKey(j => j.ApprovalId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(j => j.RevokedByUser)
                .WithMany()
                .HasForeignKey(j => j.RevokedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(j => j.UserId).HasDatabaseName("idx_jit_accesses_user_id");
            entity.HasIndex(j => j.ResourceType).HasDatabaseName("idx_jit_accesses_resource_type");
            entity.HasIndex(j => j.ResourceId).HasDatabaseName("idx_jit_accesses_resource_id");
            entity.HasIndex(j => j.Status).HasDatabaseName("idx_jit_accesses_status");
            entity.HasIndex(j => j.RequestedAt).HasDatabaseName("idx_jit_accesses_requested_at");
            entity.HasIndex(j => j.ExpiresAt).HasDatabaseName("idx_jit_accesses_expires_at");
        });

        // Configure JitAccessTemplate
        builder.Entity<JitAccessTemplate>(entity =>
        {
            entity.ToTable("jit_access_templates");
            entity.HasKey(t => t.Id);

            entity.Property(t => t.Id).HasColumnName("id");
            entity.Property(t => t.Name).HasColumnName("name").HasMaxLength(255);
            entity.Property(t => t.Description).HasColumnName("description");
            entity.Property(t => t.ResourceType).HasColumnName("resource_type").HasMaxLength(100);
            entity.Property(t => t.ResourceId).HasColumnName("resource_id");
            entity.Property(t => t.AccessLevel).HasColumnName("access_level").HasMaxLength(50);
            entity.Property(t => t.DefaultDurationMinutes).HasColumnName("default_duration_minutes");
            entity.Property(t => t.MaxDurationMinutes).HasColumnName("max_duration_minutes");
            entity.Property(t => t.MinDurationMinutes).HasColumnName("min_duration_minutes");
            entity.Property(t => t.RequiresApproval).HasColumnName("requires_approval");
            entity.Property(t => t.ApprovalPolicy).HasColumnName("approval_policy").HasMaxLength(50);
            entity.Property(t => t.Approvers).HasColumnName("approvers").HasColumnType("jsonb");
            entity.Property(t => t.RequiresJustification).HasColumnName("requires_justification");
            entity.Property(t => t.AllowedRoles).HasColumnName("allowed_roles").HasColumnType("jsonb");
            entity.Property(t => t.Active).HasColumnName("active");
            entity.Property(t => t.UsageCount).HasColumnName("usage_count");
            entity.Property(t => t.LastUsed).HasColumnName("last_used");
            entity.Property(t => t.CreatedAt).HasColumnName("created_at");
            entity.Property(t => t.UpdatedAt).HasColumnName("updated_at");
            entity.Property(t => t.Metadata).HasColumnName("metadata").HasColumnType("jsonb");

            entity.HasIndex(t => t.Name).HasDatabaseName("idx_jit_access_templates_name");
            entity.HasIndex(t => t.ResourceType).HasDatabaseName("idx_jit_access_templates_resource_type");
            entity.HasIndex(t => t.Active).HasDatabaseName("idx_jit_access_templates_active");
        });

        // Configure BreakGlassAccess
        builder.Entity<BreakGlassAccess>(entity =>
        {
            entity.ToTable("break_glass_accesses");
            entity.HasKey(bg => bg.Id);

            entity.Property(bg => bg.Id).HasColumnName("id");
            entity.Property(bg => bg.UserId).HasColumnName("user_id");
            entity.Property(bg => bg.Reason).HasColumnName("reason");
            entity.Property(bg => bg.IncidentType).HasColumnName("incident_type").HasMaxLength(100);
            entity.Property(bg => bg.Severity).HasColumnName("severity").HasMaxLength(50);
            entity.Property(bg => bg.ActivatedAt).HasColumnName("activated_at");
            entity.Property(bg => bg.DeactivatedAt).HasColumnName("deactivated_at");
            entity.Property(bg => bg.ExpiresAt).HasColumnName("expires_at");
            entity.Property(bg => bg.Status).HasColumnName("status").HasMaxLength(50);
            entity.Property(bg => bg.DurationMinutes).HasColumnName("duration_minutes");
            entity.Property(bg => bg.SessionRecordingMandatory).HasColumnName("session_recording_mandatory");
            entity.Property(bg => bg.SessionId).HasColumnName("session_id");
            entity.Property(bg => bg.AccessedResources).HasColumnName("accessed_resources");
            entity.Property(bg => bg.ActionsPerformed).HasColumnName("actions_performed");
            entity.Property(bg => bg.ExecutiveNotified).HasColumnName("executive_notified");
            entity.Property(bg => bg.ExecutiveNotifiedAt).HasColumnName("executive_notified_at");
            entity.Property(bg => bg.NotifiedExecutives).HasColumnName("notified_executives");
            entity.Property(bg => bg.RequiresReview).HasColumnName("requires_review");
            entity.Property(bg => bg.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(bg => bg.ReviewedAt).HasColumnName("reviewed_at");
            entity.Property(bg => bg.ReviewNotes).HasColumnName("review_notes");
            entity.Property(bg => bg.ReviewDecision).HasColumnName("review_decision").HasMaxLength(100);
            entity.Property(bg => bg.IpAddress).HasColumnName("ip_address");
            entity.Property(bg => bg.UserAgent).HasColumnName("user_agent");
            entity.Property(bg => bg.Location).HasColumnName("location").HasMaxLength(500);
            entity.Property(bg => bg.DeviceFingerprint).HasColumnName("device_fingerprint").HasMaxLength(500);
            entity.Property(bg => bg.Metadata).HasColumnName("metadata").HasColumnType("jsonb");

            entity.HasOne(bg => bg.User)
                .WithMany()
                .HasForeignKey(bg => bg.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(bg => bg.Reviewer)
                .WithMany()
                .HasForeignKey(bg => bg.ReviewedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(bg => bg.Session)
                .WithMany()
                .HasForeignKey(bg => bg.SessionId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(bg => bg.UserId).HasDatabaseName("idx_break_glass_accesses_user_id");
            entity.HasIndex(bg => bg.Status).HasDatabaseName("idx_break_glass_accesses_status");
            entity.HasIndex(bg => bg.IncidentType).HasDatabaseName("idx_break_glass_accesses_incident_type");
            entity.HasIndex(bg => bg.ActivatedAt).HasDatabaseName("idx_break_glass_accesses_activated_at");
            entity.HasIndex(bg => bg.ExpiresAt).HasDatabaseName("idx_break_glass_accesses_expires_at");
            entity.HasIndex(bg => bg.RequiresReview).HasDatabaseName("idx_break_glass_accesses_requires_review");
        });

        // Configure BreakGlassPolicy
        builder.Entity<BreakGlassPolicy>(entity =>
        {
            entity.ToTable("break_glass_policies");
            entity.HasKey(bp => bp.Id);

            entity.Property(bp => bp.Id).HasColumnName("id");
            entity.Property(bp => bp.Name).HasColumnName("name").HasMaxLength(255);
            entity.Property(bp => bp.Description).HasColumnName("description");
            entity.Property(bp => bp.Enabled).HasColumnName("enabled");
            entity.Property(bp => bp.DefaultDurationMinutes).HasColumnName("default_duration_minutes");
            entity.Property(bp => bp.MaxDurationMinutes).HasColumnName("max_duration_minutes");
            entity.Property(bp => bp.RequireJustification).HasColumnName("require_justification");
            entity.Property(bp => bp.MinJustificationLength).HasColumnName("min_justification_length");
            entity.Property(bp => bp.AutoNotifyExecutives).HasColumnName("auto_notify_executives");
            entity.Property(bp => bp.ExecutiveUserIds).HasColumnName("executive_user_ids");
            entity.Property(bp => bp.MandatorySessionRecording).HasColumnName("mandatory_session_recording");
            entity.Property(bp => bp.RequirePostAccessReview).HasColumnName("require_post_access_review");
            entity.Property(bp => bp.ReviewRequiredWithinHours).HasColumnName("review_required_within_hours");
            entity.Property(bp => bp.AllowedIncidentTypes).HasColumnName("allowed_incident_types");
            entity.Property(bp => bp.RestrictedToRoles).HasColumnName("restricted_to_roles");
            entity.Property(bp => bp.NotificationChannels).HasColumnName("notification_channels");
            entity.Property(bp => bp.CreatedAt).HasColumnName("created_at");
            entity.Property(bp => bp.UpdatedAt).HasColumnName("updated_at");
            entity.Property(bp => bp.Metadata).HasColumnName("metadata").HasColumnType("jsonb");

            entity.HasIndex(bp => bp.Name).HasDatabaseName("idx_break_glass_policies_name");
            entity.HasIndex(bp => bp.Enabled).HasDatabaseName("idx_break_glass_policies_enabled");
        });

        // Ignore Identity tables we don't need
        builder.Ignore<IdentityUserClaim<Guid>>();
        builder.Ignore<IdentityUserLogin<Guid>>();
        builder.Ignore<IdentityRoleClaim<Guid>>();
        builder.Ignore<IdentityUserToken<Guid>>();
    }

    /// <summary>
    /// Apply global query filters for multi-tenancy
    /// CRITICAL: Ensures all queries are automatically scoped to the current workspace
    /// </summary>
    private void ApplyTenantQueryFilters(ModelBuilder builder)
    {
        // Get all entity types that implement ITenantEntity
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                // Use reflection to call the generic SetTenantFilter method
                var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(SetTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.MakeGenericMethod(entityType.ClrType);

                method?.Invoke(this, new object[] { builder });
            }
        }
    }

    /// <summary>
    /// Set tenant filter for a specific entity type
    /// </summary>
    private void SetTenantFilter<TEntity>(ModelBuilder builder) where TEntity : class, ITenantEntity
    {
        builder.Entity<TEntity>().HasQueryFilter(e =>
            e.WorkspaceId == GetCurrentWorkspaceId());
    }

    /// <summary>
    /// Get the current workspace ID from HttpContext
    /// Returns Guid.Empty if no workspace is set (which will match nothing)
    /// </summary>
    private Guid GetCurrentWorkspaceId()
    {
        if (_httpContextAccessor?.HttpContext == null)
        {
            // No HTTP context available (e.g., background job, migration)
            // Return Guid.Empty which will match nothing
            return Guid.Empty;
        }

        var workspaceId = _httpContextAccessor.HttpContext.Items["WorkspaceId"] as Guid?;
        return workspaceId ?? Guid.Empty;
    }
}

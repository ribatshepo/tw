using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using USP.Core.Domain.Entities.Audit;
using USP.Core.Domain.Entities.Identity;
using USP.Core.Domain.Entities.Integration;
using USP.Core.Domain.Entities.PAM;
using USP.Core.Domain.Entities.Secrets;
using USP.Core.Domain.Entities.Security;
using USP.Core.Domain.Entities.Vault;

namespace USP.Infrastructure.Persistence;

/// <summary>
/// Application database context with EF Core Identity integration
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Identity entities (inherited from IdentityDbContext)
    // - Users (ApplicationUser)
    // - Roles (ApplicationRole)
    // - UserRoles, UserClaims, UserLogins, UserTokens, RoleClaims

    public DbSet<MFADevice> MFADevices => Set<MFADevice>();
    public DbSet<TrustedDevice> TrustedDevices => Set<TrustedDevice>();
    public DbSet<Session> Sessions => Set<Session>();

    // Security entities
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<AccessPolicy> AccessPolicies => Set<AccessPolicy>();

    // Secrets entities
    public DbSet<Secret> Secrets => Set<Secret>();
    public DbSet<SecretVersion> SecretVersions => Set<SecretVersion>();
    public DbSet<EncryptionKey> EncryptionKeys => Set<EncryptionKey>();
    public DbSet<Certificate> Certificates => Set<Certificate>();

    // Vault entities
    public DbSet<SealConfiguration> SealConfigurations => Set<SealConfiguration>();

    // PAM entities
    public DbSet<Safe> Safes => Set<Safe>();
    public DbSet<PrivilegedAccount> PrivilegedAccounts => Set<PrivilegedAccount>();
    public DbSet<Checkout> Checkouts => Set<Checkout>();

    // Audit entities
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RotationJob> RotationJobs => Set<RotationJob>();
    public DbSet<RotationPolicy> RotationPolicies => Set<RotationPolicy>();

    // Integration entities
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<Webhook> Webhooks => Set<Webhook>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply all entity configurations from the assembly
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Global query filters for soft delete
        builder.Entity<ApplicationUser>().HasQueryFilter(u => u.DeletedAt == null);
        builder.Entity<ApplicationRole>().HasQueryFilter(r => r.DeletedAt == null);
        builder.Entity<MFADevice>().HasQueryFilter(d => d.DeletedAt == null);
        builder.Entity<TrustedDevice>().HasQueryFilter(d => d.DeletedAt == null);
        builder.Entity<Policy>().HasQueryFilter(p => p.DeletedAt == null);
        builder.Entity<Permission>().HasQueryFilter(p => p.DeletedAt == null);
        builder.Entity<AccessPolicy>().HasQueryFilter(p => p.DeletedAt == null);
        builder.Entity<EncryptionKey>().HasQueryFilter(k => k.DeletedAt == null);
        builder.Entity<Certificate>().HasQueryFilter(c => c.DeletedAt == null);
        builder.Entity<Safe>().HasQueryFilter(s => s.DeletedAt == null);
        builder.Entity<PrivilegedAccount>().HasQueryFilter(a => a.DeletedAt == null);
        builder.Entity<RotationPolicy>().HasQueryFilter(p => p.DeletedAt == null);
        builder.Entity<ApiKey>().HasQueryFilter(k => k.DeletedAt == null);
        builder.Entity<Webhook>().HasQueryFilter(w => w.DeletedAt == null);
        builder.Entity<Workspace>().HasQueryFilter(w => w.DeletedAt == null);
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Property("CreatedAt").CurrentValue == null)
                {
                    entry.Property("CreatedAt").CurrentValue = DateTime.UtcNow;
                }
            }

            if (entry.Property("UpdatedAt") != null)
            {
                entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;
            }
        }
    }
}

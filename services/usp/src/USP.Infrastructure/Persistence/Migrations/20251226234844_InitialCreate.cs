using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace USP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "access_policies",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Effect = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "allow"),
                    Subjects = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    Resources = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    Actions = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    Conditions = table.Column<string>(type: "jsonb", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access_policies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "api_keys",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    UserId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    KeyPrefix = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Scopes = table.Column<string>(type: "jsonb", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    RateLimitPerMinute = table.Column<int>(type: "integer", nullable: true),
                    AllowedIps = table.Column<string>(type: "jsonb", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UsageCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_keys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UserName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Resource = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    Details = table.Column<string>(type: "jsonb", nullable: true),
                    EncryptedData = table.Column<string>(type: "text", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "certificates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SerialNumber = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Issuer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CertificateData = table.Column<string>(type: "text", nullable: false),
                    PrivateKeyData = table.Column<string>(type: "text", nullable: true),
                    NotBefore = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NotAfter = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevocationReason = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certificates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "encryption_keys",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Algorithm = table.Column<string>(type: "text", nullable: false),
                    CurrentVersion = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    MinDecryptionVersion = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    AllowPlaintextBackup = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Exportable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletionAllowed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ConvergentEncryption = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_encryption_keys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Resource = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsSystemPermission = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "policies",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: true),
                    Effect = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "allow"),
                    Priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsSystemPolicy = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_policies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsSystemRole = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "rotation_jobs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TargetResource = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TargetCredentialId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PolicyId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Duration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rotation_jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "rotation_policies",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Type = table.Column<string>(type: "text", nullable: false),
                    IntervalDays = table.Column<int>(type: "integer", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CronSchedule = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Configuration = table.Column<string>(type: "jsonb", nullable: true),
                    LastExecutedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextExecutionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rotation_policies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "safes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_safes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "secrets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    CurrentVersion = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    MaxVersions = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                    CasRequired = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_secrets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LastName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    MfaEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    MfaSecret = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    FailedLoginAttempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastFailedLogin = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PasswordChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RiskScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m),
                    RiskScoreUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RequireReauthentication = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastLoginIp = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    LastLoginLocation = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    MaxConcurrentSessions = table.Column<int>(type: "integer", nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "webhooks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    UserId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Events = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    SecretKey = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    MaxRetries = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    TimeoutSeconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhooks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "workspaces",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Settings = table.Column<string>(type: "jsonb", nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    PermissionsId = table.Column<string>(type: "character varying(255)", nullable: false),
                    RolesId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => new { x.PermissionsId, x.RolesId });
                    table.ForeignKey(
                        name: "FK_role_permissions_permissions_PermissionsId",
                        column: x => x.PermissionsId,
                        principalTable: "permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_permissions_roles_RolesId",
                        column: x => x.RolesId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "privileged_accounts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SafeId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AccountName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Platform = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EncryptedPassword = table.Column<string>(type: "text", nullable: true),
                    EncryptedSSHKey = table.Column<string>(type: "text", nullable: true),
                    AutoRotationEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    RotationIntervalDays = table.Column<int>(type: "integer", nullable: true),
                    LastRotatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextRotationAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_privileged_accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_privileged_accounts_safes_SafeId",
                        column: x => x.SafeId,
                        principalTable: "safes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "secret_versions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SecretId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    EncryptedData = table.Column<string>(type: "text", nullable: false),
                    EncryptionKeyId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsDestroyed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TimeToLive = table.Column<TimeSpan>(type: "interval", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DestroyedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_secret_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_secret_versions_secrets_SecretId",
                        column: x => x.SecretId,
                        principalTable: "secrets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    RoleId = table.Column<string>(type: "text", nullable: false),
                    ApplicationRoleId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_roles_ApplicationRoleId",
                        column: x => x.ApplicationRoleId,
                        principalTable: "roles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mfa_devices",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    UserId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Method = table.Column<string>(type: "text", nullable: false),
                    DeviceName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    DeviceData = table.Column<string>(type: "text", nullable: true),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    UsageCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EnrolledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mfa_devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mfa_devices_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    UserId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AccessToken = table.Column<string>(type: "text", nullable: false),
                    RefreshToken = table.Column<string>(type: "text", nullable: false),
                    AccessTokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RefreshTokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DeviceFingerprint = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Location = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevocationReason = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sessions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trusted_devices",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    UserId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    DeviceFingerprint = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    DeviceName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    DeviceType = table.Column<string>(type: "text", nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    Location = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    UsageCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TrustedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trusted_devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trusted_devices_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "webhook_deliveries",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    WebhookId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ResponseStatusCode = table.Column<int>(type: "integer", nullable: true),
                    ResponseBody = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    NextRetryAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_deliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_webhook_deliveries_webhooks_WebhookId",
                        column: x => x.WebhookId,
                        principalTable: "webhooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "checkouts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AccountId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    UserId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CheckedOutAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CheckedInAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PasswordRotatedOnCheckin = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_checkouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_checkouts_privileged_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "privileged_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_access_policies_CreatedAt",
                table: "access_policies",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_access_policies_DeletedAt",
                table: "access_policies",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_access_policies_IsActive",
                table: "access_policies",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_access_policies_Name",
                table: "access_policies",
                column: "Name",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_access_policies_Priority",
                table: "access_policies",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_CreatedAt",
                table: "api_keys",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_DeletedAt",
                table: "api_keys",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_ExpiresAt",
                table: "api_keys",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_IsActive",
                table: "api_keys",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_KeyHash",
                table: "api_keys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_KeyPrefix",
                table: "api_keys",
                column: "KeyPrefix");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_UserId",
                table: "api_keys",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_UserId_IsActive",
                table: "api_keys",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_ApplicationRoleId",
                table: "AspNetUserRoles",
                column: "ApplicationRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_Action",
                table: "audit_logs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_CorrelationId",
                table: "audit_logs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_EventType",
                table: "audit_logs",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_Resource",
                table: "audit_logs",
                column: "Resource");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_Success",
                table: "audit_logs",
                column: "Success");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_Timestamp",
                table: "audit_logs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_UserId",
                table: "audit_logs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_UserId_EventType_Timestamp",
                table: "audit_logs",
                columns: new[] { "UserId", "EventType", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_certificates_CreatedAt",
                table: "certificates",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_certificates_DeletedAt",
                table: "certificates",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_certificates_IsRevoked",
                table: "certificates",
                column: "IsRevoked");

            migrationBuilder.CreateIndex(
                name: "IX_certificates_NotAfter",
                table: "certificates",
                column: "NotAfter");

            migrationBuilder.CreateIndex(
                name: "IX_certificates_SerialNumber",
                table: "certificates",
                column: "SerialNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_certificates_Subject",
                table: "certificates",
                column: "Subject");

            migrationBuilder.CreateIndex(
                name: "IX_checkouts_AccountId",
                table: "checkouts",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_checkouts_AccountId_Status",
                table: "checkouts",
                columns: new[] { "AccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_checkouts_CreatedAt",
                table: "checkouts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_checkouts_ExpiresAt",
                table: "checkouts",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_checkouts_RequestedAt",
                table: "checkouts",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_checkouts_Status",
                table: "checkouts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_checkouts_UserId",
                table: "checkouts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_encryption_keys_Algorithm",
                table: "encryption_keys",
                column: "Algorithm");

            migrationBuilder.CreateIndex(
                name: "IX_encryption_keys_CreatedAt",
                table: "encryption_keys",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_encryption_keys_DeletedAt",
                table: "encryption_keys",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_encryption_keys_Name",
                table: "encryption_keys",
                column: "Name",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_mfa_devices_CreatedAt",
                table: "mfa_devices",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_mfa_devices_DeletedAt",
                table: "mfa_devices",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_mfa_devices_Method",
                table: "mfa_devices",
                column: "Method");

            migrationBuilder.CreateIndex(
                name: "IX_mfa_devices_UserId",
                table: "mfa_devices",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_mfa_devices_UserId_IsPrimary",
                table: "mfa_devices",
                columns: new[] { "UserId", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "IX_permissions_CreatedAt",
                table: "permissions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_permissions_DeletedAt",
                table: "permissions",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_permissions_IsSystemPermission",
                table: "permissions",
                column: "IsSystemPermission");

            migrationBuilder.CreateIndex(
                name: "IX_permissions_Resource",
                table: "permissions",
                column: "Resource");

            migrationBuilder.CreateIndex(
                name: "IX_permissions_Resource_Action",
                table: "permissions",
                columns: new[] { "Resource", "Action" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_policies_CreatedAt",
                table: "policies",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_policies_DeletedAt",
                table: "policies",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_policies_IsActive",
                table: "policies",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_policies_Name",
                table: "policies",
                column: "Name",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_policies_Priority",
                table: "policies",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_policies_Type",
                table: "policies",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_privileged_accounts_AutoRotationEnabled",
                table: "privileged_accounts",
                column: "AutoRotationEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_privileged_accounts_CreatedAt",
                table: "privileged_accounts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_privileged_accounts_DeletedAt",
                table: "privileged_accounts",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_privileged_accounts_NextRotationAt",
                table: "privileged_accounts",
                column: "NextRotationAt");

            migrationBuilder.CreateIndex(
                name: "IX_privileged_accounts_Platform",
                table: "privileged_accounts",
                column: "Platform");

            migrationBuilder.CreateIndex(
                name: "IX_privileged_accounts_SafeId",
                table: "privileged_accounts",
                column: "SafeId");

            migrationBuilder.CreateIndex(
                name: "IX_privileged_accounts_SafeId_AccountName_Platform",
                table: "privileged_accounts",
                columns: new[] { "SafeId", "AccountName", "Platform" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_RolesId",
                table: "role_permissions",
                column: "RolesId");

            migrationBuilder.CreateIndex(
                name: "IX_roles_CreatedAt",
                table: "roles",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_roles_DeletedAt",
                table: "roles",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_roles_IsSystemRole",
                table: "roles",
                column: "IsSystemRole");

            migrationBuilder.CreateIndex(
                name: "IX_roles_Name",
                table: "roles",
                column: "Name",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_roles_Priority",
                table: "roles",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "roles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rotation_jobs_CreatedAt",
                table: "rotation_jobs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_rotation_jobs_PolicyId",
                table: "rotation_jobs",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_rotation_jobs_ScheduledAt",
                table: "rotation_jobs",
                column: "ScheduledAt");

            migrationBuilder.CreateIndex(
                name: "IX_rotation_jobs_Status",
                table: "rotation_jobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_rotation_jobs_Status_ScheduledAt",
                table: "rotation_jobs",
                columns: new[] { "Status", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_rotation_jobs_TargetResource",
                table: "rotation_jobs",
                column: "TargetResource");

            migrationBuilder.CreateIndex(
                name: "IX_rotation_jobs_Type",
                table: "rotation_jobs",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_rotation_policies_CreatedAt",
                table: "rotation_policies",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_rotation_policies_DeletedAt",
                table: "rotation_policies",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_rotation_policies_Enabled_NextExecutionAt",
                table: "rotation_policies",
                columns: new[] { "Enabled", "NextExecutionAt" });

            migrationBuilder.CreateIndex(
                name: "IX_rotation_policies_Name",
                table: "rotation_policies",
                column: "Name",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_rotation_policies_NextExecutionAt",
                table: "rotation_policies",
                column: "NextExecutionAt");

            migrationBuilder.CreateIndex(
                name: "IX_rotation_policies_Type",
                table: "rotation_policies",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_safes_CreatedAt",
                table: "safes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_safes_DeletedAt",
                table: "safes",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_safes_Name",
                table: "safes",
                column: "Name",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_secret_versions_CreatedAt",
                table: "secret_versions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_secret_versions_ExpiresAt",
                table: "secret_versions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_secret_versions_IsDeleted",
                table: "secret_versions",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_secret_versions_IsDestroyed",
                table: "secret_versions",
                column: "IsDestroyed");

            migrationBuilder.CreateIndex(
                name: "IX_secret_versions_SecretId",
                table: "secret_versions",
                column: "SecretId");

            migrationBuilder.CreateIndex(
                name: "IX_secret_versions_SecretId_Version",
                table: "secret_versions",
                columns: new[] { "SecretId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_secrets_CreatedAt",
                table: "secrets",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_secrets_DeletedAt",
                table: "secrets",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_secrets_IsDeleted",
                table: "secrets",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_secrets_Path",
                table: "secrets",
                column: "Path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_secrets_Type",
                table: "secrets",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_CreatedAt",
                table: "sessions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_ExpiresAt",
                table: "sessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_IsActive",
                table: "sessions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_LastActivityAt",
                table: "sessions",
                column: "LastActivityAt");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_RevokedAt",
                table: "sessions",
                column: "RevokedAt");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_UserId",
                table: "sessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_UserId_IsActive",
                table: "sessions",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_trusted_devices_CreatedAt",
                table: "trusted_devices",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_trusted_devices_DeletedAt",
                table: "trusted_devices",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_trusted_devices_DeviceFingerprint",
                table: "trusted_devices",
                column: "DeviceFingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_trusted_devices_ExpiresAt",
                table: "trusted_devices",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_trusted_devices_IsActive",
                table: "trusted_devices",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_trusted_devices_UserId",
                table: "trusted_devices",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_trusted_devices_UserId_DeviceFingerprint",
                table: "trusted_devices",
                columns: new[] { "UserId", "DeviceFingerprint" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "users",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_users_CreatedAt",
                table: "users",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_users_DeletedAt",
                table: "users",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_users_RiskScore",
                table: "users",
                column: "RiskScore");

            migrationBuilder.CreateIndex(
                name: "IX_users_Status",
                table: "users",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_users_UserName",
                table: "users",
                column: "UserName",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "users",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_CreatedAt",
                table: "webhook_deliveries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_EventType",
                table: "webhook_deliveries",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_Status",
                table: "webhook_deliveries",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_Status_NextRetryAt",
                table: "webhook_deliveries",
                columns: new[] { "Status", "NextRetryAt" });

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_WebhookId",
                table: "webhook_deliveries",
                column: "WebhookId");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_WebhookId_Status",
                table: "webhook_deliveries",
                columns: new[] { "WebhookId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_webhooks_CreatedAt",
                table: "webhooks",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_webhooks_DeletedAt",
                table: "webhooks",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_webhooks_IsActive",
                table: "webhooks",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_webhooks_UserId",
                table: "webhooks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_webhooks_UserId_IsActive",
                table: "webhooks",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_workspaces_CreatedAt",
                table: "workspaces",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_workspaces_DeletedAt",
                table: "workspaces",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_workspaces_IsActive",
                table: "workspaces",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_workspaces_Name",
                table: "workspaces",
                column: "Name",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_workspaces_Slug",
                table: "workspaces",
                column: "Slug",
                unique: true,
                filter: "\"DeletedAt\" IS NULL AND \"Slug\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access_policies");

            migrationBuilder.DropTable(
                name: "api_keys");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "certificates");

            migrationBuilder.DropTable(
                name: "checkouts");

            migrationBuilder.DropTable(
                name: "encryption_keys");

            migrationBuilder.DropTable(
                name: "mfa_devices");

            migrationBuilder.DropTable(
                name: "policies");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "rotation_jobs");

            migrationBuilder.DropTable(
                name: "rotation_policies");

            migrationBuilder.DropTable(
                name: "secret_versions");

            migrationBuilder.DropTable(
                name: "sessions");

            migrationBuilder.DropTable(
                name: "trusted_devices");

            migrationBuilder.DropTable(
                name: "webhook_deliveries");

            migrationBuilder.DropTable(
                name: "workspaces");

            migrationBuilder.DropTable(
                name: "privileged_accounts");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "secrets");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "webhooks");

            migrationBuilder.DropTable(
                name: "safes");
        }
    }
}

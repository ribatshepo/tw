using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace USP.Infrastructure.Migrations
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
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    policy_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    policy = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effect = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    subjects = table.Column<string>(type: "jsonb", nullable: true),
                    resources = table.Column<string>(type: "jsonb", nullable: true),
                    actions = table.Column<string[]>(type: "text[]", nullable: true),
                    conditions = table.Column<string>(type: "jsonb", nullable: true),
                    priority = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access_policies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "AuthorizationFlows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FlowName = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ResourceType = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    RequiredApprovals = table.Column<int>(type: "integer", nullable: false),
                    ApproverRoles = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthorizationFlows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "break_glass_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    default_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    max_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    require_justification = table.Column<bool>(type: "boolean", nullable: false),
                    min_justification_length = table.Column<int>(type: "integer", nullable: false),
                    auto_notify_executives = table.Column<bool>(type: "boolean", nullable: false),
                    executive_user_ids = table.Column<string>(type: "text", nullable: true),
                    mandatory_session_recording = table.Column<bool>(type: "boolean", nullable: false),
                    require_post_access_review = table.Column<bool>(type: "boolean", nullable: false),
                    review_required_within_hours = table.Column<int>(type: "integer", nullable: false),
                    allowed_incident_types = table.Column<string>(type: "text", nullable: true),
                    restricted_to_roles = table.Column<string>(type: "text", nullable: true),
                    notification_channels = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_break_glass_policies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ColumnSecurityRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TableName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ColumnName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Operation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RestrictionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MaskingPattern = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AllowedRoles = table.Column<string[]>(type: "text[]", nullable: true),
                    DeniedRoles = table.Column<string[]>(type: "text[]", nullable: true),
                    Condition = table.Column<string>(type: "text", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ColumnSecurityRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContextPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "*"),
                    EnableTimeRestriction = table.Column<bool>(type: "boolean", nullable: false),
                    AllowedDaysOfWeek = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AllowedStartTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    AllowedEndTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    EnableLocationRestriction = table.Column<bool>(type: "boolean", nullable: false),
                    AllowedCountries = table.Column<string[]>(type: "text[]", nullable: true),
                    DeniedCountries = table.Column<string[]>(type: "text[]", nullable: true),
                    AllowedNetworkZones = table.Column<string[]>(type: "text[]", nullable: true),
                    EnableDeviceRestriction = table.Column<bool>(type: "boolean", nullable: false),
                    RequireCompliantDevice = table.Column<bool>(type: "boolean", nullable: false),
                    AllowedDeviceTypes = table.Column<string[]>(type: "text[]", nullable: true),
                    EnableRiskRestriction = table.Column<bool>(type: "boolean", nullable: false),
                    MaxAllowedRiskScore = table.Column<int>(type: "integer", nullable: true),
                    DenyImpossibleTravel = table.Column<bool>(type: "boolean", nullable: false),
                    RequireMfaOnHighRisk = table.Column<bool>(type: "boolean", nullable: false),
                    RequireApprovalOnHighRisk = table.Column<bool>(type: "boolean", nullable: false),
                    HighRiskThreshold = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContextPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "database_configs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Plugin = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EncryptedConnectionUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    EncryptedUsername = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EncryptedPassword = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MaxOpenConnections = table.Column<int>(type: "integer", nullable: false, defaultValue: 4),
                    MaxIdleConnections = table.Column<int>(type: "integer", nullable: false, defaultValue: 2),
                    MaxConnectionLifetimeSeconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 3600),
                    additional_config = table.Column<string>(type: "jsonb", nullable: true),
                    ConfiguredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConfiguredBy = table.Column<Guid>(type: "uuid", nullable: false),
                    LastRotatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_database_configs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "jit_access_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    resource_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    access_level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    default_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    max_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    min_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    requires_approval = table.Column<bool>(type: "boolean", nullable: false),
                    approval_policy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    approvers = table.Column<string>(type: "jsonb", nullable: true),
                    requires_justification = table.Column<bool>(type: "boolean", nullable: false),
                    allowed_roles = table.Column<string>(type: "jsonb", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    usage_count = table.Column<int>(type: "integer", nullable: false),
                    last_used = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jit_access_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "OAuth2Clients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<string>(type: "text", nullable: false),
                    ClientSecret = table.Column<string>(type: "text", nullable: false),
                    ClientName = table.Column<string>(type: "text", nullable: false),
                    ClientType = table.Column<string>(type: "text", nullable: false),
                    RedirectUris = table.Column<List<string>>(type: "text[]", nullable: false),
                    AllowedScopes = table.Column<List<string>>(type: "text[]", nullable: false),
                    AllowedGrantTypes = table.Column<List<string>>(type: "text[]", nullable: false),
                    RequirePkce = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OAuth2Clients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    resource = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pki_certificate_authorities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    subject_dn = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    certificate_pem = table.Column<string>(type: "text", nullable: false),
                    serial_number = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    encrypted_private_key = table.Column<string>(type: "text", nullable: false),
                    key_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    not_before = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    not_after = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    max_path_length = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    parent_ca_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revoked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    issued_certificate_count = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pki_certificate_authorities", x => x.id);
                    table.ForeignKey(
                        name: "FK_pki_certificate_authorities_pki_certificate_authorities_par~",
                        column: x => x.parent_ca_id,
                        principalTable: "pki_certificate_authorities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_system_role = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "SealConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    SecretThreshold = table.Column<int>(type: "integer", nullable: false),
                    SecretShares = table.Column<int>(type: "integer", nullable: false),
                    EncryptedMasterKey = table.Column<string>(type: "text", nullable: false),
                    Initialized = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUnsealedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SealConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    last_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    mfa_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    mfa_secret = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    VerifiedPhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberVerified = table.Column<bool>(type: "boolean", nullable: false),
                    failed_login_attempts = table.Column<int>(type: "integer", nullable: false),
                    last_failed_login = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    locked_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    password_changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    RiskProfile = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    username = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "database_roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DatabaseConfigId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreationStatements = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    RevocationStatements = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    RenewStatements = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    RollbackStatements = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    DefaultTtlSeconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 3600),
                    MaxTtlSeconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 86400),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_database_roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_database_roles_database_configs_DatabaseConfigId",
                        column: x => x.DatabaseConfigId,
                        principalTable: "database_configs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pki_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    certificate_authority_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "rsa-2048"),
                    ttl_days = table.Column<int>(type: "integer", nullable: false, defaultValue: 365),
                    max_ttl_days = table.Column<int>(type: "integer", nullable: false, defaultValue: 3650),
                    allow_localhost = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    allow_bare_domains = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    allow_subdomains = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    allow_wildcards = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    allow_ip_sans = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    allowed_domains = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    server_auth = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    client_auth = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    code_signing = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    email_protection = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pki_roles", x => x.id);
                    table.ForeignKey(
                        name: "FK_pki_roles_pki_certificate_authorities_certificate_authority~",
                        column: x => x.certificate_authority_id,
                        principalTable: "pki_certificate_authorities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    granted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => new { x.role_id, x.permission_id });
                    table.ForeignKey(
                        name: "FK_role_permissions_permissions_permission_id",
                        column: x => x.permission_id,
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_permissions_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "access_approvals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    approval_policy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    required_approvals = table.Column<int>(type: "integer", nullable: false),
                    current_approvals = table.Column<int>(type: "integer", nullable: false),
                    approvers = table.Column<List<Guid>>(type: "jsonb", nullable: false),
                    approved_by = table.Column<List<Guid>>(type: "jsonb", nullable: false),
                    denied_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    denial_reason = table.Column<string>(type: "text", nullable: true),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    denied_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access_approvals", x => x.id);
                    table.ForeignKey(
                        name: "FK_access_approvals_users_requester_id",
                        column: x => x.requester_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "api_keys",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    key_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    key_prefix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    scopes = table.Column<string[]>(type: "text[]", nullable: false),
                    SigningSecret = table.Column<string>(type: "text", nullable: true),
                    RateLimitPerMinute = table.Column<int>(type: "integer", nullable: true),
                    RateLimitPerHour = table.Column<int>(type: "integer", nullable: true),
                    RateLimitPerDay = table.Column<int>(type: "integer", nullable: true),
                    RequestCount = table.Column<int>(type: "integer", nullable: false),
                    last_used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked = table.Column<bool>(type: "boolean", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_keys", x => x.id);
                    table.ForeignKey(
                        name: "FK_api_keys_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    Username = table.Column<string>(type: "text", nullable: false),
                    action = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Resource = table.Column<string>(type: "text", nullable: false),
                    resource_type = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    resource_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ip_address = table.Column<string>(type: "text", nullable: false),
                    user_agent = table.Column<string>(type: "text", nullable: true),
                    HttpMethod = table.Column<string>(type: "text", nullable: false),
                    RequestPath = table.Column<string>(type: "text", nullable: false),
                    QueryString = table.Column<string>(type: "text", nullable: true),
                    RequestBody = table.Column<string>(type: "text", nullable: true),
                    ResponseStatus = table.Column<int>(type: "integer", nullable: false),
                    ResponseBody = table.Column<string>(type: "text", nullable: true),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    old_value = table.Column<string>(type: "text", nullable: true),
                    new_value = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    previous_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    current_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_logs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuthorizationFlowInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FlowId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceType = table.Column<string>(type: "text", nullable: false),
                    ResourceId = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Context = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthorizationFlowInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuthorizationFlowInstances_AuthorizationFlows_FlowId",
                        column: x => x.FlowId,
                        principalTable: "AuthorizationFlows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuthorizationFlowInstances_users_RequesterId",
                        column: x => x.RequesterId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "business_associate_agreements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    partner_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    partner_contact_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    partner_contact_phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    effective_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expiration_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    document_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    document_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    document_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    last_reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    review_notes = table.Column<string>(type: "text", nullable: true),
                    services_provided = table.Column<string>(type: "text", nullable: true),
                    phi_categories = table.Column<string>(type: "text", nullable: true),
                    requires_annual_review = table.Column<bool>(type: "boolean", nullable: false),
                    notify_days_before_expiration = table.Column<int>(type: "integer", nullable: false),
                    renewal_requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    renewal_completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    compliance_notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_associate_agreements", x => x.id);
                    table.ForeignKey(
                        name: "FK_business_associate_agreements_users_last_reviewed_by",
                        column: x => x.last_reviewed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "compliance_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    framework = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    report_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    period_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    period_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    generated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    report_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    format = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    total_controls = table.Column<int>(type: "integer", nullable: false),
                    implemented_controls = table.Column<int>(type: "integer", nullable: false),
                    partial_controls = table.Column<int>(type: "integer", nullable: false),
                    not_implemented_controls = table.Column<int>(type: "integer", nullable: false),
                    compliance_score = table.Column<double>(type: "double precision", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: true),
                    recommendations = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_reports", x => x.id);
                    table.ForeignKey(
                        name: "FK_compliance_reports_users_generated_by",
                        column: x => x.generated_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ldap_configurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    server_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    port = table.Column<int>(type: "integer", nullable: false),
                    use_ssl = table.Column<bool>(type: "boolean", nullable: false),
                    use_tls = table.Column<bool>(type: "boolean", nullable: false),
                    base_dn = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    bind_dn = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    bind_password = table.Column<string>(type: "text", nullable: false),
                    user_search_filter = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    user_search_base = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    group_search_filter = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    group_search_base = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    email_attribute = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    first_name_attribute = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name_attribute = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    username_attribute = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    group_membership_attribute = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    enable_jit_provisioning = table.Column<bool>(type: "boolean", nullable: false),
                    default_role_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sync_groups_as_roles = table.Column<bool>(type: "boolean", nullable: false),
                    update_user_on_login = table.Column<bool>(type: "boolean", nullable: false),
                    enable_group_sync = table.Column<bool>(type: "boolean", nullable: false),
                    group_sync_interval_minutes = table.Column<int>(type: "integer", nullable: false),
                    last_group_sync = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    nested_groups_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    group_role_mapping = table.Column<string>(type: "jsonb", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_test_result = table.Column<string>(type: "text", nullable: true),
                    last_tested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ldap_configurations", x => x.id);
                    table.ForeignKey(
                        name: "FK_ldap_configurations_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MagicLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Token = table.Column<string>(type: "text", nullable: false),
                    RedirectUrl = table.Column<string>(type: "text", nullable: true),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MagicLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MagicLinks_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MfaBackupCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "text", nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MfaBackupCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MfaBackupCodes_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MfaDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceType = table.Column<string>(type: "text", nullable: false),
                    DeviceName = table.Column<string>(type: "text", nullable: false),
                    DeviceFingerprint = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EnrolledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MfaDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MfaDevices_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OAuth2AuthorizationCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RedirectUri = table.Column<string>(type: "text", nullable: false),
                    Scope = table.Column<string>(type: "text", nullable: false),
                    CodeChallenge = table.Column<string>(type: "text", nullable: true),
                    CodeChallengeMethod = table.Column<string>(type: "text", nullable: true),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OAuth2AuthorizationCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OAuth2AuthorizationCodes_OAuth2Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "OAuth2Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OAuth2AuthorizationCodes_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "privileged_safes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    safe_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    access_control = table.Column<string>(type: "jsonb", nullable: false),
                    require_approval = table.Column<bool>(type: "boolean", nullable: false),
                    require_dual_control = table.Column<bool>(type: "boolean", nullable: false),
                    max_checkout_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    rotate_on_checkin = table.Column<bool>(type: "boolean", nullable: false),
                    session_recording_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_privileged_safes", x => x.id);
                    table.ForeignKey(
                        name: "FK_privileged_safes_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskAssessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IpAddress = table.Column<string>(type: "text", nullable: false),
                    UserAgent = table.Column<string>(type: "text", nullable: false),
                    DeviceFingerprint = table.Column<string>(type: "text", nullable: true),
                    RiskLevel = table.Column<string>(type: "text", nullable: false),
                    RiskScore = table.Column<int>(type: "integer", nullable: false),
                    RiskFactors = table.Column<List<string>>(type: "text[]", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    AssessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskAssessments_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "saml_identity_providers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    sso_service_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    slo_service_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    signing_certificate = table.Column<string>(type: "text", nullable: false),
                    metadata_xml = table.Column<string>(type: "text", nullable: true),
                    sign_authn_requests = table.Column<bool>(type: "boolean", nullable: false),
                    require_signed_assertions = table.Column<bool>(type: "boolean", nullable: false),
                    enable_jit_provisioning = table.Column<bool>(type: "boolean", nullable: false),
                    email_attribute_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    first_name_attribute_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    last_name_attribute_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    groups_attribute_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    role_mapping = table.Column<string>(type: "jsonb", nullable: true),
                    default_role_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saml_identity_providers", x => x.id);
                    table.ForeignKey(
                        name: "FK_saml_identity_providers_roles_default_role_id",
                        column: x => x.default_role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_saml_identity_providers_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "secrets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    encrypted_value = table.Column<byte[]>(type: "bytea", nullable: false),
                    EncryptedData = table.Column<string>(type: "text", nullable: false),
                    encryption_key_version = table.Column<int>(type: "integer", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsDestroyed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_secrets", x => x.id);
                    table.ForeignKey(
                        name: "FK_secrets_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    refresh_token_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ip_address = table.Column<IPAddress>(type: "inet", nullable: true),
                    user_agent = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_activity = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked = table.Column<bool>(type: "boolean", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transit_keys",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    latest_version = table.Column<int>(type: "integer", nullable: false),
                    min_decryption_version = table.Column<int>(type: "integer", nullable: false),
                    min_encryption_version = table.Column<int>(type: "integer", nullable: false),
                    deletion_allowed = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    exportable = table.Column<bool>(type: "boolean", nullable: false),
                    allow_plaintext_backup = table.Column<bool>(type: "boolean", nullable: false),
                    convergent_encryption = table.Column<bool>(type: "boolean", nullable: false),
                    convergent_version = table.Column<int>(type: "integer", nullable: true),
                    derived = table.Column<bool>(type: "boolean", nullable: false),
                    encryption_count = table.Column<long>(type: "bigint", nullable: false),
                    decryption_count = table.Column<long>(type: "bigint", nullable: false),
                    signing_count = table.Column<long>(type: "bigint", nullable: false),
                    verification_count = table.Column<long>(type: "bigint", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transit_keys", x => x.id);
                    table.ForeignKey(
                        name: "FK_transit_keys_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrustedDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceFingerprint = table.Column<string>(type: "text", nullable: false),
                    DeviceId = table.Column<string>(type: "text", nullable: false),
                    DeviceName = table.Column<string>(type: "text", nullable: false),
                    DeviceType = table.Column<string>(type: "text", nullable: false),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    Location = table.Column<string>(type: "text", nullable: true),
                    Country = table.Column<string>(type: "text", nullable: true),
                    CountryCode = table.Column<string>(type: "text", nullable: true),
                    Region = table.Column<string>(type: "text", nullable: true),
                    City = table.Column<string>(type: "text", nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    LastLocationUpdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsTrusted = table.Column<bool>(type: "boolean", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrustedDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrustedDevices_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_clearances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    clearance_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    granted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    granted_by = table.Column<Guid>(type: "uuid", nullable: false),
                    clearance_level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    background_check_details = table.Column<string>(type: "text", nullable: true),
                    background_check_provider = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    background_check_completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    training_completed = table.Column<string>(type: "text", nullable: true),
                    last_reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    review_notes = table.Column<string>(type: "text", nullable: true),
                    documentation_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_clearances", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_clearances_users_granted_by",
                        column: x => x.granted_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_clearances_users_last_reviewed_by",
                        column: x => x.last_reviewed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_clearances_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    granted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    granted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NamespaceId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "FK_user_roles_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_roles_users_granted_by",
                        column: x => x.granted_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_roles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRiskProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    KnownIpAddresses = table.Column<List<string>>(type: "text[]", nullable: false),
                    KnownCountries = table.Column<List<string>>(type: "text[]", nullable: false),
                    KnownCities = table.Column<List<string>>(type: "text[]", nullable: false),
                    KnownDeviceFingerprints = table.Column<List<string>>(type: "text[]", nullable: false),
                    TypicalLoginHours = table.Column<List<int>>(type: "integer[]", nullable: false),
                    BaselineRiskScore = table.Column<int>(type: "integer", nullable: false),
                    CurrentRiskScore = table.Column<int>(type: "integer", nullable: false),
                    RiskTier = table.Column<string>(type: "text", nullable: false),
                    ConsecutiveFailedLogins = table.Column<int>(type: "integer", nullable: false),
                    SuspiciousActivityCount = table.Column<int>(type: "integer", nullable: false),
                    LastSuspiciousActivity = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastPasswordChange = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastMfaEnrollment = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastKnownCountry = table.Column<string>(type: "text", nullable: true),
                    LastKnownCity = table.Column<string>(type: "text", nullable: true),
                    LastKnownLatitude = table.Column<double>(type: "double precision", nullable: true),
                    LastKnownLongitude = table.Column<double>(type: "double precision", nullable: true),
                    LastLocationUpdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LoginAttemptsLast24Hours = table.Column<int>(type: "integer", nullable: false),
                    LoginAttemptsLastHour = table.Column<int>(type: "integer", nullable: false),
                    LastLoginAttempt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TrustScore = table.Column<int>(type: "integer", nullable: false),
                    IsCompromised = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresMandatoryMfa = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRiskProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRiskProfiles_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WebAuthnCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceName = table.Column<string>(type: "text", nullable: false),
                    CredentialId = table.Column<byte[]>(type: "bytea", nullable: false),
                    PublicKey = table.Column<byte[]>(type: "bytea", nullable: false),
                    SignatureCounter = table.Column<long>(type: "bigint", nullable: false),
                    AaGuid = table.Column<string>(type: "text", nullable: false),
                    CredentialType = table.Column<string>(type: "text", nullable: false),
                    Transports = table.Column<List<string>>(type: "text[]", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebAuthnCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebAuthnCredentials_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "webhooks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    events = table.Column<List<string>>(type: "jsonb", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    authentication_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    secret_token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    oauth2_client_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    oauth2_client_secret = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    oauth2_token_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    custom_headers = table.Column<string>(type: "jsonb", nullable: true),
                    payload_template = table.Column<string>(type: "text", nullable: true),
                    max_retries = table.Column<int>(type: "integer", nullable: false),
                    timeout_seconds = table.Column<int>(type: "integer", nullable: false),
                    verify_ssl = table.Column<bool>(type: "boolean", nullable: false),
                    circuit_breaker_state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    consecutive_failures = table.Column<int>(type: "integer", nullable: false),
                    circuit_breaker_opened_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    circuit_breaker_threshold = table.Column<int>(type: "integer", nullable: false),
                    circuit_breaker_reset_minutes = table.Column<int>(type: "integer", nullable: false),
                    total_deliveries = table.Column<int>(type: "integer", nullable: false),
                    successful_deliveries = table.Column<int>(type: "integer", nullable: false),
                    failed_deliveries = table.Column<int>(type: "integer", nullable: false),
                    last_triggered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_success_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_failure_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhooks", x => x.id);
                    table.ForeignKey(
                        name: "FK_webhooks_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workspaces",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    slug = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_workspace_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    settings = table.Column<string>(type: "jsonb", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    subscription_tier = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    custom_domain = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    require_mfa = table.Column<bool>(type: "boolean", nullable: false),
                    min_password_length = table.Column<int>(type: "integer", nullable: false),
                    ip_whitelist = table.Column<string>(type: "jsonb", nullable: true),
                    session_timeout_minutes = table.Column<int>(type: "integer", nullable: false),
                    is_billable = table.Column<bool>(type: "boolean", nullable: false),
                    monthly_cost_cents = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspaces", x => x.id);
                    table.ForeignKey(
                        name: "FK_workspaces_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_workspaces_workspaces_parent_workspace_id",
                        column: x => x.parent_workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "database_leases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaseId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DatabaseConfigId = table.Column<Guid>(type: "uuid", nullable: false),
                    DatabaseRoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    GeneratedUsername = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EncryptedPassword = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    RenewalCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_database_leases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_database_leases_database_configs_DatabaseConfigId",
                        column: x => x.DatabaseConfigId,
                        principalTable: "database_configs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_database_leases_database_roles_DatabaseRoleId",
                        column: x => x.DatabaseRoleId,
                        principalTable: "database_roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pki_issued_certificates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    serial_number = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    certificate_authority_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_dn = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    certificate_pem = table.Column<string>(type: "text", nullable: false),
                    not_before = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    not_after = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    issued_by = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pki_issued_certificates", x => x.id);
                    table.ForeignKey(
                        name: "FK_pki_issued_certificates_pki_certificate_authorities_certifi~",
                        column: x => x.certificate_authority_id,
                        principalTable: "pki_certificate_authorities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pki_issued_certificates_pki_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "pki_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "jit_accesses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    access_level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    justification = table.Column<string>(type: "text", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approval_id = table.Column<Guid>(type: "uuid", nullable: true),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    granted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_by = table.Column<Guid>(type: "uuid", nullable: true),
                    revocation_reason = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    auto_provisioning_completed = table.Column<bool>(type: "boolean", nullable: false),
                    auto_deprovisioning_completed = table.Column<bool>(type: "boolean", nullable: false),
                    provisioning_details = table.Column<string>(type: "text", nullable: true),
                    deprovisioning_details = table.Column<string>(type: "text", nullable: true),
                    ip_address = table.Column<string>(type: "text", nullable: true),
                    user_agent = table.Column<string>(type: "text", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jit_accesses", x => x.id);
                    table.ForeignKey(
                        name: "FK_jit_accesses_access_approvals_approval_id",
                        column: x => x.approval_id,
                        principalTable: "access_approvals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_jit_accesses_jit_access_templates_template_id",
                        column: x => x.template_id,
                        principalTable: "jit_access_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_jit_accesses_users_revoked_by",
                        column: x => x.revoked_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_jit_accesses_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FlowApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FlowInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApproverId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlowApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlowApprovals_AuthorizationFlowInstances_FlowInstanceId",
                        column: x => x.FlowInstanceId,
                        principalTable: "AuthorizationFlowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FlowApprovals_users_ApproverId",
                        column: x => x.ApproverId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "compliance_controls",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    control_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    control_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    control_description = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    implementation = table.Column<string>(type: "text", nullable: true),
                    evidence = table.Column<string>(type: "text", nullable: true),
                    gaps = table.Column<string>(type: "text", nullable: true),
                    last_assessed = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    assessed_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_controls", x => x.id);
                    table.ForeignKey(
                        name: "FK_compliance_controls_compliance_reports_report_id",
                        column: x => x.report_id,
                        principalTable: "compliance_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_compliance_controls_users_assessed_by",
                        column: x => x.assessed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "privileged_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    safe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    username = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    encrypted_password = table.Column<string>(type: "text", nullable: false),
                    platform = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    host_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    port = table.Column<int>(type: "integer", nullable: true),
                    database_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    connection_details = table.Column<string>(type: "jsonb", nullable: true),
                    rotation_policy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RotationEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    rotation_interval_days = table.Column<int>(type: "integer", nullable: false),
                    last_rotated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    next_rotation = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    password_complexity = table.Column<int>(type: "integer", nullable: true),
                    require_mfa = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresMfa = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresDualApproval = table.Column<bool>(type: "boolean", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_privileged_accounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_privileged_accounts_privileged_safes_safe_id",
                        column: x => x.safe_id,
                        principalTable: "privileged_safes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "secret_access_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    secret_id = table.Column<Guid>(type: "uuid", nullable: true),
                    accessed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    access_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ip_address = table.Column<IPAddress>(type: "inet", nullable: true),
                    user_agent = table.Column<string>(type: "text", nullable: true),
                    accessed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_secret_access_log", x => x.id);
                    table.ForeignKey(
                        name: "FK_secret_access_log_secrets_secret_id",
                        column: x => x.secret_id,
                        principalTable: "secrets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_secret_access_log_users_accessed_by",
                        column: x => x.accessed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transit_key_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transit_key_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    encrypted_key_material = table.Column<string>(type: "text", nullable: false),
                    public_key = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    archived_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    destroyed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transit_key_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_transit_key_versions_transit_keys_transit_key_id",
                        column: x => x.transit_key_id,
                        principalTable: "transit_keys",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_transit_key_versions_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "webhook_deliveries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    webhook_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    response_status = table.Column<int>(type: "integer", nullable: false),
                    response_body = table.Column<string>(type: "text", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    duration_ms = table.Column<int>(type: "integer", nullable: false),
                    hmac_signature = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AttemptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    delivered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    next_retry_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_deliveries", x => x.id);
                    table.ForeignKey(
                        name: "FK_webhook_deliveries_webhooks_webhook_id",
                        column: x => x.webhook_id,
                        principalTable: "webhooks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workspace_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    invitation_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    invitation_token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    invitation_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    invited_by = table.Column<Guid>(type: "uuid", nullable: true),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    left_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_accessed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_members", x => x.id);
                    table.ForeignKey(
                        name: "FK_workspace_members_users_invited_by",
                        column: x => x.invited_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_workspace_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_workspace_members_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workspace_quotas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    max_users = table.Column<int>(type: "integer", nullable: false),
                    max_secrets = table.Column<int>(type: "integer", nullable: false),
                    max_privileged_accounts = table.Column<int>(type: "integer", nullable: false),
                    max_pam_sessions = table.Column<int>(type: "integer", nullable: false),
                    max_api_requests_per_hour = table.Column<int>(type: "integer", nullable: false),
                    max_storage_mb = table.Column<long>(type: "bigint", nullable: false),
                    max_child_workspaces = table.Column<int>(type: "integer", nullable: false),
                    audit_retention_days = table.Column<int>(type: "integer", nullable: false),
                    session_recording_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    advanced_compliance_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    custom_auth_methods_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_quotas", x => x.id);
                    table.ForeignKey(
                        name: "FK_workspace_quotas_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workspace_usages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_users = table.Column<int>(type: "integer", nullable: false),
                    current_secrets = table.Column<int>(type: "integer", nullable: false),
                    current_privileged_accounts = table.Column<int>(type: "integer", nullable: false),
                    current_pam_sessions = table.Column<int>(type: "integer", nullable: false),
                    api_requests_this_hour = table.Column<int>(type: "integer", nullable: false),
                    api_requests_reset_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    current_storage_mb = table.Column<long>(type: "bigint", nullable: false),
                    current_child_workspaces = table.Column<int>(type: "integer", nullable: false),
                    total_api_requests = table.Column<long>(type: "bigint", nullable: false),
                    total_audit_logs = table.Column<long>(type: "bigint", nullable: false),
                    total_session_recordings = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_usages", x => x.id);
                    table.ForeignKey(
                        name: "FK_workspace_usages_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "account_checkouts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    checked_out_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CheckoutTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    checked_in_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    approval_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovalRequired = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    rotate_on_checkin = table.Column<bool>(type: "boolean", nullable: false),
                    was_rotated = table.Column<bool>(type: "boolean", nullable: false),
                    session_recording_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ip_address = table.Column<string>(type: "text", nullable: true),
                    user_agent = table.Column<string>(type: "text", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_checkouts", x => x.id);
                    table.ForeignKey(
                        name: "FK_account_checkouts_access_approvals_approval_id",
                        column: x => x.approval_id,
                        principalTable: "access_approvals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_account_checkouts_privileged_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "privileged_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_account_checkouts_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "privileged_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_checkout_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    protocol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    platform = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    host_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    port = table.Column<int>(type: "integer", nullable: true),
                    recording_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsRecorded = table.Column<bool>(type: "boolean", nullable: false),
                    recording_size = table.Column<long>(type: "bigint", nullable: false),
                    session_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    command_count = table.Column<int>(type: "integer", nullable: false),
                    query_count = table.Column<int>(type: "integer", nullable: false),
                    suspicious_activity_detected = table.Column<bool>(type: "boolean", nullable: false),
                    suspicious_activity_details = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ip_address = table.Column<string>(type: "text", nullable: true),
                    user_agent = table.Column<string>(type: "text", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_privileged_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_privileged_sessions_account_checkouts_account_checkout_id",
                        column: x => x.account_checkout_id,
                        principalTable: "account_checkouts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_privileged_sessions_privileged_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "privileged_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_privileged_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "break_glass_accesses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    Justification = table.Column<string>(type: "text", nullable: false),
                    incident_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    activated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deactivated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    session_recording_mandatory = table.Column<bool>(type: "boolean", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    accessed_resources = table.Column<string>(type: "text", nullable: true),
                    actions_performed = table.Column<string>(type: "text", nullable: true),
                    executive_notified = table.Column<bool>(type: "boolean", nullable: false),
                    executive_notified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notified_executives = table.Column<string>(type: "text", nullable: true),
                    requires_review = table.Column<bool>(type: "boolean", nullable: false),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    review_notes = table.Column<string>(type: "text", nullable: true),
                    review_decision = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ip_address = table.Column<string>(type: "text", nullable: true),
                    user_agent = table.Column<string>(type: "text", nullable: true),
                    location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    device_fingerprint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_break_glass_accesses", x => x.id);
                    table.ForeignKey(
                        name: "FK_break_glass_accesses_privileged_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "privileged_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_break_glass_accesses_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_break_glass_accesses_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "session_commands",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    executed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    command_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    command = table.Column<string>(type: "text", nullable: false),
                    response = table.Column<string>(type: "text", nullable: true),
                    response_size = table.Column<int>(type: "integer", nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    execution_time_ms = table.Column<int>(type: "integer", nullable: false),
                    is_suspicious = table.Column<bool>(type: "boolean", nullable: false),
                    suspicious_reason = table.Column<string>(type: "text", nullable: true),
                    sequence_number = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_commands", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_commands_privileged_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "privileged_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_access_approvals_expires_at",
                table: "access_approvals",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "idx_access_approvals_requester_id",
                table: "access_approvals",
                column: "requester_id");

            migrationBuilder.CreateIndex(
                name: "idx_access_approvals_resource_type",
                table: "access_approvals",
                column: "resource_type");

            migrationBuilder.CreateIndex(
                name: "idx_access_approvals_status",
                table: "access_approvals",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_access_policies_is_active",
                table: "access_policies",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "idx_access_policies_name",
                table: "access_policies",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_access_policies_policy_type",
                table: "access_policies",
                column: "policy_type");

            migrationBuilder.CreateIndex(
                name: "idx_account_checkouts_account_id",
                table: "account_checkouts",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "idx_account_checkouts_expires_at",
                table: "account_checkouts",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "idx_account_checkouts_status",
                table: "account_checkouts",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_account_checkouts_user_id",
                table: "account_checkouts",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_account_checkouts_approval_id",
                table: "account_checkouts",
                column: "approval_id");

            migrationBuilder.CreateIndex(
                name: "idx_api_keys_key_hash",
                table: "api_keys",
                column: "key_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_api_keys_revoked",
                table: "api_keys",
                column: "revoked");

            migrationBuilder.CreateIndex(
                name: "idx_api_keys_user_id",
                table: "api_keys",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_audit_logs_action",
                table: "audit_logs",
                column: "action");

            migrationBuilder.CreateIndex(
                name: "idx_audit_logs_correlation_id",
                table: "audit_logs",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "idx_audit_logs_created_at",
                table: "audit_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_audit_logs_resource_type",
                table: "audit_logs",
                column: "resource_type");

            migrationBuilder.CreateIndex(
                name: "idx_audit_logs_user_id",
                table: "audit_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_AuthorizationFlowInstances_FlowId",
                table: "AuthorizationFlowInstances",
                column: "FlowId");

            migrationBuilder.CreateIndex(
                name: "IX_AuthorizationFlowInstances_RequesterId",
                table: "AuthorizationFlowInstances",
                column: "RequesterId");

            migrationBuilder.CreateIndex(
                name: "idx_break_glass_accesses_activated_at",
                table: "break_glass_accesses",
                column: "activated_at");

            migrationBuilder.CreateIndex(
                name: "idx_break_glass_accesses_expires_at",
                table: "break_glass_accesses",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "idx_break_glass_accesses_incident_type",
                table: "break_glass_accesses",
                column: "incident_type");

            migrationBuilder.CreateIndex(
                name: "idx_break_glass_accesses_requires_review",
                table: "break_glass_accesses",
                column: "requires_review");

            migrationBuilder.CreateIndex(
                name: "idx_break_glass_accesses_status",
                table: "break_glass_accesses",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_break_glass_accesses_user_id",
                table: "break_glass_accesses",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_break_glass_accesses_reviewed_by",
                table: "break_glass_accesses",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "IX_break_glass_accesses_session_id",
                table: "break_glass_accesses",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "idx_break_glass_policies_enabled",
                table: "break_glass_policies",
                column: "enabled");

            migrationBuilder.CreateIndex(
                name: "idx_break_glass_policies_name",
                table: "break_glass_policies",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "idx_baa_expiration_date",
                table: "business_associate_agreements",
                column: "expiration_date");

            migrationBuilder.CreateIndex(
                name: "idx_baa_partner_id",
                table: "business_associate_agreements",
                column: "partner_id");

            migrationBuilder.CreateIndex(
                name: "idx_baa_partner_name",
                table: "business_associate_agreements",
                column: "partner_name");

            migrationBuilder.CreateIndex(
                name: "idx_baa_status",
                table: "business_associate_agreements",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_baa_status_expiration",
                table: "business_associate_agreements",
                columns: new[] { "status", "expiration_date" });

            migrationBuilder.CreateIndex(
                name: "IX_business_associate_agreements_last_reviewed_by",
                table: "business_associate_agreements",
                column: "last_reviewed_by");

            migrationBuilder.CreateIndex(
                name: "IX_ColumnSecurityRules_IsActive",
                table: "ColumnSecurityRules",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ColumnSecurityRules_Table_Column_Operation",
                table: "ColumnSecurityRules",
                columns: new[] { "TableName", "ColumnName", "Operation" });

            migrationBuilder.CreateIndex(
                name: "idx_compliance_controls_control_id",
                table: "compliance_controls",
                column: "control_id");

            migrationBuilder.CreateIndex(
                name: "idx_compliance_controls_report_id",
                table: "compliance_controls",
                column: "report_id");

            migrationBuilder.CreateIndex(
                name: "idx_compliance_controls_status",
                table: "compliance_controls",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_controls_assessed_by",
                table: "compliance_controls",
                column: "assessed_by");

            migrationBuilder.CreateIndex(
                name: "idx_compliance_reports_framework",
                table: "compliance_reports",
                column: "framework");

            migrationBuilder.CreateIndex(
                name: "idx_compliance_reports_generated_at",
                table: "compliance_reports",
                column: "generated_at");

            migrationBuilder.CreateIndex(
                name: "idx_compliance_reports_status",
                table: "compliance_reports",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_reports_generated_by",
                table: "compliance_reports",
                column: "generated_by");

            migrationBuilder.CreateIndex(
                name: "IX_ContextPolicies_Resource_Action_Active",
                table: "ContextPolicies",
                columns: new[] { "ResourceType", "Action", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_database_configs_ConfiguredAt",
                table: "database_configs",
                column: "ConfiguredAt");

            migrationBuilder.CreateIndex(
                name: "IX_database_configs_Name",
                table: "database_configs",
                column: "Name",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_database_configs_Plugin",
                table: "database_configs",
                column: "Plugin");

            migrationBuilder.CreateIndex(
                name: "IX_database_leases_CreatedAt",
                table: "database_leases",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_database_leases_DatabaseConfigId_IsRevoked",
                table: "database_leases",
                columns: new[] { "DatabaseConfigId", "IsRevoked" });

            migrationBuilder.CreateIndex(
                name: "IX_database_leases_DatabaseRoleId",
                table: "database_leases",
                column: "DatabaseRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_database_leases_ExpiresAt",
                table: "database_leases",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_database_leases_IsRevoked",
                table: "database_leases",
                column: "IsRevoked");

            migrationBuilder.CreateIndex(
                name: "IX_database_leases_LeaseId",
                table: "database_leases",
                column: "LeaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_database_roles_CreatedAt",
                table: "database_roles",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_database_roles_DatabaseConfigId_RoleName",
                table: "database_roles",
                columns: new[] { "DatabaseConfigId", "RoleName" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_FlowApprovals_ApproverId",
                table: "FlowApprovals",
                column: "ApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_FlowApprovals_FlowInstanceId",
                table: "FlowApprovals",
                column: "FlowInstanceId");

            migrationBuilder.CreateIndex(
                name: "idx_jit_access_templates_active",
                table: "jit_access_templates",
                column: "active");

            migrationBuilder.CreateIndex(
                name: "idx_jit_access_templates_name",
                table: "jit_access_templates",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "idx_jit_access_templates_resource_type",
                table: "jit_access_templates",
                column: "resource_type");

            migrationBuilder.CreateIndex(
                name: "idx_jit_accesses_expires_at",
                table: "jit_accesses",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "idx_jit_accesses_requested_at",
                table: "jit_accesses",
                column: "requested_at");

            migrationBuilder.CreateIndex(
                name: "idx_jit_accesses_resource_id",
                table: "jit_accesses",
                column: "resource_id");

            migrationBuilder.CreateIndex(
                name: "idx_jit_accesses_resource_type",
                table: "jit_accesses",
                column: "resource_type");

            migrationBuilder.CreateIndex(
                name: "idx_jit_accesses_status",
                table: "jit_accesses",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_jit_accesses_user_id",
                table: "jit_accesses",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_jit_accesses_approval_id",
                table: "jit_accesses",
                column: "approval_id");

            migrationBuilder.CreateIndex(
                name: "IX_jit_accesses_revoked_by",
                table: "jit_accesses",
                column: "revoked_by");

            migrationBuilder.CreateIndex(
                name: "IX_jit_accesses_template_id",
                table: "jit_accesses",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "idx_ldap_configs_is_active",
                table: "ldap_configurations",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "idx_ldap_configs_name",
                table: "ldap_configurations",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ldap_configurations_created_by",
                table: "ldap_configurations",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_MagicLinks_UserId",
                table: "MagicLinks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MfaBackupCodes_UserId",
                table: "MfaBackupCodes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MfaDevices_UserId",
                table: "MfaDevices",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OAuth2AuthorizationCodes_ClientId",
                table: "OAuth2AuthorizationCodes",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_OAuth2AuthorizationCodes_UserId",
                table: "OAuth2AuthorizationCodes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "idx_permissions_action",
                table: "permissions",
                column: "action");

            migrationBuilder.CreateIndex(
                name: "idx_permissions_resource",
                table: "permissions",
                column: "resource");

            migrationBuilder.CreateIndex(
                name: "unique_permission",
                table: "permissions",
                columns: new[] { "resource", "action" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_pki_cas_name",
                table: "pki_certificate_authorities",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_pki_cas_parent_ca_id",
                table: "pki_certificate_authorities",
                column: "parent_ca_id");

            migrationBuilder.CreateIndex(
                name: "idx_pki_issued_certs_ca_id",
                table: "pki_issued_certificates",
                column: "certificate_authority_id");

            migrationBuilder.CreateIndex(
                name: "idx_pki_issued_certs_revoked",
                table: "pki_issued_certificates",
                column: "revoked");

            migrationBuilder.CreateIndex(
                name: "idx_pki_issued_certs_role_id",
                table: "pki_issued_certificates",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "idx_pki_issued_certs_serial",
                table: "pki_issued_certificates",
                column: "serial_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_pki_roles_ca_id",
                table: "pki_roles",
                column: "certificate_authority_id");

            migrationBuilder.CreateIndex(
                name: "idx_pki_roles_name",
                table: "pki_roles",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_privileged_accounts_next_rotation",
                table: "privileged_accounts",
                column: "next_rotation");

            migrationBuilder.CreateIndex(
                name: "idx_privileged_accounts_platform",
                table: "privileged_accounts",
                column: "platform");

            migrationBuilder.CreateIndex(
                name: "idx_privileged_accounts_safe_id",
                table: "privileged_accounts",
                column: "safe_id");

            migrationBuilder.CreateIndex(
                name: "idx_privileged_accounts_status",
                table: "privileged_accounts",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_privileged_safes_owner_id",
                table: "privileged_safes",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "idx_privileged_safes_safe_type",
                table: "privileged_safes",
                column: "safe_type");

            migrationBuilder.CreateIndex(
                name: "idx_privileged_sessions_account_id",
                table: "privileged_sessions",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "idx_privileged_sessions_checkout_id",
                table: "privileged_sessions",
                column: "account_checkout_id");

            migrationBuilder.CreateIndex(
                name: "idx_privileged_sessions_start_time",
                table: "privileged_sessions",
                column: "start_time");

            migrationBuilder.CreateIndex(
                name: "idx_privileged_sessions_status",
                table: "privileged_sessions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_privileged_sessions_suspicious",
                table: "privileged_sessions",
                column: "suspicious_activity_detected");

            migrationBuilder.CreateIndex(
                name: "idx_privileged_sessions_user_id",
                table: "privileged_sessions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_RiskAssessments_UserId",
                table: "RiskAssessments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_permission_id",
                table: "role_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "idx_saml_idps_entity_id",
                table: "saml_identity_providers",
                column: "entity_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_saml_idps_is_enabled",
                table: "saml_identity_providers",
                column: "is_enabled");

            migrationBuilder.CreateIndex(
                name: "idx_saml_idps_name",
                table: "saml_identity_providers",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_saml_identity_providers_created_by",
                table: "saml_identity_providers",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_saml_identity_providers_default_role_id",
                table: "saml_identity_providers",
                column: "default_role_id");

            migrationBuilder.CreateIndex(
                name: "idx_secret_access_log_accessed_at",
                table: "secret_access_log",
                column: "accessed_at");

            migrationBuilder.CreateIndex(
                name: "idx_secret_access_log_secret_id",
                table: "secret_access_log",
                column: "secret_id");

            migrationBuilder.CreateIndex(
                name: "IX_secret_access_log_accessed_by",
                table: "secret_access_log",
                column: "accessed_by");

            migrationBuilder.CreateIndex(
                name: "idx_secrets_created_by",
                table: "secrets",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "idx_secrets_path",
                table: "secrets",
                column: "path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_session_commands_executed_at",
                table: "session_commands",
                column: "executed_at");

            migrationBuilder.CreateIndex(
                name: "idx_session_commands_sequence",
                table: "session_commands",
                column: "sequence_number");

            migrationBuilder.CreateIndex(
                name: "idx_session_commands_session_id",
                table: "session_commands",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "idx_session_commands_suspicious",
                table: "session_commands",
                column: "is_suspicious");

            migrationBuilder.CreateIndex(
                name: "idx_sessions_expires_at",
                table: "sessions",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "idx_sessions_revoked",
                table: "sessions",
                column: "revoked");

            migrationBuilder.CreateIndex(
                name: "idx_sessions_token_hash",
                table: "sessions",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_sessions_user_id",
                table: "sessions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_transit_key_versions_transit_key_id",
                table: "transit_key_versions",
                column: "transit_key_id");

            migrationBuilder.CreateIndex(
                name: "idx_transit_key_versions_transit_key_id_version",
                table: "transit_key_versions",
                columns: new[] { "transit_key_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transit_key_versions_created_by",
                table: "transit_key_versions",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "idx_transit_keys_created_by",
                table: "transit_keys",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "idx_transit_keys_name",
                table: "transit_keys",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrustedDevices_UserId",
                table: "TrustedDevices",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "idx_user_clearances_expires_at",
                table: "user_clearances",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "idx_user_clearances_status",
                table: "user_clearances",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_user_clearances_type",
                table: "user_clearances",
                column: "clearance_type");

            migrationBuilder.CreateIndex(
                name: "idx_user_clearances_user_id",
                table: "user_clearances",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_user_clearances_user_type_status",
                table: "user_clearances",
                columns: new[] { "user_id", "clearance_type", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_user_clearances_granted_by",
                table: "user_clearances",
                column: "granted_by");

            migrationBuilder.CreateIndex(
                name: "IX_user_clearances_last_reviewed_by",
                table: "user_clearances",
                column: "last_reviewed_by");

            migrationBuilder.CreateIndex(
                name: "idx_user_roles_role_id",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "idx_user_roles_user_id",
                table: "user_roles",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_granted_by",
                table: "user_roles",
                column: "granted_by");

            migrationBuilder.CreateIndex(
                name: "IX_UserRiskProfiles_UserId",
                table: "UserRiskProfiles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "idx_users_email",
                table: "users",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "idx_users_status",
                table: "users",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_users_username",
                table: "users",
                column: "username");

            migrationBuilder.CreateIndex(
                name: "IX_WebAuthnCredentials_UserId",
                table: "WebAuthnCredentials",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "idx_webhook_deliveries_created_at",
                table: "webhook_deliveries",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_webhook_deliveries_event_type",
                table: "webhook_deliveries",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "idx_webhook_deliveries_next_retry_at",
                table: "webhook_deliveries",
                column: "next_retry_at");

            migrationBuilder.CreateIndex(
                name: "idx_webhook_deliveries_status",
                table: "webhook_deliveries",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_webhook_deliveries_webhook_id",
                table: "webhook_deliveries",
                column: "webhook_id");

            migrationBuilder.CreateIndex(
                name: "idx_webhooks_active",
                table: "webhooks",
                column: "active");

            migrationBuilder.CreateIndex(
                name: "idx_webhooks_events",
                table: "webhooks",
                column: "events")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "idx_webhooks_user_id",
                table: "webhooks",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_workspace_members_invitation_token",
                table: "workspace_members",
                column: "invitation_token");

            migrationBuilder.CreateIndex(
                name: "idx_workspace_members_is_active",
                table: "workspace_members",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "idx_workspace_members_user_id",
                table: "workspace_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_workspace_members_workspace_id",
                table: "workspace_members",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "idx_workspace_members_workspace_user",
                table: "workspace_members",
                columns: new[] { "workspace_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workspace_members_invited_by",
                table: "workspace_members",
                column: "invited_by");

            migrationBuilder.CreateIndex(
                name: "idx_workspace_quotas_workspace_id",
                table: "workspace_quotas",
                column: "workspace_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_workspace_usages_workspace_id",
                table: "workspace_usages",
                column: "workspace_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_workspaces_custom_domain",
                table: "workspaces",
                column: "custom_domain");

            migrationBuilder.CreateIndex(
                name: "idx_workspaces_deleted_at",
                table: "workspaces",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "idx_workspaces_owner_id",
                table: "workspaces",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "idx_workspaces_parent_id",
                table: "workspaces",
                column: "parent_workspace_id");

            migrationBuilder.CreateIndex(
                name: "idx_workspaces_slug",
                table: "workspaces",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_workspaces_status",
                table: "workspaces",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access_policies");

            migrationBuilder.DropTable(
                name: "api_keys");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "break_glass_accesses");

            migrationBuilder.DropTable(
                name: "break_glass_policies");

            migrationBuilder.DropTable(
                name: "business_associate_agreements");

            migrationBuilder.DropTable(
                name: "ColumnSecurityRules");

            migrationBuilder.DropTable(
                name: "compliance_controls");

            migrationBuilder.DropTable(
                name: "ContextPolicies");

            migrationBuilder.DropTable(
                name: "database_leases");

            migrationBuilder.DropTable(
                name: "FlowApprovals");

            migrationBuilder.DropTable(
                name: "jit_accesses");

            migrationBuilder.DropTable(
                name: "ldap_configurations");

            migrationBuilder.DropTable(
                name: "MagicLinks");

            migrationBuilder.DropTable(
                name: "MfaBackupCodes");

            migrationBuilder.DropTable(
                name: "MfaDevices");

            migrationBuilder.DropTable(
                name: "OAuth2AuthorizationCodes");

            migrationBuilder.DropTable(
                name: "pki_issued_certificates");

            migrationBuilder.DropTable(
                name: "RiskAssessments");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "saml_identity_providers");

            migrationBuilder.DropTable(
                name: "SealConfigurations");

            migrationBuilder.DropTable(
                name: "secret_access_log");

            migrationBuilder.DropTable(
                name: "session_commands");

            migrationBuilder.DropTable(
                name: "sessions");

            migrationBuilder.DropTable(
                name: "transit_key_versions");

            migrationBuilder.DropTable(
                name: "TrustedDevices");

            migrationBuilder.DropTable(
                name: "user_clearances");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "UserRiskProfiles");

            migrationBuilder.DropTable(
                name: "WebAuthnCredentials");

            migrationBuilder.DropTable(
                name: "webhook_deliveries");

            migrationBuilder.DropTable(
                name: "workspace_members");

            migrationBuilder.DropTable(
                name: "workspace_quotas");

            migrationBuilder.DropTable(
                name: "workspace_usages");

            migrationBuilder.DropTable(
                name: "compliance_reports");

            migrationBuilder.DropTable(
                name: "database_roles");

            migrationBuilder.DropTable(
                name: "AuthorizationFlowInstances");

            migrationBuilder.DropTable(
                name: "jit_access_templates");

            migrationBuilder.DropTable(
                name: "OAuth2Clients");

            migrationBuilder.DropTable(
                name: "pki_roles");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "secrets");

            migrationBuilder.DropTable(
                name: "privileged_sessions");

            migrationBuilder.DropTable(
                name: "transit_keys");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "webhooks");

            migrationBuilder.DropTable(
                name: "workspaces");

            migrationBuilder.DropTable(
                name: "database_configs");

            migrationBuilder.DropTable(
                name: "AuthorizationFlows");

            migrationBuilder.DropTable(
                name: "pki_certificate_authorities");

            migrationBuilder.DropTable(
                name: "account_checkouts");

            migrationBuilder.DropTable(
                name: "access_approvals");

            migrationBuilder.DropTable(
                name: "privileged_accounts");

            migrationBuilder.DropTable(
                name: "privileged_safes");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}

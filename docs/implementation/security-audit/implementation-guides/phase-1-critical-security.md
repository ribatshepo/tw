# Phase 1: Critical Security - Week 1 Implementation Guide

**Phase:** 1 of 6
**Duration:** Week 1 (5 days)
**Focus:** P0 Critical Findings - Production Blockers
**Team:** Security + Backend + DevOps
**Deliverable:** All P0 findings resolved, production deployment unblocked

---

## Overview

Phase 1 addresses all **8 P0 Critical findings** that block production deployment. These are security vulnerabilities with immediate, severe impact that must be resolved before any production launch.

**Critical Path:** All P0 findings must complete sequentially in order shown below due to dependencies.

---

## Findings Roadmap

| Day | Finding ID | Title | Hours | Status |
|-----|-----------|-------|-------|--------|
| 1 | SEC-P0-001 | Hardcoded Secrets in .env Files | 4h | Not Started |
| 2 | SEC-P0-002 | Hardcoded Secrets in appsettings.Development.json | 3h | Not Started |
| 2 | SEC-P0-003 | Hardcoded SQL Passwords in Migration Scripts | 2h | Not Started |
| 3 | SEC-P0-004 | Vault Seal/Unseal Endpoints Unauthenticated | 2h | Not Started |
| 3 | SEC-P0-005 | JWT Bearer Middleware Missing | 1h | Not Started |
| 4 | SEC-P0-008 | TrustServerCertificate=true in Production | 3h | Not Started |
| 5 | SEC-P0-006 | TODO Comments in Production Code | 2h | Not Started |
| 5 | SEC-P0-007 | NotImplementedException in HSM Support | 3h | Not Started |

**Total Effort:** 20 hours (4 days + 1 buffer day)

---

## Day 1: Migrate .env Secrets to Vault (4 hours)

### Objective
Remove all hardcoded secrets from `.env` files and migrate to USP Vault.

### Prerequisites
- [ ] USP Vault deployed and unsealed
- [ ] Vault root token available
- [ ] Database connection working

### Implementation Steps

**1. Inventory Secrets in .env (30 minutes)**

```bash
cd services/usp

# List all secrets in .env
cat .env | grep -E "PASSWORD|SECRET|KEY" | cut -d'=' -f1

# Expected secrets:
# - Database__Password
# - Redis__Password
# - Jwt__SecretKey
# - Encryption__MasterKey
# - EmailService__Password
```

**2. Create Secrets in Vault (1 hour)**

```bash
# Login to Vault
export VAULT_TOKEN="<root-token>"

# Create database credentials
curl -k -X POST https://localhost:5001/api/v1/secrets \
  -H "Authorization: Bearer $VAULT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "path": "/database/usp",
    "data": {
      "host": "localhost",
      "port": "5432",
      "database": "usp_prod",
      "username": "usp_user",
      "password": "'"$(openssl rand -base64 32)"'"
    }
  }'

# Create JWT secret
curl -k -X POST https://localhost:5001/api/v1/secrets \
  -H "Authorization: Bearer $VAULT_TOKEN" \
  -d '{
    "path": "/jwt/secret",
    "data": {
      "key": "'"$(openssl rand -base64 64)"'"
    }
  }'

# Create encryption master key
curl -k -X POST https://localhost:5001/api/v1/secrets \
  -H "Authorization: Bearer $VAULT_TOKEN" \
  -d '{
    "path": "/encryption/master-key",
    "data": {
      "key": "'"$(openssl rand -base64 32)"'"
    }
  }'
```

**3. Update Application to Fetch from Vault (2 hours)**

```csharp
// Program.cs - Add Vault client initialization

builder.Services.AddSingleton<IVaultClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var vaultUrl = config["Vault:Url"];
    var vaultToken = config["Vault:Token"]; // From environment variable

    return new VaultClient(vaultUrl, vaultToken);
});

// Fetch secrets on startup
var vaultClient = app.Services.GetRequiredService<IVaultClient>();

// Fetch database password
var dbSecret = await vaultClient.GetSecretAsync("/database/usp");
var dbPassword = dbSecret.Data["password"].ToString();

// Update configuration
builder.Configuration["Database:Password"] = dbPassword;

// Fetch JWT secret
var jwtSecret = await vaultClient.GetSecretAsync("/jwt/secret");
builder.Configuration["Jwt:SecretKey"] = jwtSecret.Data["key"].ToString();

// Fetch encryption key
var encryptionKey = await vaultClient.GetSecretAsync("/encryption/master-key");
builder.Configuration["Encryption:MasterKey"] = encryptionKey.Data["key"].ToString();
```

**4. Remove Secrets from .env (30 minutes)**

```bash
# Backup original .env
cp .env .env.backup

# Remove secrets from .env (keep structure)
cat > .env <<'EOF'
# Database Configuration (password from Vault)
Database__Host=localhost
Database__Port=5432
Database__Database=usp_dev
Database__Username=usp_user
# Database__Password=  # REMOVED - Fetched from Vault

# JWT Configuration (secret from Vault)
Jwt__Issuer=USP
Jwt__Audience=TW-Platform
# Jwt__SecretKey=  # REMOVED - Fetched from Vault

# Vault Configuration
Vault__Url=https://localhost:5001
# Vault__Token loaded from environment variable
EOF

# Add .env to .gitignore (if not already)
echo ".env" >> .gitignore
echo ".env.backup" >> .gitignore
```

**5. Test Application Startup (30 minutes)**

```bash
# Set Vault token in environment
export VAULT_TOKEN="<root-token>"

# Start application
dotnet run --project src/USP.API

# Verify secrets loaded
# Check logs for "Secrets loaded from Vault" message

# Test database connection
curl -k https://localhost:5001/health
# Expected: {"status":"Healthy","vault":{"sealed":false}}

# Test JWT authentication
curl -k -X POST https://localhost:5001/api/v1/auth/login \
  -d '{"username":"admin","password":"admin"}' \
  -H "Content-Type: application/json"
# Expected: JWT token returned
```

### Deliverable
- [ ] All .env secrets migrated to Vault
- [ ] Application successfully fetches secrets from Vault
- [ ] .env file cleaned of secrets
- [ ] Application starts and connects to database

### Verification
```bash
# Verify no secrets in .env
grep -E "PASSWORD|SECRET|KEY.*=" .env
# Expected: 0 results

# Verify secrets in Vault
curl -k -H "Authorization: Bearer $VAULT_TOKEN" \
  https://localhost:5001/api/v1/secrets/database/usp
# Expected: Secret data returned
```

---

## Day 2: Migrate appsettings Secrets & SQL Passwords (5 hours)

### Morning: appsettings Secrets (3 hours)

**SEC-P0-002: Remove appsettings.Development.json Secrets**

```bash
# Backup appsettings
cp appsettings.Development.json appsettings.Development.json.backup

# Remove secrets from appsettings
cat > appsettings.Development.json <<'EOF'
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Database": {
    "Host": "localhost",
    "Port": 5432
    // Password fetched from Vault
  },
  "Jwt": {
    "Issuer": "USP",
    "Audience": "TW-Platform"
    // SecretKey fetched from Vault
  }
}
EOF
```

Same Vault fetch logic from Day 1 applies.

### Afternoon: SQL Password Parameterization (2 hours)

**SEC-P0-003: Parameterize SQL Migration Passwords**

```sql
-- 02-create-roles.sql

-- BEFORE:
-- CREATE USER usp_user WITH PASSWORD 'usp_dev_password_change_me';

-- AFTER:
CREATE USER usp_user WITH PASSWORD :'USP_DB_PASSWORD';
CREATE USER uccp_user WITH PASSWORD :'UCCP_DB_PASSWORD';
CREATE USER nccs_user WITH PASSWORD :'NCCS_DB_PASSWORD';
CREATE USER udps_user WITH PASSWORD :'UDPS_DB_PASSWORD';
CREATE USER stream_user WITH PASSWORD :'STREAM_DB_PASSWORD';
```

```bash
# Create credential loader script
cat > scripts/db/load-db-credentials.sh <<'EOF'
#!/usr/bin/env bash
set -euo pipefail

export USP_DB_PASSWORD=$(curl -s -k -H "Authorization: Bearer $VAULT_TOKEN" \
  https://localhost:5001/api/v1/secrets/database/usp_user | jq -r '.data.password')

export UCCP_DB_PASSWORD=$(curl -s -k -H "Authorization: Bearer $VAULT_TOKEN" \
  https://localhost:5001/api/v1/secrets/database/uccp_user | jq -r '.data.password')

# ... repeat for other users
EOF

chmod +x scripts/db/load-db-credentials.sh

# Update bootstrap script
cat > scripts/bootstrap-database.sh <<'EOF'
#!/usr/bin/env bash
source scripts/db/load-db-credentials.sh

psql -h localhost -U postgres -d postgres \
  -v USP_DB_PASSWORD="$USP_DB_PASSWORD" \
  -v UCCP_DB_PASSWORD="$UCCP_DB_PASSWORD" \
  -f migrations/sql/02-create-roles.sql
EOF

chmod +x scripts/bootstrap-database.sh
```

### Deliverable
- [ ] appsettings.Development.json cleaned of secrets
- [ ] SQL scripts use :VAR_NAME syntax
- [ ] Credential loader script working
- [ ] Database bootstrap successful with Vault credentials

---

## Day 3: Authentication & Authorization (3 hours)

### Morning: Vault Authentication (2 hours)

**SEC-P0-004: Protect Vault Seal/Unseal Endpoints**

```csharp
// Attributes/RequireVaultTokenAttribute.cs
public class RequireVaultTokenAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var token = context.HttpContext.Request.Headers["X-Vault-Token"].FirstOrDefault();

        if (string.IsNullOrEmpty(token))
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                error = "vault_token_required",
                message = "X-Vault-Token header required"
            });
            return;
        }

        // Validate token
        var vaultService = context.HttpContext.RequestServices
            .GetRequiredService<IVaultService>();

        if (!vaultService.ValidateRootToken(token))
        {
            context.Result = new ForbidResult();
        }
    }
}

// VaultController.cs - Add attribute to endpoints
[HttpPost("seal")]
[RequireVaultToken]  // ✅ Now protected
public async Task<IActionResult> Seal()
{
    await _vaultService.SealAsync();
    return Ok();
}

[HttpPost("seal/unseal")]
[RequireVaultToken]  // ✅ Now protected
public async Task<IActionResult> Unseal([FromBody] UnsealRequest request)
{
    var result = await _vaultService.UnsealAsync(request.Key);
    return Ok(result);
}
```

### Afternoon: JWT Middleware (1 hour)

**SEC-P0-005: Add JWT Bearer Authentication**

```csharp
// Program.cs

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSecret = await vaultClient.GetSecretAsync("/jwt/secret");
        var secretKey = jwtSecret.Data["key"].ToString();

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(secretKey))
        };
    });

builder.Services.AddAuthorization();

// Add middleware
app.UseAuthentication();  // ✅ Enable JWT validation
app.UseAuthorization();
```

### Deliverable
- [ ] Vault endpoints require X-Vault-Token
- [ ] JWT Bearer middleware configured
- [ ] All API endpoints protected by [Authorize]
- [ ] Authentication tests passing

---

## Day 4: PostgreSQL TLS Configuration (3 hours)

**SEC-P0-008: Remove TrustServerCertificate=true**

### Implementation Steps

**1. Generate PostgreSQL Certificates (1 hour)**

```bash
cd config/postgresql

# Generate CA
openssl req -new -x509 -days 3650 -nodes \
  -out ca.crt -keyout ca.key \
  -subj "/CN=PostgreSQL CA"

# Generate server certificate
openssl req -new -nodes \
  -out server.csr -keyout server.key \
  -subj "/CN=postgres.local"

openssl x509 -req -in server.csr \
  -CA ca.crt -CAkey ca.key -CAcreateserial \
  -out server.crt -days 3650

# Set permissions
chmod 600 server.key ca.key
chmod 644 server.crt ca.crt
```

**2. Configure PostgreSQL (1 hour)**

```conf
# postgresql.conf
ssl = on
ssl_cert_file = '/var/lib/postgresql/certs/server.crt'
ssl_key_file = '/var/lib/postgresql/certs/server.key'
ssl_ca_file = '/var/lib/postgresql/certs/ca.crt'
```

```yaml
# docker-compose.infra.yml
postgres:
  volumes:
    - ./config/postgresql/certs:/var/lib/postgresql/certs:ro
```

**3. Update Connection Strings (1 hour)**

```json
// appsettings.json
{
  "Database": {
    "ConnectionString": "Host=localhost;Port=5432;Database=usp_prod;Username=usp_user;Password=<from-vault>;SSL Mode=Require;Root Certificate=/path/to/ca.crt"
  }
}
```

```csharp
// Remove TrustServerCertificate from all connection strings
// Search: TrustServerCertificate=true
// Replace with: SSL Mode=Require;Root Certificate={certificatePath}
```

### Deliverable
- [ ] PostgreSQL certificates generated
- [ ] PostgreSQL SSL enabled
- [ ] Connection strings use SSL Mode=Require
- [ ] TrustServerCertificate=true removed
- [ ] Database connection working with TLS

---

## Day 5: Code Quality & Production Readiness (5 hours)

### Morning: TODO Comment Resolution (2 hours)

**SEC-P0-006: Resolve All TODO Comments**

```bash
# Find all TODOs
grep -rn "TODO" src/ --include="*.cs"

# Critical TODOs to fix:
# 1. Program.cs:230 - MapMetrics
# 2. EncryptionService.cs - HSM implementation
# 3. VaultService.cs - Transaction support
```

**Fix MapMetrics:**
```bash
dotnet add package prometheus-net.AspNetCore
```

```csharp
// Program.cs
using Prometheus;

app.UseHttpMetrics();
app.MapMetrics("/metrics");  // ✅ TODO removed, issue fixed
```

### Afternoon: HSM Implementation (3 hours)

**SEC-P0-007: Resolve NotImplementedException**

**Option 1: Document Limitation (30 minutes)**
```csharp
/// <summary>
/// HSM encryption not supported in v1.0.
/// </summary>
/// <remarks>
/// Use EncryptAsync() for software-based AES-256-GCM encryption.
/// HSM support planned for v2.0.
/// </remarks>
public Task<byte[]> EncryptWithHsmAsync(byte[] plaintext)
{
    throw new NotSupportedException(
        "HSM encryption not available. Use EncryptAsync() instead.");
}
```

**Option 2: Basic Implementation (3 hours)** - If time permits
```csharp
// Implement basic PKCS#11 HSM support
// (See SEC-P0-007 finding document for full implementation)
```

### Deliverable
- [ ] All TODO comments resolved
- [ ] MapMetrics working
- [ ] NotImplementedException replaced
- [ ] Code compiles without warnings
- [ ] All tests passing

---

## End of Week 1: Verification Checklist

### Security Verification

- [ ] **Secrets Management**
  - [ ] No hardcoded secrets in .env
  - [ ] No hardcoded secrets in appsettings.json
  - [ ] No hardcoded SQL passwords
  - [ ] All secrets stored in Vault
  - [ ] Application fetches secrets from Vault successfully

- [ ] **Authentication**
  - [ ] Vault seal/unseal requires X-Vault-Token
  - [ ] JWT Bearer middleware validates tokens
  - [ ] Unauthenticated requests return 401
  - [ ] Invalid tokens return 403

- [ ] **TLS Security**
  - [ ] PostgreSQL connections use TLS
  - [ ] TrustServerCertificate=true removed
  - [ ] Certificate validation working

- [ ] **Code Quality**
  - [ ] Zero TODO comments in production code
  - [ ] Zero NotImplementedException
  - [ ] All P0 findings marked "Complete"

### Compliance Verification

- [ ] **SOC 2 CC6.1** - Access controls implemented
- [ ] **HIPAA 164.312(a)** - Authentication mechanisms in place
- [ ] **PCI-DSS Req 8.2.1** - No hardcoded credentials

### Testing

```bash
# Run full test suite
dotnet test

# Security scan
trivy fs --severity CRITICAL,HIGH .

# Secret scan
git secrets --scan

# Build verification
dotnet build --configuration Release
```

---

## Handoff to Phase 2

**Phase 1 Complete Criteria:**
- All 8 P0 findings resolved
- Production deployment unblocked
- Security baseline established
- Compliance evidence collected

**Phase 2 Preview:**
- P1 findings (TLS, Observability, Authorization)
- 12 tasks over Week 2
- Non-blocking but recommended before production

---

**Status:** Ready to Start
**Last Updated:** 2025-12-27
**Phase Owner:** Security Engineering Lead

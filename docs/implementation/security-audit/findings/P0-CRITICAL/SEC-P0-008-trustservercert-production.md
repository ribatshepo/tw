# SEC-P0-008: TrustServerCertificate=true in Production

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P0-008 |
| **Title** | Database TrustServerCertificate=true in Production Configuration |
| **Priority** | P0 - CRITICAL |
| **Severity** | Critical (Production Only) |
| **Category** | TLS/HTTPS Security |
| **Status** | Not Started |
| **Effort Estimate** | 6 hours |
| **Implementation Phase** | Phase 1 (Week 1, Day 2-3) |
| **Assigned To** | Backend Engineer 2 + DevOps Engineer |
| **Reviewers** | Security Engineer, DBA |
| **Created** | 2025-12-27 |
| **Last Updated** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:160-165` |
| **Security Spec** | `/home/tshepo/projects/tw/docs/specs/security.md` (TLS Security) |
| **Code Files** | `/home/tshepo/projects/tw/services/usp/src/USP.API/appsettings.json:21` |
| **Dependencies** | None |
| **Blocks** | SEC-P1-002 (HSTS), SEC-P1-003 (Elasticsearch HTTPS) |
| **Related Findings** | SEC-P0-003 (Database Passwords) |
| **Compliance Impact** | SOC 2 (CC6.6 - Encryption), HIPAA (164.312(e)(1)), PCI-DSS (Req 4.1), GDPR (Article 32) |

---

## 3. Executive Summary

### Problem Statement

The database connection string in `appsettings.json:21` contains `"TrustServerCertificate": true`, which **disables TLS certificate validation** for PostgreSQL connections. While acceptable for local development, this creates a **critical Man-in-the-Middle (MITM) attack vector** in production.

### Business Impact

- **MITM Attack:** Attacker can intercept database traffic without detection
- **Credential Theft:** Database username/password can be stolen during authentication
- **Data Breach:** All database traffic (queries, results, updates) can be intercepted and modified
- **Compliance Violation:** Violates SOC 2 CC6.6 (encryption), HIPAA 164.312(e)(1) (transmission security), PCI-DSS Req 4.1 (encryption in transit), GDPR Article 32
- **Production Blocker:** P0 finding that **BLOCKS PRODUCTION DEPLOYMENT**

### Solution Overview

1. **Generate PostgreSQL server certificate** (self-signed for dev, CA-signed for prod)
2. **Configure PostgreSQL to require TLS** (`ssl=on` in postgresql.conf)
3. **Update connection string** to set `TrustServerCertificate=false`
4. **Add certificate validation** in connection string (`SslMode=Require` or `SslMode=VerifyFull`)
5. **Test database connections** with TLS validation enabled
6. **Create environment-specific configurations** (dev vs staging vs production)

**Timeline:** 6 hours (Day 2-3 of Week 1, parallel with SEC-P0-003)

---

## 4. Technical Details

### Current State

**File: `/home/tshepo/projects/tw/services/usp/src/USP.API/appsettings.json`**

```json
{
  "Database": {
    "Host": "localhost",
    "Port": "5432",
    "Name": "usp_db",
    "Username": "usp_user",
    "Password": "from_environment_or_user_secrets",
    "TrustServerCertificate": true  // ❌ DISABLES CERTIFICATE VALIDATION
  }
}
```

**Connection String Construction:**
```csharp
var connectionString = $"Host={config["Database:Host"]};" +
    $"Port={config["Database:Port"]};" +
    $"Database={config["Database:Name"]};" +
    $"Username={config["Database:Username"]};" +
    $"Password={config["Database:Password"]};" +
    $"Trust Server Certificate={config["Database:TrustServerCertificate"]};";  // ❌ INSECURE
```

### Vulnerability Analysis

**1. Man-in-the-Middle (MITM) Attack:**

```
Developer/Service  ←──────→  Attacker  ←──────→  PostgreSQL
                  (intercepts)      (forwards)
```

**Attack Scenario:**
1. Attacker positions between application and database (network tap, compromised router, DNS poisoning)
2. Application connects to database
3. **Without certificate validation:**
   - Attacker presents fake certificate
   - Application accepts it (TrustServerCertificate=true)
   - Attacker decrypts, reads, and forwards all traffic
4. **Result:**
   - Database credentials stolen
   - All queries and results intercepted
   - Data can be modified in transit (e.g., change password hashes, inject malicious data)

**2. Current Risk Assessment:**

- **Development:** Low risk (localhost connections, controlled environment)
- **Staging:** Medium risk (network-based database, less controlled)
- **Production:** **CRITICAL** risk (public network, high-value target)

**3. Certificate Validation Modes:**

| SslMode | Certificate Check | Hostname Check | Security Level |
|---------|------------------|----------------|----------------|
| `Disable` | No | No | ❌ None (plaintext) |
| `Allow` | No | No | ❌ Opportunistic TLS, no validation |
| `Prefer` | No | No | ❌ Prefers TLS but no validation |
| `Require` | No | No | ⚠️ Encrypted but no MITM protection |
| `VerifyCA` | Yes | No | ✅ Validates certificate chain |
| `VerifyFull` | Yes | Yes | ✅✅ Full MITM protection (recommended) |

**Current Configuration:**
- `TrustServerCertificate=true` = equivalent to `SslMode=Require` (encrypted but no validation)
- **Recommended:** `SslMode=VerifyFull` (full validation)

---

## 5. Implementation Requirements

### Acceptance Criteria

- [ ] PostgreSQL server certificate generated (development: self-signed, production: CA-signed)
- [ ] PostgreSQL configured to require TLS (`ssl=on`)
- [ ] Connection string updated: `TrustServerCertificate=false`, `SslMode=VerifyFull`
- [ ] Certificate validation working (connections fail with invalid certificate)
- [ ] All services connect successfully with TLS validation
- [ ] Environment-specific configurations (dev/staging/prod)
- [ ] Documentation updated

### Technical Requirements

1. **PostgreSQL Server Certificate:**
   - Development: Self-signed certificate (acceptable for local testing)
   - Staging: Let's Encrypt or internal CA certificate
   - Production: Enterprise CA certificate (e.g., DigiCert, Let's Encrypt)

2. **PostgreSQL Configuration:**
   ```
   ssl = on
   ssl_cert_file = '/etc/ssl/certs/postgresql.crt'
   ssl_key_file = '/etc/ssl/private/postgresql.key'
   ssl_ca_file = '/etc/ssl/certs/ca-bundle.crt'  # Optional: for client cert validation
   ```

3. **Connection String:**
   ```
   Host=localhost;Port=5432;Database=usp_db;Username=usp_user;Password=...;
   SslMode=VerifyFull;Trust Server Certificate=false;
   ```

---

## 6. Step-by-Step Implementation Guide

### Step 1: Generate PostgreSQL Server Certificate (Development) (1 hour)

```bash
cd /home/tshepo/projects/tw/config/postgres

# Create certificate directory
mkdir -p certs
cd certs

# Generate CA private key
openssl genrsa -out ca.key 4096

# Generate CA certificate (valid for 10 years)
openssl req -x509 -new -nodes -key ca.key -sha256 -days 3650 -out ca.crt \
  -subj "/C=US/ST=State/L=City/O=Organization/CN=PostgreSQL CA"

# Generate server private key
openssl genrsa -out server.key 2048

# Generate certificate signing request (CSR)
openssl req -new -key server.key -out server.csr \
  -subj "/C=US/ST=State/L=City/O=Organization/CN=localhost"

# Sign server certificate with CA
openssl x509 -req -in server.csr -CA ca.crt -CAkey ca.key \
  -CAcreateserial -out server.crt -days 365 -sha256

# Set permissions
chmod 600 server.key
chmod 644 server.crt ca.crt

echo "PostgreSQL server certificate generated successfully"
```

### Step 2: Configure PostgreSQL to Use TLS (30 minutes)

**Update `docker-compose.yml` to mount certificates:**

```yaml
services:
  postgres:
    image: postgres:15-alpine
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: ${POSTGRES_SUPERUSER_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./config/postgres/init-scripts:/docker-entrypoint-initdb.d
      - ./config/postgres/certs:/etc/ssl/postgres:ro  # Mount certificates
    command: >
      -c ssl=on
      -c ssl_cert_file=/etc/ssl/postgres/server.crt
      -c ssl_key_file=/etc/ssl/postgres/server.key
      -c ssl_ca_file=/etc/ssl/postgres/ca.crt
```

**Restart PostgreSQL:**
```bash
docker-compose down postgres
docker-compose up -d postgres

# Wait for startup
sleep 10

# Verify TLS enabled
docker-compose exec postgres psql -U postgres -c "SHOW ssl;"
# Expected: ssl | on
```

### Step 3: Update Connection String in appsettings.json (1 hour)

**Create environment-specific configuration files:**

**`appsettings.Development.json`:**
```json
{
  "Database": {
    "Host": "localhost",
    "Port": "5432",
    "Name": "usp_dev",
    "Username": "usp_user",
    "SslMode": "VerifyCA",  // ✅ Validate certificate chain (self-signed CA ok)
    "TrustServerCertificate": false,  // ✅ ENFORCE VALIDATION
    "RootCertificate": "/home/tshepo/projects/tw/config/postgres/certs/ca.crt"
  }
}
```

**`appsettings.Production.json`:**
```json
{
  "Database": {
    "Host": "postgres.production.example.com",
    "Port": "5432",
    "Name": "usp_prod",
    "Username": "usp_user",
    "SslMode": "VerifyFull",  // ✅ Full validation (certificate + hostname)
    "TrustServerCertificate": false,  // ✅ ENFORCE VALIDATION
    "RootCertificate": "/etc/ssl/certs/ca-certificates.crt"  // System CA bundle
  }
}
```

**Update connection string builder:**

```csharp
var builder = new NpgsqlConnectionStringBuilder
{
    Host = configuration["Database:Host"],
    Port = int.Parse(configuration["Database:Port"]),
    Database = configuration["Database:Name"],
    Username = configuration["Database:Username"],
    Password = configuration["Database:Password"],  // From user secrets or environment
    SslMode = Enum.Parse<SslMode>(configuration["Database:SslMode"]),
    TrustServerCertificate = bool.Parse(configuration["Database:TrustServerCertificate"] ?? "false"),
    RootCertificate = configuration["Database:RootCertificate"]  // Path to CA certificate
};

var connectionString = builder.ToString();
```

### Step 4: Test Database Connection with TLS Validation (2 hours)

```bash
cd /home/tshepo/projects/tw/services/usp/src/USP.API

# Configure development environment
export ASPNETCORE_ENVIRONMENT=Development

# Run USP service
dotnet run

# Check logs for successful database connection
# Expected: "Database connection established using TLS (SslMode=VerifyCA)"

# Test 1: Connection should succeed with valid certificate
curl -k https://localhost:5001/health
# Expected: {"status":"Healthy"}

# Test 2: Connection should FAIL with invalid certificate
# (Modify ca.crt to be invalid and restart PostgreSQL)
# Expected: "SSL connection error: Certificate validation failed"
```

**Integration Test:**

```csharp
[Fact]
public async Task DatabaseConnection_UsesTls()
{
    // Arrange
    using var connection = new NpgsqlConnection(connectionString);

    // Act
    await connection.OpenAsync();

    // Assert
    Assert.True(connection.IsSecure);  // Verify TLS used

    using var command = new NpgsqlCommand("SELECT ssl_is_used()", connection);
    var sslUsed = (bool)await command.ExecuteScalarAsync();

    Assert.True(sslUsed);
}
```

### Step 5: Environment-Specific Configuration (1 hour)

**Development (localhost, self-signed):**
- `SslMode=VerifyCA`
- `RootCertificate=/path/to/ca.crt`
- Self-signed CA acceptable

**Staging (cloud, Let's Encrypt):**
- `SslMode=VerifyFull`
- `RootCertificate=/etc/ssl/certs/ca-certificates.crt` (system CA bundle)
- Let's Encrypt certificate from PostgreSQL provider (e.g., AWS RDS, Azure Database)

**Production (cloud, enterprise CA):**
- `SslMode=VerifyFull`
- `RootCertificate=/etc/ssl/certs/ca-certificates.crt`
- Enterprise CA certificate (DigiCert, etc.) from PostgreSQL provider

### Step 6: Commit Changes (30 minutes)

```bash
git add config/postgres/certs/ca.crt
git add config/postgres/certs/server.crt
git add docker-compose.yml
git add services/usp/src/USP.API/appsettings.Development.json
git add services/usp/src/USP.API/appsettings.Production.json

git commit -m "Enable PostgreSQL TLS certificate validation

- Generate self-signed CA and server certificate for development
- Configure PostgreSQL to require TLS (ssl=on)
- Update connection strings: TrustServerCertificate=false, SslMode=VerifyFull
- Create environment-specific configurations (dev vs prod)
- Add integration tests for TLS validation

Resolves: SEC-P0-008 - TrustServerCertificate=true in Production

Security Impact:
- Prevents MITM attacks on database connections
- Validates server identity before sending credentials
- Protects data in transit with certificate validation

🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## 7. Testing Strategy

**Test 1: TLS Connection Successful**
```bash
docker-compose exec postgres psql -U usp_user -d usp_db -c "SELECT ssl_is_used();"
# Expected: t (true)
```

**Test 2: Invalid Certificate Rejected**
```bash
# Rename ca.crt to break validation
mv config/postgres/certs/ca.crt config/postgres/certs/ca.crt.bak
docker-compose restart usp
# Expected: Connection fails with certificate validation error
mv config/postgres/certs/ca.crt.bak config/postgres/certs/ca.crt
```

**Test 3: Application Connects Successfully**
```bash
dotnet test --filter "FullyQualifiedName~DatabaseConnection_UsesTls"
# Expected: PASS
```

---

## 8. Rollback Plan

If TLS validation causes issues:
1. Temporarily set `TrustServerCertificate=true` in development only
2. Debug certificate issues
3. Re-enable validation when fixed

**Never deploy to production with TrustServerCertificate=true**

---

## 9. Monitoring & Validation

**Post-Implementation:**
- [ ] All database connections use TLS
- [ ] Certificate validation enabled
- [ ] No connection errors

**Metrics:**
- `db_ssl_connections` - Gauge (should be > 0)
- `db_ssl_validation_failures` - Counter (target: 0)

---

## 10. Compliance Evidence

**SOC 2 CC6.6:** Database connections encrypted with validated certificates
**HIPAA 164.312(e)(1):** Transmission security via TLS with certificate validation
**PCI-DSS Req 4.1:** Strong cryptography for data in transit

---

## 11. Sign-Off

- [ ] **Developer:** TLS validation working
- [ ] **DBA:** PostgreSQL TLS configured correctly
- [ ] **Security Engineer:** Certificate validation enforced

---

## 12. Appendix

### Related Documentation

- [SEC-P0-003](SEC-P0-003-hardcoded-sql-passwords.md) - Database Passwords
- [PostgreSQL TLS Docs](https://www.postgresql.org/docs/current/ssl-tcp.html)

### Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-12-27 | Security Audit Team | Initial version |

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P0-008 Finding Document**

# Phase 2: TLS & Observability - Week 2 Implementation Guide

**Phase:** 2 of 6
**Duration:** Week 2 (7 days)
**Focus:** P1 High Priority - Pre-Production Requirements
**Team:** Security + Backend + DevOps + SRE
**Deliverable:** All P1 findings resolved, observability stack operational

---

## Overview

Phase 2 addresses all **12 P1 High Priority findings** that should be resolved before production deployment. While not blocking production like P0 findings, these are critical for security, reliability, and operational excellence.

**Dependencies:** Phase 1 (All P0 findings) must be complete before starting Phase 2.

---

## Findings Roadmap

| Day | Finding ID | Title | Hours | Team |
|-----|-----------|-------|-------|------|
| 6 | SEC-P1-010 | Schema Scripts Lack Transaction Wrapping | 2h | Database |
| 6 | SEC-P1-011 | SQL Parameterized Passwords | 3h | Database + Security |
| 7 | SEC-P1-004 | Metrics Endpoint Mapping Broken | 1h | Backend |
| 7 | SEC-P1-005 | Metric Recording Inactive | 2h | Backend |
| 8 | SEC-P1-001 | Metrics Endpoint Over HTTP | 2h | Security + DevOps |
| 8 | SEC-P1-002 | HSTS Middleware Missing | 1h | Security |
| 8 | SEC-P1-003 | Elasticsearch Default HTTP | 1h | DevOps |
| 9-10 | SEC-P1-007 | Observability Stack Missing | 8h | DevOps + SRE |
| 11 | SEC-P1-006 | Distributed Tracing Not Implemented | 4h | Backend |
| 11 | SEC-P1-008 | Secrets Endpoints Lack Granular Authorization | 3h | Security + Backend |
| 12 | SEC-P1-009 | Row-Level Security Not Enabled | 4h | Database + Security |
| 12 | SEC-P1-012 | Certificate Automation Missing | 4h | DevOps |

**Total Effort:** 35 hours (7 days)

---

## Day 6: Database Hardening (5 hours)

### Objective
Secure database migrations and parameterize credentials for production safety.

### Prerequisites
- [ ] Phase 1 complete (all P0 findings resolved)
- [ ] PostgreSQL 16 running with TLS enabled
- [ ] USP Vault unsealed and accessible
- [ ] Database migration scripts in `services/usp/migrations/sql/`

---

### Morning: Transaction Wrapping (2 hours)

**SEC-P1-010: Add BEGIN/COMMIT to Schema Scripts**

**Problem:** Failed DDL statements leave database in inconsistent state.

**Solution:** Wrap all schema scripts in transactions for atomic execution.

#### Implementation Steps

**1. Update UCCP Schema Script (20 minutes)**

```bash
cd services/usp/migrations/sql
```

Edit `04-uccp-schema.sql`:

```sql
-- 04-uccp-schema.sql
BEGIN;  -- ✅ Add transaction wrapper

-- Grant schema access
GRANT USAGE ON SCHEMA uccp TO uccp_user;
GRANT CREATE ON SCHEMA uccp TO uccp_user;

-- 1. Task Definitions Table
CREATE TABLE uccp.task_definitions (
    task_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    task_type VARCHAR(50) NOT NULL CHECK (task_type IN ('train', 'inference', 'batch', 'stream')),
    resource_requirements JSONB,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW()
);

-- 2. Tasks Table
CREATE TABLE uccp.tasks (
    task_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    definition_id UUID REFERENCES uccp.task_definitions(task_id) ON DELETE CASCADE,
    status VARCHAR(50) NOT NULL CHECK (status IN ('pending', 'running', 'completed', 'failed', 'cancelled')),
    worker_id UUID,
    started_at TIMESTAMP,
    completed_at TIMESTAMP,
    error_message TEXT,
    result JSONB,
    created_at TIMESTAMP DEFAULT NOW()
);

-- 3. Indexes
CREATE INDEX idx_tasks_status ON uccp.tasks(status);
CREATE INDEX idx_tasks_worker ON uccp.tasks(worker_id);
CREATE INDEX idx_tasks_created_at ON uccp.tasks(created_at);
CREATE INDEX idx_task_definitions_type ON uccp.task_definitions(task_type);

-- 4. Grant permissions
GRANT SELECT, INSERT, UPDATE, DELETE ON uccp.task_definitions TO uccp_user;
GRANT SELECT, INSERT, UPDATE, DELETE ON uccp.tasks TO uccp_user;

COMMIT;  -- ✅ Commit all changes atomically
```

**2. Update Remaining Schema Scripts (1 hour)**

Apply same pattern to:
- `05-usp-schema.sql`
- `06-nccs-schema.sql`
- `07-udps-schema.sql`
- `08-stream-schema.sql`

Template:
```sql
BEGIN;

-- Schema grants
GRANT USAGE ON SCHEMA <schema_name> TO <user>;

-- Table creations
CREATE TABLE <schema>.<table> (...);

-- Indexes
CREATE INDEX idx_<name> ON <schema>.<table>(<column>);

-- Permissions
GRANT SELECT, INSERT, UPDATE, DELETE ON <schema>.<table> TO <user>;

COMMIT;
```

**3. Test Transaction Rollback (40 minutes)**

Create test script with intentional error:

```bash
cat > test-transaction-rollback.sql <<'EOF'
BEGIN;

CREATE TABLE test_schema.table1 (id INT);
CREATE TABLE test_schema.table2 (id INT);
CREATE TABLE test_schema.SYNTAX ERROR HERE;  -- Intentional failure

COMMIT;
EOF

# Run test
psql -h localhost -U postgres -d postgres -f test-transaction-rollback.sql
# Expected: ERROR at line 5

# Verify rollback worked (no tables created)
psql -h localhost -U postgres -d postgres -c \
  "SELECT tablename FROM pg_tables WHERE schemaname = 'test_schema';"
# Expected: 0 rows (transaction rolled back)

# Cleanup
psql -h localhost -U postgres -d postgres -c "DROP SCHEMA IF EXISTS test_schema CASCADE;"
```

---

### Afternoon: Parameterized Passwords (3 hours)

**SEC-P1-011: Implement Environment Variable Substitution**

**Problem:** No runtime mechanism for injecting secrets into SQL scripts.

**Solution:** Use psql variable substitution with Vault-backed credentials.

#### Implementation Steps

**1. Create Credential Loader Script (1 hour)**

```bash
cat > scripts/db/load-db-credentials.sh <<'EOF'
#!/usr/bin/env bash
set -euo pipefail

echo "🔐 Loading database credentials from USP Vault..."

# Vault configuration
VAULT_ADDR="${VAULT_ADDR:-https://localhost:5001}"
VAULT_TOKEN="${VAULT_TOKEN:?VAULT_TOKEN environment variable required}"

# Function to fetch secret from Vault
fetch_secret() {
    local path=$1
    local field=$2

    curl -s -k -H "Authorization: Bearer $VAULT_TOKEN" \
        "$VAULT_ADDR/api/v1/secrets$path" | jq -r ".data.$field"
}

# Fetch all database user passwords
export USP_DB_PASSWORD=$(fetch_secret "/database/usp_user" "password")
export UCCP_DB_PASSWORD=$(fetch_secret "/database/uccp_user" "password")
export NCCS_DB_PASSWORD=$(fetch_secret "/database/nccs_user" "password")
export UDPS_DB_PASSWORD=$(fetch_secret "/database/udps_user" "password")
export STREAM_DB_PASSWORD=$(fetch_secret "/database/stream_user" "password")

# Verify all credentials loaded
if [[ -z "$USP_DB_PASSWORD" ]] || [[ -z "$UCCP_DB_PASSWORD" ]]; then
    echo "❌ Failed to load database credentials from Vault"
    exit 1
fi

echo "✅ All database credentials loaded successfully"
echo "   - USP user password: ${USP_DB_PASSWORD:0:4}***"
echo "   - UCCP user password: ${UCCP_DB_PASSWORD:0:4}***"
echo "   - NCCS user password: ${NCCS_DB_PASSWORD:0:4}***"
echo "   - UDPS user password: ${UDPS_DB_PASSWORD:0:4}***"
echo "   - Stream user password: ${STREAM_DB_PASSWORD:0:4}***"
EOF

chmod +x scripts/db/load-db-credentials.sh
```

**2. Update Migration Script (1 hour)**

```bash
cat > scripts/db/apply-migrations.sh <<'EOF'
#!/usr/bin/env bash
set -euo pipefail

# Configuration
MIGRATION_DIR="services/usp/migrations/sql"
POSTGRES_HOST="${POSTGRES_HOST:-localhost}"
POSTGRES_PORT="${POSTGRES_PORT:-5432}"
POSTGRES_ADMIN_USER="${POSTGRES_ADMIN_USER:-postgres}"

echo "🗄️  Applying database migrations..."

# Load credentials from Vault
source scripts/db/load-db-credentials.sh

# Function to run SQL file with psql variable substitution
run_sql() {
    local file=$1
    echo "  📄 Applying $file..."

    psql -h "$POSTGRES_HOST" -p "$POSTGRES_PORT" -U "$POSTGRES_ADMIN_USER" -d postgres \
        -v USP_DB_PASSWORD="$USP_DB_PASSWORD" \
        -v UCCP_DB_PASSWORD="$UCCP_DB_PASSWORD" \
        -v NCCS_DB_PASSWORD="$NCCS_DB_PASSWORD" \
        -v UDPS_DB_PASSWORD="$UDPS_DB_PASSWORD" \
        -v STREAM_DB_PASSWORD="$STREAM_DB_PASSWORD" \
        -f "$file"
}

# Apply migrations in order
echo "📦 Step 1: Create databases..."
run_sql "$MIGRATION_DIR/01-create-databases.sql"

echo "👥 Step 2: Create roles with parameterized passwords..."
run_sql "$MIGRATION_DIR/02-create-roles.sql"

echo "📐 Step 3: Create schemas..."
run_sql "$MIGRATION_DIR/03-create-schemas.sql"

echo "🏗️  Step 4-8: Create tables and indexes..."
for schema_file in "$MIGRATION_DIR"/0[4-8]-*.sql; do
    run_sql "$schema_file"
done

echo "✅ All migrations applied successfully"
EOF

chmod +x scripts/db/apply-migrations.sh
```

**3. Test Parameterized Execution (1 hour)**

```bash
# Store test passwords in Vault
export VAULT_TOKEN="<your-root-token>"

# Create test secrets
curl -k -X POST https://localhost:5001/api/v1/secrets \
  -H "Authorization: Bearer $VAULT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "path": "/database/usp_user",
    "data": {
      "password": "'"$(openssl rand -base64 32)"'"
    }
  }'

# Repeat for all 5 database users (uccp_user, nccs_user, udps_user, stream_user)

# Test migration script
bash scripts/db/apply-migrations.sh

# Verify users can login with new passwords
echo "Testing USP user login..."
PGPASSWORD="$USP_DB_PASSWORD" psql -h localhost -U usp_user -d postgres -c "SELECT 1;"
# Expected: Connection successful, returns 1

echo "Testing UCCP user login..."
PGPASSWORD="$UCCP_DB_PASSWORD" psql -h localhost -U uccp_user -d postgres -c "SELECT 1;"
# Expected: Connection successful
```

### Deliverable (Day 6)
- [ ] All 5 schema scripts wrapped in BEGIN/COMMIT transactions
- [ ] Transaction rollback verified with test script
- [ ] `load-db-credentials.sh` fetches passwords from Vault
- [ ] `apply-migrations.sh` uses parameterized passwords
- [ ] All database users can authenticate with Vault-stored passwords
- [ ] No hardcoded passwords in any SQL files

---

## Day 7: Metrics Foundation (3 hours)

### Objective
Enable Prometheus metrics endpoint and activate metric recording.

### Prerequisites
- [ ] USP service running on ports 5001 (HTTPS) and 9091 (metrics)
- [ ] Vault operational with secrets configured

---

### Morning: Fix MapMetrics Endpoint (1 hour)

**SEC-P1-004: Enable Prometheus Metrics Endpoint**

**Problem:** `app.MapMetrics("/metrics")` is disabled due to missing prometheus-net package.

**Solution:** Install prometheus-net.AspNetCore and enable metrics.

#### Implementation Steps

**1. Install prometheus-net Package (10 minutes)**

```bash
cd services/usp/src/USP.API

dotnet add package prometheus-net.AspNetCore --version 8.2.1
```

**2. Enable Metrics in Program.cs (20 minutes)**

Edit `src/USP.API/Program.cs`:

```csharp
// Add at top of file
using Prometheus;

// ... existing builder configuration ...

// After app.Build()
var app = builder.Build();

// Enable HTTP request metrics middleware
app.UseHttpMetrics();  // ✅ Track HTTP request duration, status codes

// Map metrics endpoint
app.MapMetrics("/metrics");  // ✅ Expose Prometheus-compatible endpoint

// ... rest of middleware pipeline ...

app.Run();
```

**3. Verify Metrics Endpoint (30 minutes)**

```bash
# Rebuild and run
dotnet build
dotnet run --project src/USP.API

# Test metrics endpoint (currently HTTP - will fix in afternoon)
curl http://localhost:9091/metrics

# Expected output (Prometheus format):
# HELP http_requests_received_total Total HTTP requests
# TYPE http_requests_received_total counter
# http_requests_received_total{code="200",method="GET",controller="Health",action="GetHealth"} 5
#
# HELP http_request_duration_seconds HTTP request duration
# TYPE http_request_duration_seconds histogram
# http_request_duration_seconds_bucket{code="200",method="GET",le="0.005"} 3
# http_request_duration_seconds_bucket{code="200",method="GET",le="0.01"} 5
```

---

### Afternoon: Activate Metric Recording (2 hours)

**SEC-P1-005: Enable Security Metrics Recording**

**Problem:** `SecurityMetrics` class defined but `RecordLoginAttempt` never called.

**Solution:** Inject metrics into services and record security events.

#### Implementation Steps

**1. Register SecurityMetrics in DI (15 minutes)**

Edit `src/USP.API/Program.cs`:

```csharp
// Register security metrics as singleton
builder.Services.AddSingleton<SecurityMetrics>();
```

**2. Inject into AuthenticationService (30 minutes)**

Edit `src/USP.Core/Services/AuthenticationService.cs`:

```csharp
public class AuthenticationService : IAuthenticationService
{
    private readonly IUserRepository _userRepository;
    private readonly ISessionService _sessionService;
    private readonly SecurityMetrics _securityMetrics;  // ✅ Add field

    public AuthenticationService(
        IUserRepository userRepository,
        ISessionService sessionService,
        SecurityMetrics securityMetrics)  // ✅ Inject
    {
        _userRepository = userRepository;
        _sessionService = sessionService;
        _securityMetrics = securityMetrics;
    }

    public async Task<AuthenticationResult> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.GetByUsernameAsync(request.Username);

        if (user == null)
        {
            // ✅ Record failed login attempt
            _securityMetrics.RecordLoginAttempt(
                username: request.Username,
                success: false,
                mfaUsed: false
            );

            return AuthenticationResult.Failed("Invalid username or password");
        }

        // Verify password
        var passwordValid = VerifyPassword(request.Password, user.PasswordHash);

        if (!passwordValid)
        {
            user.FailedLoginAttempts++;
            await _userRepository.UpdateAsync(user);

            // ✅ Record failed login
            _securityMetrics.RecordLoginAttempt(
                username: request.Username,
                success: false,
                mfaUsed: false
            );

            return AuthenticationResult.Failed("Invalid username or password");
        }

        // Check MFA requirement
        var mfaRequired = user.MfaEnabled;
        var mfaValid = !mfaRequired || await VerifyMfaAsync(user.UserId, request.MfaCode);

        if (mfaRequired && !mfaValid)
        {
            _securityMetrics.RecordLoginAttempt(
                username: request.Username,
                success: false,
                mfaUsed: true
            );

            return AuthenticationResult.Failed("Invalid MFA code");
        }

        // Success
        user.FailedLoginAttempts = 0;
        user.LastLoginAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        var session = await _sessionService.CreateSessionAsync(user.UserId);

        // ✅ Record successful login
        _securityMetrics.RecordLoginAttempt(
            username: request.Username,
            success: true,
            mfaUsed: mfaRequired
        );

        return AuthenticationResult.Success(session.Token);
    }
}
```

**3. Add Metrics to Other Security Events (1 hour)**

Add metrics to:

**EncryptionService.cs:**
```csharp
_securityMetrics.RecordEncryption(algorithm: "AES-256-GCM", sizeBytes: plaintext.Length);
_securityMetrics.RecordDecryption(algorithm: "AES-256-GCM", sizeBytes: ciphertext.Length);
```

**VaultService.cs:**
```csharp
_securityMetrics.RecordVaultOperation(operation: "seal");
_securityMetrics.RecordVaultOperation(operation: "unseal");
_securityMetrics.RecordSecretAccess(secretPath: path, operation: "read");
_securityMetrics.RecordSecretAccess(secretPath: path, operation: "write");
```

**AuthorizationService.cs:**
```csharp
_securityMetrics.RecordAuthorizationCheck(
    userId: userId,
    resource: resource,
    action: action,
    granted: hasPermission
);
```

**4. Verify Metrics (15 minutes)**

```bash
# Restart service
dotnet run --project src/USP.API

# Generate some login attempts
curl -k -X POST https://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"wrongpassword"}'

curl -k -X POST https://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"correctpassword"}'

# Check metrics endpoint
curl http://localhost:9091/metrics | grep usp_login_attempts

# Expected output:
# usp_login_attempts_total{username="admin",success="false",mfa_used="false"} 1
# usp_login_attempts_total{username="admin",success="true",mfa_used="false"} 1
```

### Deliverable (Day 7)
- [ ] prometheus-net.AspNetCore package installed
- [ ] `app.MapMetrics("/metrics")` enabled
- [ ] Metrics endpoint returns Prometheus-compatible output
- [ ] SecurityMetrics injected into all security services
- [ ] Login attempts, encryption/decryption, vault operations recorded
- [ ] Metrics visible at `/metrics` endpoint

---

## Day 8: HTTPS & TLS Hardening (4 hours)

### Objective
Enforce HTTPS for all services and enable HSTS protection.

### Prerequisites
- [ ] TLS certificates configured (from Phase 1, Day 4)
- [ ] Services running with HTTPS enabled

---

### Morning: Metrics Endpoint HTTPS (2 hours)

**SEC-P1-001: Move Metrics Endpoint to HTTPS**

**Problem:** Prometheus metrics exposed over unencrypted HTTP on port 9091.

**Solution:** Configure Kestrel to serve metrics over HTTPS with dedicated certificate.

#### Implementation Steps

**1. Generate Metrics Endpoint Certificate (30 minutes)**

```bash
cd config/certs

# Generate metrics certificate
openssl req -new -x509 -days 365 -nodes \
  -out metrics.crt -keyout metrics.key \
  -subj "/CN=usp-metrics.local/O=TW/C=US"

# Convert to PFX for .NET
openssl pkcs12 -export -out metrics.pfx \
  -inkey metrics.key -in metrics.crt \
  -passout pass:$(openssl rand -base64 32)

# Store password in Vault
METRICS_CERT_PASSWORD=$(openssl rand -base64 32)

curl -k -X POST https://localhost:5001/api/v1/secrets \
  -H "Authorization: Bearer $VAULT_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"path\": \"/certificates/metrics\",
    \"data\": {
      \"password\": \"$METRICS_CERT_PASSWORD\",
      \"path\": \"/app/certs/metrics.pfx\"
    }
  }"
```

**2. Configure HTTPS for Metrics Port (1 hour)**

Edit `src/USP.API/Program.cs`:

```csharp
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel for dual HTTPS endpoints
builder.WebHost.ConfigureKestrel(options =>
{
    // Main API endpoint - HTTPS 5001
    options.Listen(IPAddress.Any, 5001, listenOptions =>
    {
        listenOptions.UseHttps(httpsOptions =>
        {
            httpsOptions.ServerCertificate = LoadApiCertificate();
        });
    });

    // Metrics endpoint - HTTPS 9091 ✅
    options.Listen(IPAddress.Any, 9091, listenOptions =>
    {
        listenOptions.UseHttps(httpsOptions =>
        {
            httpsOptions.ServerCertificate = LoadMetricsCertificate();  // ✅ Separate cert
        });
    });
});

// Certificate loading functions
X509Certificate2 LoadApiCertificate()
{
    var certPath = builder.Configuration["Certificate:Path"];
    var certPassword = builder.Configuration["Certificate:Password"];
    return new X509Certificate2(certPath, certPassword);
}

X509Certificate2 LoadMetricsCertificate()
{
    // Fetch from Vault
    var vaultClient = new VaultClient(
        builder.Configuration["Vault:Url"],
        Environment.GetEnvironmentVariable("VAULT_TOKEN")
    );

    var secret = vaultClient.GetSecretAsync("/certificates/metrics").Result;
    var certPath = secret.Data["path"].ToString();
    var certPassword = secret.Data["password"].ToString();

    return new X509Certificate2(certPath, certPassword);
}
```

**3. Update Prometheus Scrape Config (30 minutes)**

Edit `config/prometheus/prometheus.yml`:

```yaml
scrape_configs:
  - job_name: 'usp-metrics'
    scheme: https  # ✅ Changed from http
    tls_config:
      insecure_skip_verify: true  # For self-signed certs (dev only)
      # In production, use:
      # ca_file: /etc/prometheus/certs/ca.crt
    static_configs:
      - targets: ['usp:9091']  # ✅ HTTPS port
```

---

### Afternoon: HSTS & Default HTTPS (2 hours)

**SEC-P1-002: Enable HSTS Middleware**

**Problem:** No HSTS headers to prevent downgrade attacks.

**Solution:** Configure HSTS with 1-year max age and preload.

#### Implementation Steps

**1. Configure HSTS in Program.cs (30 minutes)**

Edit `src/USP.API/Program.cs`:

```csharp
// Add HSTS configuration
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);  // 1 year
    options.IncludeSubDomains = true;
    options.Preload = true;  // Enable HSTS preload list
});

var app = builder.Build();

// Apply HSTS in production
if (app.Environment.IsProduction())
{
    app.UseHsts();  // ✅ Adds Strict-Transport-Security header
}

// Force HTTPS redirection
app.UseHttpsRedirection();  // ✅ Redirect HTTP → HTTPS
```

**2. Test HSTS Headers (15 minutes)**

```bash
# Restart service
dotnet run --project src/USP.API

# Test HSTS header
curl -k -I https://localhost:5001/health

# Expected response headers:
# HTTP/1.1 200 OK
# Strict-Transport-Security: max-age=31536000; includeSubDomains; preload
```

**SEC-P1-003: Fix Elasticsearch Default HTTP URL**

**Problem:** `ObservabilityOptions.ElasticsearchUrl` defaults to `http://elasticsearch:9200`.

**Solution:** Change default to HTTPS.

**3. Update Default Configuration (30 minutes)**

Edit `src/USP.Core/Configuration/ObservabilityOptions.cs`:

```csharp
public class ObservabilityOptions
{
    /// <summary>
    /// Elasticsearch endpoint URL.
    /// </summary>
    public string ElasticsearchUrl { get; set; } = "https://elasticsearch:9200";  // ✅ HTTPS default

    /// <summary>
    /// Prometheus metrics endpoint URL.
    /// </summary>
    public string PrometheusUrl { get; set; } = "https://prometheus:9090";  // ✅ HTTPS

    /// <summary>
    /// Jaeger tracing endpoint URL.
    /// </summary>
    public string JaegerUrl { get; set; } = "https://jaeger:14268";  // ✅ HTTPS
}
```

**4. Update appsettings.json (15 minutes)**

Edit `src/USP.API/appsettings.json`:

```json
{
  "Observability": {
    "ElasticsearchUrl": "https://elasticsearch:9200",
    "PrometheusUrl": "https://prometheus:9090",
    "JaegerUrl": "https://jaeger:14268"
  }
}
```

**5. Verify Configuration (30 minutes)**

```bash
# Test configuration loading
dotnet run --project src/USP.API

# Check logs for HTTPS URLs
# Expected: "Elasticsearch URL: https://elasticsearch:9200"
```

### Deliverable (Day 8)
- [ ] Metrics endpoint certificate generated and stored in Vault
- [ ] Metrics endpoint serving HTTPS on port 9091
- [ ] Prometheus configured to scrape HTTPS endpoint
- [ ] HSTS middleware enabled with 1-year max-age
- [ ] HTTPS redirection enabled
- [ ] All observability URLs default to HTTPS
- [ ] HSTS headers present in API responses

---

## Day 9-10: Observability Stack Deployment (8 hours)

### Objective
Deploy complete observability stack: Prometheus, Grafana, Jaeger, Elasticsearch.

### Prerequisites
- [ ] Kubernetes cluster operational
- [ ] Helm 3 installed
- [ ] kubectl configured

---

**SEC-P1-007: Deploy Observability Stack**

**Problem:** No Prometheus, Grafana, Jaeger, or Elasticsearch deployed for monitoring.

**Solution:** Deploy via Helm charts with TLS and authentication.

### Day 9 Morning: Prometheus & Grafana (4 hours)

#### Implementation Steps

**1. Install kube-prometheus-stack (2 hours)**

```bash
# Add Helm repository
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm repo update

# Create namespace
kubectl create namespace monitoring

# Create values file for customization
cat > prometheus-values.yaml <<'EOF'
prometheus:
  prometheusSpec:
    retention: 30d
    storageSpec:
      volumeClaimTemplate:
        spec:
          accessModes: ["ReadWriteOnce"]
          resources:
            requests:
              storage: 50Gi

    # Enable TLS
    web:
      tlsConfig:
        cert:
          secret:
            name: prometheus-tls
            key: tls.crt
        keySecret:
          name: prometheus-tls
          key: tls.key

    # Scrape configurations
    additionalScrapeConfigs:
      - job_name: 'usp-api'
        scheme: https
        tls_config:
          insecure_skip_verify: true
        static_configs:
          - targets: ['usp.default.svc.cluster.local:9091']

      - job_name: 'nccs-api'
        scheme: https
        static_configs:
          - targets: ['nccs.default.svc.cluster.local:9200']

      - job_name: 'uccp'
        scheme: https
        static_configs:
          - targets: ['uccp.default.svc.cluster.local:9100']

grafana:
  enabled: true
  adminPassword: <change-me>  # Store in Vault

  ingress:
    enabled: true
    hosts:
      - grafana.tw.local
    tls:
      - secretName: grafana-tls
        hosts:
          - grafana.tw.local

  datasources:
    datasources.yaml:
      apiVersion: 1
      datasources:
        - name: Prometheus
          type: prometheus
          url: http://prometheus-kube-prometheus-prometheus:9090
          isDefault: true

        - name: Jaeger
          type: jaeger
          url: http://jaeger-query:16686

        - name: Elasticsearch
          type: elasticsearch
          url: https://elasticsearch-master:9200
          basicAuth: true
          basicAuthUser: elastic
          secureJsonData:
            basicAuthPassword: <change-me>

alertmanager:
  enabled: true
  config:
    global:
      slack_api_url: '<slack-webhook-url>'

    route:
      receiver: 'slack-notifications'
      group_by: ['alertname', 'cluster', 'service']
      group_wait: 10s
      group_interval: 10s
      repeat_interval: 12h

    receivers:
      - name: 'slack-notifications'
        slack_configs:
          - channel: '#alerts'
            title: '{{ .GroupLabels.alertname }}'
            text: '{{ range .Alerts }}{{ .Annotations.description }}{{ end }}'
EOF

# Install kube-prometheus-stack
helm install prometheus prometheus-community/kube-prometheus-stack \
  --namespace monitoring \
  --values prometheus-values.yaml

# Wait for deployment
kubectl wait --for=condition=ready pod -l app.kubernetes.io/name=prometheus \
  -n monitoring --timeout=300s
```

**2. Verify Prometheus Deployment (30 minutes)**

```bash
# Port-forward to Prometheus UI
kubectl port-forward -n monitoring svc/prometheus-kube-prometheus-prometheus 9090:9090

# Open browser: http://localhost:9090
# Navigate to Status > Targets
# Expected: All scrape targets (usp, nccs, uccp) visible and UP

# Query test
# Expression: up{job="usp-api"}
# Expected: Value 1 (service is up)
```

**3. Configure Grafana Dashboards (1.5 hours)**

```bash
# Port-forward to Grafana
kubectl port-forward -n monitoring svc/prometheus-grafana 3000:80

# Login: http://localhost:3000
# Username: admin
# Password: <from prometheus-values.yaml>

# Import USP dashboard
cat > dashboards/usp-overview.json <<'EOF'
{
  "dashboard": {
    "title": "USP - Unified Security Platform Overview",
    "panels": [
      {
        "title": "Login Attempts (Success vs Failure)",
        "targets": [
          {
            "expr": "rate(usp_login_attempts_total{success=\"true\"}[5m])",
            "legendFormat": "Successful"
          },
          {
            "expr": "rate(usp_login_attempts_total{success=\"false\"}[5m])",
            "legendFormat": "Failed"
          }
        ],
        "type": "graph"
      },
      {
        "title": "Encryption Operations",
        "targets": [
          {
            "expr": "rate(usp_encryption_operations_total[5m])",
            "legendFormat": "{{algorithm}}"
          }
        ],
        "type": "graph"
      },
      {
        "title": "Secret Access by Path",
        "targets": [
          {
            "expr": "rate(usp_secret_access_total[5m])",
            "legendFormat": "{{secret_path}} - {{operation}}"
          }
        ],
        "type": "graph"
      },
      {
        "title": "API Request Duration (p95)",
        "targets": [
          {
            "expr": "histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))",
            "legendFormat": "{{method}} {{controller}}/{{action}}"
          }
        ],
        "type": "graph"
      }
    ]
  }
}
EOF

# Import via Grafana UI or API
curl -X POST http://localhost:3000/api/dashboards/db \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <grafana-api-key>" \
  -d @dashboards/usp-overview.json
```

### Day 9 Afternoon: Jaeger (2 hours)

**4. Deploy Jaeger Operator (1 hour)**

```bash
# Install Jaeger Operator
kubectl create namespace observability
kubectl create -f https://github.com/jaegertracing/jaeger-operator/releases/download/v1.51.0/jaeger-operator.yaml -n observability

# Create Jaeger instance
cat > jaeger-instance.yaml <<'EOF'
apiVersion: jaegertracing.io/v1
kind: Jaeger
metadata:
  name: jaeger
  namespace: observability
spec:
  strategy: production

  storage:
    type: elasticsearch
    options:
      es:
        server-urls: https://elasticsearch-master:9200
        tls:
          ca: /es/certificates/ca.crt
    secretName: elasticsearch-credentials

  ingress:
    enabled: true
    hosts:
      - jaeger.tw.local
    tls:
      - secretName: jaeger-tls
        hosts:
          - jaeger.tw.local

  collector:
    maxReplicas: 5
    resources:
      limits:
        cpu: 1
        memory: 1Gi

  query:
    resources:
      limits:
        cpu: 500m
        memory: 512Mi
EOF

kubectl apply -f jaeger-instance.yaml

# Wait for Jaeger to be ready
kubectl wait --for=condition=ready pod -l app.kubernetes.io/name=jaeger \
  -n observability --timeout=300s
```

**5. Verify Jaeger UI (1 hour)**

```bash
# Port-forward to Jaeger Query UI
kubectl port-forward -n observability svc/jaeger-query 16686:16686

# Open browser: http://localhost:16686

# Generate test traces by making API calls
curl -k -X POST https://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"test"}'

# In Jaeger UI:
# 1. Select service: "USP"
# 2. Click "Find Traces"
# Expected: Login request trace with spans for AuthenticationService, VaultService
```

### Day 10: Elasticsearch (2 hours)

**6. Deploy Elasticsearch with ECK (1.5 hours)**

```bash
# Install Elastic Cloud on Kubernetes (ECK) Operator
kubectl create -f https://download.elastic.co/downloads/eck/2.10.0/crds.yaml
kubectl apply -f https://download.elastic.co/downloads/eck/2.10.0/operator.yaml

# Create Elasticsearch cluster
cat > elasticsearch.yaml <<'EOF'
apiVersion: elasticsearch.k8s.elastic.co/v1
kind: Elasticsearch
metadata:
  name: elasticsearch
  namespace: observability
spec:
  version: 8.11.0

  nodeSets:
    - name: master
      count: 3
      config:
        node.roles: ["master"]
      volumeClaimTemplates:
        - metadata:
            name: elasticsearch-data
          spec:
            accessModes: ["ReadWriteOnce"]
            resources:
              requests:
                storage: 50Gi

    - name: data
      count: 3
      config:
        node.roles: ["data", "ingest"]
      volumeClaimTemplates:
        - metadata:
            name: elasticsearch-data
          spec:
            accessModes: ["ReadWriteOnce"]
            resources:
              requests:
                storage: 100Gi

  http:
    tls:
      selfSignedCertificate:
        disabled: false
EOF

kubectl apply -f elasticsearch.yaml

# Wait for Elasticsearch to be ready
kubectl wait --for=condition=ready pod -l elasticsearch.k8s.elastic.co/cluster-name=elasticsearch \
  -n observability --timeout=600s

# Get elastic user password
ELASTIC_PASSWORD=$(kubectl get secret elasticsearch-es-elastic-user \
  -n observability -o go-template='{{.data.elastic | base64decode}}')

echo "Elasticsearch password: $ELASTIC_PASSWORD"
```

**7. Verify Elasticsearch (30 minutes)**

```bash
# Port-forward to Elasticsearch
kubectl port-forward -n observability svc/elasticsearch-es-http 9200:9200

# Test connection
curl -k -u "elastic:$ELASTIC_PASSWORD" https://localhost:9200

# Expected output:
# {
#   "name" : "elasticsearch-es-master-0",
#   "cluster_name" : "elasticsearch",
#   "version" : {
#     "number" : "8.11.0"
#   }
# }

# Create index for USP logs
curl -k -u "elastic:$ELASTIC_PASSWORD" -X PUT https://localhost:9200/usp-logs \
  -H "Content-Type: application/json" \
  -d '{
    "settings": {
      "number_of_shards": 3,
      "number_of_replicas": 2
    },
    "mappings": {
      "properties": {
        "timestamp": {"type": "date"},
        "level": {"type": "keyword"},
        "message": {"type": "text"},
        "correlationId": {"type": "keyword"},
        "userId": {"type": "keyword"},
        "action": {"type": "keyword"}
      }
    }
  }'
```

### Deliverable (Day 9-10)
- [ ] Prometheus deployed with 30-day retention
- [ ] Grafana deployed with USP dashboard
- [ ] Alertmanager configured with Slack integration
- [ ] Jaeger Operator deployed
- [ ] Jaeger instance running with Elasticsearch storage
- [ ] Elasticsearch cluster (3 master + 3 data nodes) operational
- [ ] All services scraping metrics successfully
- [ ] Distributed traces visible in Jaeger UI
- [ ] Logs indexed in Elasticsearch

---

## Day 11: Distributed Tracing & Authorization (7 hours)

### Objective
Implement OpenTelemetry tracing and granular authorization.

### Prerequisites
- [ ] Jaeger deployed and operational (from Day 9-10)
- [ ] USP service running with metrics enabled

---

### Morning: OpenTelemetry Integration (4 hours)

**SEC-P1-006: Implement Distributed Tracing**

**Problem:** No distributed tracing across services for debugging and performance analysis.

**Solution:** Integrate OpenTelemetry with Jaeger exporter.

#### Implementation Steps

**1. Install OpenTelemetry Packages (15 minutes)**

```bash
cd services/usp/src/USP.API

dotnet add package OpenTelemetry --version 1.6.0
dotnet add package OpenTelemetry.Extensions.Hosting --version 1.6.0
dotnet add package OpenTelemetry.Instrumentation.AspNetCore --version 1.5.1-beta.1
dotnet add package OpenTelemetry.Instrumentation.Http --version 1.5.1-beta.1
dotnet add package OpenTelemetry.Instrumentation.EntityFrameworkCore --version 1.0.0-beta.8
dotnet add package OpenTelemetry.Exporter.Jaeger --version 1.5.1
```

**2. Configure OpenTelemetry in Program.cs (2 hours)**

Edit `src/USP.API/Program.cs`:

```csharp
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Configure OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: "USP",
            serviceVersion: "1.0.0",
            serviceInstanceId: Environment.MachineName
        )
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName,
            ["service.namespace"] = "tw-platform"
        }))
    .WithTracing(tracing => tracing
        // Instrumentation
        .AddAspNetCoreInstrumentation(options =>
        {
            options.RecordException = true;
            options.Filter = (httpContext) =>
            {
                // Exclude health checks from traces
                return !httpContext.Request.Path.StartsWithSegments("/health");
            };
            options.EnrichWithHttpRequest = (activity, httpRequest) =>
            {
                activity.SetTag("http.client_ip", httpRequest.HttpContext.Connection.RemoteIpAddress?.ToString());
                activity.SetTag("http.user_agent", httpRequest.Headers["User-Agent"].ToString());
            };
            options.EnrichWithHttpResponse = (activity, httpResponse) =>
            {
                activity.SetTag("http.response_content_length", httpResponse.ContentLength);
            };
        })
        .AddHttpClientInstrumentation(options =>
        {
            options.RecordException = true;
            options.EnrichWithHttpRequestMessage = (activity, httpRequestMessage) =>
            {
                activity.SetTag("http.request.method", httpRequestMessage.Method.Method);
            };
            options.EnrichWithHttpResponseMessage = (activity, httpResponseMessage) =>
            {
                activity.SetTag("http.response.status_code", (int)httpResponseMessage.StatusCode);
            };
        })
        .AddEntityFrameworkCoreInstrumentation(options =>
        {
            options.SetDbStatementForText = true;
            options.SetDbStatementForStoredProcedure = true;
        })

        // Custom activity sources
        .AddSource("USP.VaultService")
        .AddSource("USP.EncryptionService")
        .AddSource("USP.AuthenticationService")

        // Jaeger exporter
        .AddJaegerExporter(options =>
        {
            var jaegerUrl = builder.Configuration["Observability:JaegerUrl"] ?? "http://jaeger-collector:14268";
            options.AgentHost = new Uri(jaegerUrl).Host;
            options.AgentPort = 6831;
            options.MaxPayloadSizeInBytes = 4096;
        }));
```

**3. Add Manual Instrumentation to Services (1.5 hours)**

Edit `src/USP.Core/Services/VaultService.cs`:

```csharp
using System.Diagnostics;

public class VaultService : IVaultService
{
    private static readonly ActivitySource ActivitySource = new("USP.VaultService");

    public async Task<SecretResponse> GetSecretAsync(string path)
    {
        using var activity = ActivitySource.StartActivity("GetSecret");
        activity?.SetTag("secret.path", path);
        activity?.SetTag("operation.type", "read");

        try
        {
            var secret = await _repository.GetSecretAsync(path);

            if (secret == null)
            {
                activity?.SetTag("secret.found", false);
                activity?.SetStatus(ActivityStatusCode.Error, "Secret not found");
                throw new NotFoundException($"Secret not found: {path}");
            }

            activity?.SetTag("secret.found", true);
            activity?.SetTag("secret.version", secret.Version);

            return new SecretResponse
            {
                Path = secret.Path,
                Data = DecryptSecretData(secret.EncryptedData),
                Version = secret.Version,
                CreatedAt = secret.CreatedAt
            };
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
            throw;
        }
    }

    public async Task SealAsync()
    {
        using var activity = ActivitySource.StartActivity("SealVault");
        activity?.SetTag("vault.operation", "seal");

        try
        {
            await _encryptionService.EncryptMasterKeyAsync();
            _sealStatus.Sealed = true;
            _sealStatus.UnsealProgress = 0;

            activity?.SetTag("vault.sealed", true);
            activity?.AddEvent(new ActivityEvent("Vault sealed successfully"));
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
            throw;
        }
    }
}
```

Apply similar instrumentation to:
- `AuthenticationService` (login traces)
- `EncryptionService` (encryption operation spans)
- `AuthorizationService` (permission check spans)

**4. Verify Distributed Tracing (30 minutes)**

```bash
# Rebuild and run
dotnet build
dotnet run --project src/USP.API

# Generate trace with multiple spans
curl -k -X POST https://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}'

# Open Jaeger UI
kubectl port-forward -n observability svc/jaeger-query 16686:16686

# In browser: http://localhost:16686
# 1. Select service: "USP"
# 2. Select operation: "POST /api/v1/auth/login"
# 3. Click "Find Traces"

# Expected trace structure:
# └─ POST /api/v1/auth/login (ASP.NET Core)
#    ├─ AuthenticationService.LoginAsync
#    │  ├─ UserRepository.GetByUsernameAsync
#    │  │  └─ PostgreSQL SELECT (EF Core)
#    │  ├─ VaultService.GetSecret (for password hash)
#    │  │  └─ PostgreSQL SELECT
#    │  └─ SessionService.CreateSession
#    │     └─ Redis SET
#    └─ HTTP 200 OK
```

---

### Afternoon: Granular Authorization (3 hours)

**SEC-P1-008: Implement RequirePermission Attributes**

**Problem:** Secrets endpoints use generic `[Authorize]` - any authenticated user can access all secrets.

**Solution:** Replace with granular `[RequirePermission]` attributes.

#### Implementation Steps

**1. Update SecretsController (1 hour)**

Edit `src/USP.API/Controllers/SecretsController.cs`:

```csharp
[ApiController]
[Route("api/v1/secrets")]
public class SecretsController : ControllerBase
{
    private readonly IVaultService _vaultService;

    [HttpGet]
    [RequirePermission("secrets", "list")]  // ✅ Granular permission
    public async Task<IActionResult> ListSecrets(
        [FromQuery] string? namespacePath = null)
    {
        var secrets = await _vaultService.ListSecretsAsync(namespacePath);
        return Ok(secrets);
    }

    [HttpGet("{path}")]
    [RequirePermission("secrets", "read")]  // ✅ Read permission
    public async Task<IActionResult> GetSecret(string path)
    {
        using var activity = Activity.Current;
        activity?.SetTag("secret.path", path);

        var secret = await _vaultService.GetSecretAsync(path);
        return Ok(secret);
    }

    [HttpPost]
    [RequirePermission("secrets", "write")]  // ✅ Write permission
    public async Task<IActionResult> CreateSecret([FromBody] CreateSecretRequest request)
    {
        var secretId = await _vaultService.CreateSecretAsync(
            request.Path,
            request.Data
        );

        return CreatedAtAction(
            nameof(GetSecret),
            new { path = request.Path },
            new { id = secretId }
        );
    }

    [HttpPut("{path}")]
    [RequirePermission("secrets", "write")]
    public async Task<IActionResult> UpdateSecret(
        string path,
        [FromBody] UpdateSecretRequest request)
    {
        await _vaultService.UpdateSecretAsync(path, request.Data);
        return NoContent();
    }

    [HttpDelete("{path}")]
    [RequirePermission("secrets", "delete")]  // ✅ Delete permission
    public async Task<IActionResult> DeleteSecret(string path)
    {
        await _vaultService.DeleteSecretAsync(path);
        return NoContent();
    }
}
```

**2. Create Test Users with Limited Permissions (1 hour)**

```bash
# Create read-only user
curl -k -X POST https://localhost:5001/api/v1/users \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "username": "readonly_user",
    "email": "readonly@tw.local",
    "password": "ReadOnly123!",
    "roles": ["secrets-reader"]
  }'

# Create secrets-reader role with limited permissions
curl -k -X POST https://localhost:5001/api/v1/roles \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "secrets-reader",
    "permissions": [
      {"resource": "secrets", "action": "read"},
      {"resource": "secrets", "action": "list"}
    ]
  }'

# Create admin user with full permissions
curl -k -X POST https://localhost:5001/api/v1/roles \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "secrets-admin",
    "permissions": [
      {"resource": "secrets", "action": "read"},
      {"resource": "secrets", "action": "list"},
      {"resource": "secrets", "action": "write"},
      {"resource": "secrets", "action": "delete"}
    ]
  }'
```

**3. Test Authorization (1 hour)**

```bash
# Login as read-only user
READONLY_TOKEN=$(curl -k -X POST https://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"readonly_user","password":"ReadOnly123!"}' | jq -r '.token')

# Test read access (should succeed)
curl -k -X GET https://localhost:5001/api/v1/secrets/database/password \
  -H "Authorization: Bearer $READONLY_TOKEN"
# Expected: 200 OK with secret data

# Test write access (should fail)
curl -k -X POST https://localhost:5001/api/v1/secrets \
  -H "Authorization: Bearer $READONLY_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"path":"/test/secret","data":{"key":"value"}}'
# Expected: 403 Forbidden
# {
#   "error": "insufficient_permissions",
#   "message": "User does not have 'write' permission on resource 'secrets'"
# }

# Test delete access (should fail)
curl -k -X DELETE https://localhost:5001/api/v1/secrets/test/secret \
  -H "Authorization: Bearer $READONLY_TOKEN"
# Expected: 403 Forbidden
```

### Deliverable (Day 11)
- [ ] OpenTelemetry packages installed
- [ ] ASP.NET Core, HTTP Client, EF Core instrumentation enabled
- [ ] Jaeger exporter configured
- [ ] Manual instrumentation added to VaultService, AuthenticationService
- [ ] Distributed traces visible in Jaeger with multiple spans
- [ ] SecretsController updated with RequirePermission attributes
- [ ] Test users created with limited permissions
- [ ] Authorization tests confirm 403 for insufficient permissions

---

## Day 12: Row-Level Security & Certificate Automation (8 hours)

### Objective
Enable PostgreSQL Row-Level Security and deploy cert-manager for automated certificate rotation.

### Prerequisites
- [ ] PostgreSQL 16 with TLS enabled
- [ ] Kubernetes cluster operational
- [ ] All services registered with namespaces

---

### Morning: Row-Level Security (4 hours)

**SEC-P1-009: Enable RLS on Secrets Table**

**Problem:** No database-level enforcement of namespace isolation.

**Solution:** Implement PostgreSQL Row-Level Security policies.

#### Implementation Steps

**1. Create RLS Migration Script (1 hour)**

```bash
cat > services/usp/migrations/sql/09-enable-rls-secrets.sql <<'EOF'
BEGIN;

-- Enable Row-Level Security on secrets table
ALTER TABLE usp.secrets ENABLE ROW LEVEL SECURITY;

-- Policy 1: Users can only SELECT secrets in their assigned namespaces
CREATE POLICY secrets_namespace_isolation ON usp.secrets
    FOR SELECT
    USING (
        namespace_id IN (
            SELECT namespace_id
            FROM usp.user_namespaces
            WHERE user_id = current_setting('app.current_user_id')::UUID
        )
    );

-- Policy 2: Users can only INSERT into their namespaces
CREATE POLICY secrets_insert_policy ON usp.secrets
    FOR INSERT
    WITH CHECK (
        namespace_id IN (
            SELECT namespace_id
            FROM usp.user_namespaces
            WHERE user_id = current_setting('app.current_user_id')::UUID
        )
    );

-- Policy 3: Users can only UPDATE secrets in their namespaces
CREATE POLICY secrets_update_policy ON usp.secrets
    FOR UPDATE
    USING (
        namespace_id IN (
            SELECT namespace_id
            FROM usp.user_namespaces
            WHERE user_id = current_setting('app.current_user_id')::UUID
        )
    );

-- Policy 4: Users can only DELETE from their namespaces
CREATE POLICY secrets_delete_policy ON usp.secrets
    FOR DELETE
    USING (
        namespace_id IN (
            SELECT namespace_id
            FROM usp.user_namespaces
            WHERE user_id = current_setting('app.current_user_id')::UUID
        )
    );

-- Create user_namespaces table if not exists
CREATE TABLE IF NOT EXISTS usp.user_namespaces (
    user_id UUID NOT NULL REFERENCES usp.users(user_id) ON DELETE CASCADE,
    namespace_id UUID NOT NULL REFERENCES usp.namespaces(namespace_id) ON DELETE CASCADE,
    created_at TIMESTAMP DEFAULT NOW(),
    PRIMARY KEY (user_id, namespace_id)
);

CREATE INDEX idx_user_namespaces_user ON usp.user_namespaces(user_id);
CREATE INDEX idx_user_namespaces_namespace ON usp.user_namespaces(namespace_id);

COMMIT;
EOF
```

**2. Update DbContext to Set User Context (2 hours)**

Edit `src/USP.Infrastructure/Data/USPDbContext.cs`:

```csharp
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

public class USPDbContext : DbContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public USPDbContext(
        DbContextOptions<USPDbContext> options,
        IHttpContextAccessor httpContextAccessor)
        : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public DbSet<Secret> Secrets { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Namespace> Namespaces { get; set; }
    public DbSet<UserNamespace> UserNamespaces { get; set; }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Set PostgreSQL session variable for RLS
        var userId = GetCurrentUserId();

        if (!string.IsNullOrEmpty(userId))
        {
            await Database.ExecuteSqlRawAsync(
                $"SET LOCAL app.current_user_id = '{userId}'",
                cancellationToken
            );
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    private string? GetCurrentUserId()
    {
        return _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Secrets table
        modelBuilder.Entity<Secret>(entity =>
        {
            entity.ToTable("secrets", "usp");
            entity.HasKey(e => e.SecretId);
            entity.Property(e => e.Path).IsRequired().HasMaxLength(512);
            entity.HasIndex(e => e.Path).IsUnique();
            entity.HasIndex(e => e.NamespaceId);
        });

        // User-Namespace junction table
        modelBuilder.Entity<UserNamespace>(entity =>
        {
            entity.ToTable("user_namespaces", "usp");
            entity.HasKey(e => new { e.UserId, e.NamespaceId });

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<Namespace>()
                .WithMany()
                .HasForeignKey(e => e.NamespaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
```

**3. Register IHttpContextAccessor (15 minutes)**

Edit `src/USP.API/Program.cs`:

```csharp
// Register IHttpContextAccessor for DbContext
builder.Services.AddHttpContextAccessor();

// Register DbContext with IHttpContextAccessor
builder.Services.AddDbContext<USPDbContext>((serviceProvider, options) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();

    options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
});
```

**4. Test RLS (45 minutes)**

```bash
# Apply RLS migration
psql -h localhost -U postgres -d postgres -f services/usp/migrations/sql/09-enable-rls-secrets.sql

# Create test namespaces and users
psql -h localhost -U postgres -d postgres <<'EOF'
-- Create test namespaces
INSERT INTO usp.namespaces (namespace_id, path, created_at)
VALUES
  ('11111111-1111-1111-1111-111111111111', '/team-a', NOW()),
  ('22222222-2222-2222-2222-222222222222', '/team-b', NOW());

-- Create test users
INSERT INTO usp.users (user_id, username, email, password_hash, created_at)
VALUES
  ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'user_a', 'usera@tw.local', 'hash1', NOW()),
  ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'user_b', 'userb@tw.local', 'hash2', NOW());

-- Assign users to namespaces
INSERT INTO usp.user_namespaces (user_id, namespace_id)
VALUES
  ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', '11111111-1111-1111-1111-111111111111'),
  ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', '22222222-2222-2222-2222-222222222222');

-- Create secrets in each namespace
INSERT INTO usp.secrets (secret_id, path, namespace_id, encrypted_data, version, created_at)
VALUES
  (gen_random_uuid(), '/team-a/secret1', '11111111-1111-1111-1111-111111111111', 'encrypted_data_1', 1, NOW()),
  (gen_random_uuid(), '/team-b/secret2', '22222222-2222-2222-2222-222222222222', 'encrypted_data_2', 1, NOW());
EOF

# Test RLS as user_a (should only see team-a secrets)
psql -h localhost -U usp_user -d postgres <<'EOF'
SET app.current_user_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
SELECT path, namespace_id FROM usp.secrets;
EOF
# Expected: Only /team-a/secret1 returned

# Test RLS as user_b (should only see team-b secrets)
psql -h localhost -U usp_user -d postgres <<'EOF'
SET app.current_user_id = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
SELECT path, namespace_id FROM usp.secrets;
EOF
# Expected: Only /team-b/secret2 returned
```

---

### Afternoon: Certificate Automation (4 hours)

**SEC-P1-012: Deploy cert-manager for Automated Certificate Rotation**

**Problem:** Manual certificate generation and renewal is error-prone.

**Solution:** Deploy cert-manager with Let's Encrypt integration.

#### Implementation Steps

**1. Install cert-manager (1 hour)**

```bash
# Add Helm repository
helm repo add jetstack https://charts.jetstack.io
helm repo update

# Install cert-manager CRDs
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.13.2/cert-manager.crds.yaml

# Create namespace
kubectl create namespace cert-manager

# Install cert-manager
helm install cert-manager jetstack/cert-manager \
  --namespace cert-manager \
  --version v1.13.2 \
  --set installCRDs=false \
  --set global.leaderElection.namespace=cert-manager

# Wait for cert-manager to be ready
kubectl wait --for=condition=ready pod -l app.kubernetes.io/name=cert-manager \
  -n cert-manager --timeout=300s
```

**2. Create ClusterIssuers (1 hour)**

```bash
# Let's Encrypt staging issuer (for testing)
cat > letsencrypt-staging.yaml <<'EOF'
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: letsencrypt-staging
spec:
  acme:
    server: https://acme-staging-v02.api.letsencrypt.org/directory
    email: devops@tw.local
    privateKeySecretRef:
      name: letsencrypt-staging
    solvers:
      - http01:
          ingress:
            class: nginx
EOF

kubectl apply -f letsencrypt-staging.yaml

# Let's Encrypt production issuer
cat > letsencrypt-prod.yaml <<'EOF'
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: letsencrypt-prod
spec:
  acme:
    server: https://acme-v02.api.letsencrypt.org/directory
    email: devops@tw.local
    privateKeySecretRef:
      name: letsencrypt-prod
    solvers:
      - http01:
          ingress:
            class: nginx
EOF

kubectl apply -f letsencrypt-prod.yaml

# Self-signed issuer for internal services
cat > selfsigned-issuer.yaml <<'EOF'
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: selfsigned-issuer
spec:
  selfSigned: {}
EOF

kubectl apply -f selfsigned-issuer.yaml
```

**3. Create Certificates for Services (1.5 hours)**

```bash
# USP API certificate
cat > usp-certificate.yaml <<'EOF'
apiVersion: cert-manager.io/v1
kind: Certificate
metadata:
  name: usp-api-tls
  namespace: default
spec:
  secretName: usp-api-tls
  duration: 2160h  # 90 days
  renewBefore: 360h  # Renew 15 days before expiry
  issuerRef:
    name: letsencrypt-prod
    kind: ClusterIssuer
  commonName: usp-api.tw.local
  dnsNames:
    - usp-api.tw.local
    - usp.default.svc.cluster.local
  privateKey:
    algorithm: RSA
    size: 2048
    rotationPolicy: Always
EOF

kubectl apply -f usp-certificate.yaml

# Metrics endpoint certificate
cat > usp-metrics-certificate.yaml <<'EOF'
apiVersion: cert-manager.io/v1
kind: Certificate
metadata:
  name: usp-metrics-tls
  namespace: default
spec:
  secretName: usp-metrics-tls
  duration: 2160h
  renewBefore: 360h
  issuerRef:
    name: selfsigned-issuer
    kind: ClusterIssuer
  commonName: usp-metrics.local
  dnsNames:
    - usp-metrics.local
  privateKey:
    algorithm: RSA
    size: 2048
EOF

kubectl apply -f usp-metrics-certificate.yaml

# Repeat for other services (NCCS, UCCP, UDPS, Stream Compute)
```

**4. Update Deployments to Use Certificates (30 minutes)**

```yaml
# Example: USP Deployment
apiVersion: apps/v1
kind: Deployment
metadata:
  name: usp
spec:
  template:
    spec:
      containers:
        - name: usp-api
          image: usp:latest
          volumeMounts:
            - name: tls-certs
              mountPath: /app/certs
              readOnly: true
          env:
            - name: Certificate__Path
              value: /app/certs/tls.crt
            - name: Certificate__KeyPath
              value: /app/certs/tls.key
      volumes:
        - name: tls-certs
          secret:
            secretName: usp-api-tls  # ✅ Managed by cert-manager
```

**5. Verify Certificate Issuance (30 minutes)**

```bash
# Check certificate status
kubectl describe certificate usp-api-tls

# Expected:
# Status:
#   Conditions:
#     Type:    Ready
#     Status:  True
#     Message: Certificate is up to date and has not expired

# Check secret created
kubectl get secret usp-api-tls -o yaml

# Verify certificate expiry
kubectl get secret usp-api-tls -o jsonpath='{.data.tls\.crt}' | base64 -d | openssl x509 -noout -dates
# Expected:
# notBefore=Dec 27 00:00:00 2025 GMT
# notAfter=Mar 27 00:00:00 2026 GMT  (90 days)
```

### Deliverable (Day 12)
- [ ] Row-Level Security enabled on usp.secrets table
- [ ] RLS policies created for SELECT, INSERT, UPDATE, DELETE
- [ ] USPDbContext sets `app.current_user_id` session variable
- [ ] IHttpContextAccessor registered in DI
- [ ] RLS tested with multiple users in different namespaces
- [ ] cert-manager installed and operational
- [ ] ClusterIssuers created (Let's Encrypt staging/prod, self-signed)
- [ ] Certificates created for all services with 90-day rotation
- [ ] Services configured to mount cert-manager managed secrets
- [ ] Certificate expiry verified (90 days, auto-renewal at 75 days)

---

## End of Week 2: Verification Checklist

### Security Verification

- [ ] **Database Security**
  - [ ] All schema scripts use BEGIN/COMMIT transactions
  - [ ] SQL scripts use parameterized passwords (`:VAR_NAME`)
  - [ ] Credential loader fetches passwords from Vault
  - [ ] Row-Level Security enforces namespace isolation
  - [ ] Users can only access secrets in assigned namespaces

- [ ] **TLS/HTTPS**
  - [ ] Metrics endpoint serves HTTPS on port 9091
  - [ ] HSTS middleware enabled with 1-year max-age
  - [ ] Prometheus scrapes metrics over HTTPS
  - [ ] All observability URLs default to HTTPS
  - [ ] Certificate automation via cert-manager operational

- [ ] **Observability**
  - [ ] Prometheus deployed with 30-day retention
  - [ ] Grafana dashboards created for USP metrics
  - [ ] Alertmanager configured with Slack notifications
  - [ ] Jaeger collecting distributed traces
  - [ ] Elasticsearch indexing logs
  - [ ] All services reporting metrics successfully

- [ ] **Authorization**
  - [ ] SecretsController uses RequirePermission attributes
  - [ ] Read-only users cannot write/delete secrets (403 Forbidden)
  - [ ] Admin users have full access to all operations
  - [ ] Authorization checks recorded in metrics

### Compliance Verification

- [ ] **SOC 2 CC6.1** - Granular access controls on secrets
- [ ] **SOC 2 CC7.2** - Continuous monitoring operational
- [ ] **HIPAA 164.312(c)(1)** - Data integrity via RLS
- [ ] **PCI-DSS Req 10.2** - Audit logging via distributed tracing

### Testing

```bash
# Run full test suite
cd services/usp
dotnet test

# Database security tests
dotnet test --filter "FullyQualifiedName~RowLevelSecurity"

# Authorization tests
dotnet test --filter "FullyQualifiedName~AuthorizationTests"

# Metrics tests
dotnet test --filter "FullyQualifiedName~MetricsTests"

# Integration tests with observability
cd ../../tests/integration
dotnet test --filter "FullyQualifiedName~ObservabilityTests"

# Verify all tests pass
# Expected: 0 failures
```

### Performance Validation

```bash
# Check trace overhead (should be <5% latency increase)
# Before tracing:
ab -n 1000 -c 10 https://localhost:5001/api/v1/health
# Note p50, p95, p99 latencies

# After tracing:
ab -n 1000 -c 10 https://localhost:5001/api/v1/health
# Expected: <5% increase in latencies

# Check metrics endpoint performance
curl -o /dev/null -s -w "Time: %{time_total}s\n" http://localhost:9091/metrics
# Expected: <100ms response time
```

---

## Handoff to Phase 3

**Phase 2 Complete Criteria:**
- All 12 P1 findings resolved
- Observability stack operational (Prometheus, Grafana, Jaeger, Elasticsearch)
- Distributed tracing implemented across all services
- Row-Level Security enforces multi-tenant isolation
- Automated certificate management via cert-manager
- HTTPS enforced on all endpoints
- Granular authorization on secrets endpoints

**Phase 3 Preview:**
- P2 findings (Documentation, Configuration, Code Quality)
- 15 tasks over Week 3
- Non-critical but enhances maintainability and developer experience

---

**Status:** Ready to Start
**Last Updated:** 2025-12-27
**Phase Owner:** Security + DevOps + Backend Teams

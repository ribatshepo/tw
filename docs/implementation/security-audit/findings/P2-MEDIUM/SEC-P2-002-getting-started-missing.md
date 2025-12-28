# SEC-P2-002: GETTING_STARTED Missing

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P2-002 |
| **Title** | No GETTING_STARTED.md Onboarding Guide |
| **Priority** | P2 - MEDIUM |
| **Severity** | Medium |
| **Category** | Documentation |
| **Status** | Not Started |
| **Effort Estimate** | 6 hours |
| **Implementation Phase** | Phase 3 (Week 3, Day 1-2) |
| **Assigned To** | Technical Writer + DevOps Engineer |
| **Created** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:477-486` |
| **Code Files** | None (file missing) |
| **Dependencies** | SEC-P2-001 (Root README references this file) |
| **Compliance Impact** | SOC 2 (CC1.4 - Documentation) |

---

## 3. Executive Summary

### Problem

No GETTING_STARTED.md guide for new developers. Referenced in root README but doesn't exist.

### Impact

- **No Onboarding Path:** New developers don't know where to start
- **Broken Documentation Links:** Root README references non-existent file
- **Increased Onboarding Time:** Developers must figure out setup through trial/error

### Solution

Create comprehensive GETTING_STARTED.md with step-by-step setup for all services, environment configuration, and first-time developer workflows.

---

## 4. Implementation Guide

### Step 1: Create GETTING_STARTED.md (5 hours)

```markdown
# Getting Started with TW Platform

This guide walks you through setting up the TW platform development environment from scratch.

## Prerequisites

Before you begin, ensure you have the following installed:

### Required Tools

- **Docker Desktop 24.0+** with Docker Compose 2.20+
- **Git 2.40+**
- **PostgreSQL 16+ Client** (psql command-line tool)

### Language Runtimes (Service-Specific)

| Service | Required Runtime |
|---------|-----------------|
| USP, NCCS | .NET 8 SDK |
| UCCP | Go 1.24+ |
| UDPS | Java 17+, Scala 2.13 |
| Stream Compute | Rust 1.75+ |

### IDE Recommendations

- **Visual Studio Code** with extensions:
  - C# Dev Kit (for USP, NCCS)
  - Go extension (for UCCP)
  - Scala (Metals) extension (for UDPS)
  - rust-analyzer (for Stream Compute)
- **JetBrains Rider** (for .NET services)
- **IntelliJ IDEA** (for Scala/Java services)

## Step 1: Clone Repository

```bash
# Clone the repository
git clone https://github.com/your-org/tw.git
cd tw

# Verify repository structure
ls -la
# Expected: services/, docs/, config/, deploy/, proto/, tests/
```

## Step 2: Start Infrastructure Services

The platform requires PostgreSQL, Redis, Kafka, and other infrastructure services.

```bash
# Start all infrastructure services
docker-compose -f docker-compose.infra.yml up -d

# Verify services are healthy (wait ~60 seconds)
docker-compose -f docker-compose.infra.yml ps

# Expected output:
# NAME                     STATUS
# tw-postgres              Up (healthy)
# tw-redis                 Up (healthy)
# tw-kafka                 Up (healthy)
# tw-zookeeper             Up (healthy)
# tw-minio                 Up (healthy)
```

### Verify Infrastructure

```bash
# Test PostgreSQL connection
psql -h localhost -U postgres -d postgres -c "SELECT version();"

# Test Redis connection
docker exec -it tw-redis redis-cli ping
# Expected: PONG

# Test MinIO (object storage)
curl http://localhost:9000/minio/health/live
# Expected: OK
```

## Step 3: Set Up USP (Unified Security Platform)

USP is the security foundation for all services. Start here first.

### 3.1 Generate Development Certificates

```bash
cd services/usp

# Generate TLS certificates for development
bash scripts/generate-dev-certs.sh

# Verify certificates created
ls -la certs/
# Expected: ca.crt, usp.crt, usp.key, usp.pfx
```

### 3.2 Generate Infrastructure Credentials

```bash
# Generate random passwords for databases, Redis, etc.
bash scripts/generate-infrastructure-credentials.sh

# Review generated .env file
cat .env
# Contains: Database passwords, Redis password, JWT secrets, etc.
```

### 3.3 Bootstrap Database

```bash
# Apply database migrations
bash scripts/bootstrap-database.sh

# Verify database schema
psql -h localhost -U postgres -d usp_dev -c "\dt usp.*"
# Expected: users, roles, permissions, secrets, vault_keys, etc.
```

### 3.4 Generate Master Encryption Key

```bash
# Generate KEK (Key Encryption Key) for secrets encryption
bash scripts/generate-master-key.sh

# Add to .env file
echo "USP_Encryption__MasterKey=<generated-key>" >> .env
```

### 3.5 Start USP Service

```bash
# Build USP
dotnet build src/USP.API/USP.API.csproj

# Run USP
dotnet run --project src/USP.API

# In another terminal, verify USP is running
curl -k https://localhost:5001/health
# Expected: {"status":"Healthy","vault":{"sealed":true}}
```

### 3.6 Unseal Vault

USP Vault starts in sealed state. You must unseal it using the master key.

```bash
# Get unseal keys from bootstrap output
# (Stored in services/usp/vault-unseal-keys.json)

# Unseal with 3 keys
curl -k -X POST https://localhost:5001/api/v1/vault/seal/unseal \
  -H "Content-Type: application/json" \
  -d '{"key":"<key1>"}'

curl -k -X POST https://localhost:5001/api/v1/vault/seal/unseal \
  -H "Content-Type: application/json" \
  -d '{"key":"<key2>"}'

curl -k -X POST https://localhost:5001/api/v1/vault/seal/unseal \
  -H "Content-Type: application/json" \
  -d '{"key":"<key3>"}'

# Verify unsealed
curl -k https://localhost:5001/health
# Expected: {"status":"Healthy","vault":{"sealed":false}}
```

## Step 4: Create Test User

```bash
# Create admin user
curl -k -X POST https://localhost:5001/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "admin",
    "email": "admin@localhost",
    "password": "Admin123!@#"
  }'

# Login to get JWT token
curl -k -X POST https://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "admin",
    "password": "Admin123!@#"
  }'

# Save the token for future requests
export TOKEN="<jwt-token-from-response>"
```

## Step 5: Set Up Other Services (Optional)

### UCCP (Compute Platform)

```bash
cd services/uccp

# Install dependencies
go mod download

# Build
go build ./cmd/uccp

# Run
./uccp --config config/development.yaml
```

### NCCS (.NET Client)

```bash
cd services/nccs

# Restore dependencies
dotnet restore

# Build
dotnet build

# Run
dotnet run --project src/NCCS.API
```

### UDPS (Data Platform)

```bash
cd services/udps

# Compile
sbt compile

# Run
sbt run
```

### Stream Compute

```bash
cd services/stream-compute

# Build
cargo build

# Run
cargo run
```

## Step 6: Verify Inter-Service Communication

```bash
# Test NCCS → USP communication
curl https://localhost:5001/api/v1/health

# Test UDPS → USP authentication
# (Requires service tokens - see service-specific docs)
```

## Common Issues and Solutions

### Issue: PostgreSQL Connection Refused

**Solution:**
```bash
# Restart PostgreSQL container
docker-compose -f docker-compose.infra.yml restart postgres

# Check PostgreSQL logs
docker-compose -f docker-compose.infra.yml logs postgres
```

### Issue: Certificate Verification Failed

**Solution:**
```bash
# Regenerate certificates
cd services/usp
rm -rf certs/
bash scripts/generate-dev-certs.sh
```

### Issue: Vault Sealed After Restart

**Solution:**
```bash
# Unseal vault with saved keys
# (See Step 3.6 above)
```

## Next Steps

- **Explore APIs:** Open http://localhost:5001/swagger for USP API documentation
- **Read Specs:** Review detailed architecture in `docs/specs/`
- **Run Tests:** Execute `make test-usp` to verify setup
- **Deploy to Kubernetes:** Follow `docs/DEPLOYMENT.md` for production deployment

## Need Help?

- **Troubleshooting:** See [TROUBLESHOOTING.md](TROUBLESHOOTING.md)
- **Development Workflow:** See [docs/development/DEVELOPMENT_WORKFLOW.md](docs/development/DEVELOPMENT_WORKFLOW.md)
- **Issues:** Report bugs at [GitHub Issues](https://github.com/your-org/tw/issues)
```

### Step 2: Test Getting Started Guide (1 hour)

```bash
# Test the guide from scratch on a clean machine
# (Use a fresh VM or Docker container)

# Follow every step and verify commands work
# Fix any errors or outdated commands
```

---

## 5. Testing

- [ ] GETTING_STARTED.md created
- [ ] All prerequisites documented
- [ ] Step-by-step setup verified on clean machine
- [ ] All commands work correctly
- [ ] Common issues documented
- [ ] Links to other docs valid

---

## 6. Compliance Evidence

**SOC 2 CC1.4:** Onboarding documentation available for new developers

---

## 7. Sign-Off

- [ ] **Technical Writer:** GETTING_STARTED.md complete
- [ ] **DevOps:** Setup steps verified

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P2-002**

# SEC-P0-003: Hardcoded Passwords in SQL Init Scripts

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P0-003 |
| **Title** | Hardcoded Passwords in 02-create-roles.sql |
| **Priority** | P0 - CRITICAL |
| **Severity** | Critical |
| **Category** | Secrets Management / Database Security |
| **Status** | Not Started |
| **Effort Estimate** | 3 hours |
| **Implementation Phase** | Phase 1 (Week 1, Day 2) |
| **Assigned To** | Backend Engineer 2 |
| **Reviewers** | Security Engineer, DBA |
| **Created** | 2025-12-27 |
| **Last Updated** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:89-98` |
| **Security Spec** | `/home/tshepo/projects/tw/docs/specs/security.md:528-662` (Enterprise Secrets Management) |
| **Code Files** | `/home/tshepo/projects/tw/config/postgres/init-scripts/02-create-roles.sql` (5 hardcoded passwords, lines 13, 18, 23, 28, 33) |
| **Dependencies** | Blocks SEC-P1-011 (SQL Parameterized Passwords) |
| **Blocked By** | None |
| **Related Findings** | SEC-P0-001 (.env secrets), SEC-P0-002 (appsettings secrets), SEC-P1-011 (SQL parameterized passwords) |
| **Compliance Impact** | SOC 2 (CC6.7), HIPAA (164.312(a)(2)(iv)), PCI-DSS (Req 8.2.1, Req 8.2.3) |

---

## 3. Executive Summary

### Problem Statement

The PostgreSQL initialization script `02-create-roles.sql` contains **5 hardcoded database passwords** in plaintext that were **committed to Git**. These passwords create database users for all 5 platform services (UCCP, NCCS, USP, UDPS, Stream Compute), exposing credentials for the entire multi-tenant database infrastructure.

### Business Impact

- **Multi-Service Credential Exposure:** Database passwords for **all 5 services** exposed in Git
- **Database Compromise:** Attackers can access all service databases (UCCP, NCCS, USP, UDPS, Stream Compute)
- **Data Breach:** Access to user data, ML models, secrets (in USP database), stream processing state
- **Lateral Movement:** Database access enables pivot to application layers of all services
- **Compliance Violation:** Violates SOC 2 CC6.7, HIPAA 164.312(a)(2)(iv), PCI-DSS Req 8.2.1/8.2.3
- **Production Blocker:** P0 finding that **BLOCKS PRODUCTION DEPLOYMENT**

### Solution Overview

1. **Parameterize SQL scripts** to accept passwords as variables instead of hardcoding
2. **Create init-db.sh wrapper script** to inject passwords from environment variables
3. **Update Docker Compose** to pass environment variables to PostgreSQL init container
4. **Rotate all 5 database passwords** immediately
5. **Remove hardcoded passwords from Git history** (or document accepted risk)
6. **Update database connection strings** in all services to use new passwords

**Timeline:** 3 hours (Day 2 of Week 1, parallel with SEC-P0-001/002)

---

## 4. Technical Details

### Current State

**File: `/home/tshepo/projects/tw/config/postgres/init-scripts/02-create-roles.sql`**

```sql
-- Create database users for all services

-- UCCP (Unified Compute & Coordination Platform)
CREATE DATABASE uccp_db;
CREATE USER uccp_user WITH PASSWORD 'uccp_dev_password_change_me';  -- ❌ Line 13
GRANT ALL PRIVILEGES ON DATABASE uccp_db TO uccp_user;

-- NCCS (.NET Compute Client Service)
CREATE DATABASE nccs_db;
CREATE USER nccs_user WITH PASSWORD 'nccs_dev_password_change_me';  -- ❌ Line 18
GRANT ALL PRIVILEGES ON DATABASE nccs_db TO nccs_user;

-- USP (Unified Security Platform)
CREATE DATABASE usp_db;
CREATE USER usp_user WITH PASSWORD 'usp_dev_password_change_me';  -- ❌ Line 23
GRANT ALL PRIVILEGES ON DATABASE usp_db TO usp_user;

-- UDPS (Unified Data Platform Service)
CREATE DATABASE udps_db;
CREATE USER udps_user WITH PASSWORD 'udps_dev_password_change_me';  -- ❌ Line 28
GRANT ALL PRIVILEGES ON DATABASE udps_db TO udps_user;

-- Stream Compute Service
CREATE DATABASE stream_db;
CREATE USER stream_user WITH PASSWORD 'stream_dev_password_change_me';  -- ❌ Line 33
GRANT ALL PRIVILEGES ON DATABASE stream_db TO stream_user;
```

**Current Usage:**
- PostgreSQL Docker container runs init scripts on first startup
- Scripts run as `postgres` superuser
- Scripts create databases and users for all services
- File is committed to Git repository (public exposure)

### Vulnerability Analysis

**1. Hardcoded Password Pattern:**
- All 5 passwords follow predictable pattern: `{service}_dev_password_change_me`
- Weak passwords (33-35 characters, dictionary words)
- Easily guessable by attackers familiar with repository structure

**2. Multi-Service Impact:**
- **UCCP Database:** Task scheduling, ML training metadata, service registry
- **NCCS Database:** .NET client state, cached compute results
- **USP Database:** **All user credentials, JWT signing keys, vault master keys, secrets**
- **UDPS Database:** Columnar data, SQL query results, data lineage
- **Stream Compute Database:** Stream processing state, CEP patterns, anomaly detection results

**3. USP Database - Critical Target:**
- USP database contains **the keys to the kingdom**:
  - User authentication credentials (bcrypt hashes, but attackers can brute-force)
  - JWT signing keys (compromises all service authentication)
  - Vault master keys (compromises entire secrets management system)
  - All secrets for all services (database passwords, API keys, etc.)
- Compromise of USP database = **complete platform compromise**

**4. Git History Exposure:**
- SQL scripts committed to Git in plaintext
- Passwords visible to anyone with repository access (past or present)
- Password rotation requires Git history rewrite or acceptance of risk

**5. Weak Password Policy:**
- Passwords don't meet security requirements (32+ characters, high entropy)
- Passwords contain predictable words (`dev`, `password`, `change_me`)
- No special characters, numbers, or mixed case variety

### Gap Analysis

**Security Specification Requirements:**

1. **Requirement:** "Database passwords MUST be 32+ characters with high entropy" (line 551)
   - **Current State:** ❌ VIOLATED - Passwords are 33-35 chars but low entropy (dictionary words)

2. **Requirement:** "SQL scripts MUST accept passwords as parameters, not hardcode them" (line 553)
   - **Current State:** ❌ NOT IMPLEMENTED - Passwords hardcoded in CREATE USER statements

3. **Requirement:** "Database initialization MUST use environment variables or secrets manager" (line 554)
   - **Current State:** ❌ NOT IMPLEMENTED - Static SQL scripts with hardcoded values

4. **Requirement:** "Credentials MUST be rotated every 90 days" (line 555)
   - **Current State:** ❌ NOT IMPLEMENTED - Passwords never rotated, no rotation mechanism

**Compliance Violations:**

- **SOC 2 CC6.7:** Encryption keys (database passwords) not protected
- **HIPAA 164.312(a)(2)(iv):** Database contains ePHI, passwords must be encrypted/protected
- **PCI-DSS Req 8.2.1:** Strong authentication requires strong passwords
- **PCI-DSS Req 8.2.3:** Passwords must be encrypted during transmission and storage

---

## 5. Implementation Requirements

### Acceptance Criteria

- [ ] SQL scripts parameterized (no hardcoded passwords)
- [ ] `init-db.sh` wrapper script created to inject passwords from environment
- [ ] Docker Compose updated to pass environment variables to PostgreSQL
- [ ] All 5 database passwords rotated to strong, random passwords (32+ characters)
- [ ] All services updated with new database connection strings
- [ ] Database initialization successful with parameterized passwords
- [ ] Documentation updated (GETTING_STARTED.md, DEPLOYMENT.md)
- [ ] Security regression test passing
- [ ] (Optional) Git history cleaned of hardcoded passwords

### Technical Requirements

1. **Parameterize SQL Scripts:**
   - Replace `PASSWORD 'hardcoded_value'` with `PASSWORD :'variable_name'`
   - Use psql variables (`:variable_name` syntax)

2. **Create init-db.sh Wrapper:**
   - Bash script that exports environment variables
   - Calls psql with `-v` flag to pass variables
   - Handles errors and validates password injection

3. **Docker Compose Integration:**
   - Pass environment variables to PostgreSQL init container
   - Use `.env` file or environment variable substitution
   - Ensure passwords not logged in Docker Compose output

4. **Password Generation:**
   - Generate 32+ character random passwords (high entropy)
   - Use `openssl rand -base64` or equivalent
   - Store securely (not in Git)

5. **Service Configuration Update:**
   - Update connection strings in UCCP, NCCS, USP, UDPS, Stream Compute
   - Use environment variables or configuration files (not hardcoded)
   - Test connectivity with new passwords

### Compliance Requirements

**SOC 2 Evidence:**
- Screenshot of parameterized SQL scripts
- Audit log of password rotation
- Password strength verification (32+ characters, high entropy)

**HIPAA Evidence:**
- Password encryption at rest (PostgreSQL pg_authid hashes)
- Access controls limiting database access
- Audit logs of all database password changes

**PCI-DSS Evidence:**
- Password complexity requirements met
- Passwords not stored in plaintext
- Password rotation policy (90 days)

---

## 6. Step-by-Step Implementation Guide

### Prerequisites

- [x] PostgreSQL running in Docker
- [x] Docker Compose configured
- [x] Access to all service repositories
- [x] Backup of database (if data exists)

### Step 1: Backup Current Database and Scripts (10 minutes)

```bash
cd /home/tshepo/projects/tw

# Backup SQL scripts
cp config/postgres/init-scripts/02-create-roles.sql config/postgres/init-scripts/02-create-roles.sql.backup

# Backup database (if contains data)
docker-compose exec postgres pg_dumpall -U postgres > /tmp/postgres-backup-$(date +%Y%m%d-%H%M%S).sql

# Verify backup
ls -lh /tmp/postgres-backup-*.sql
```

### Step 2: Parameterize SQL Scripts (20 minutes)

```bash
cd /home/tshepo/projects/tw/config/postgres/init-scripts

# Update 02-create-roles.sql to use psql variables
cat > 02-create-roles.sql << 'EOF'
-- Create database users for all services
-- Passwords injected via psql variables (not hardcoded)

-- UCCP (Unified Compute & Coordination Platform)
CREATE DATABASE uccp_db;
CREATE USER uccp_user WITH PASSWORD :'uccp_password';
GRANT ALL PRIVILEGES ON DATABASE uccp_db TO uccp_user;

-- NCCS (.NET Compute Client Service)
CREATE DATABASE nccs_db;
CREATE USER nccs_user WITH PASSWORD :'nccs_password';
GRANT ALL PRIVILEGES ON DATABASE nccs_db TO nccs_user;

-- USP (Unified Security Platform)
CREATE DATABASE usp_db;
CREATE USER usp_user WITH PASSWORD :'usp_password';
GRANT ALL PRIVILEGES ON DATABASE usp_db TO usp_user;

-- UDPS (Unified Data Platform Service)
CREATE DATABASE udps_db;
CREATE USER udps_user WITH PASSWORD :'udps_password';
GRANT ALL PRIVILEGES ON DATABASE udps_db TO udps_user;

-- Stream Compute Service
CREATE DATABASE stream_db;
CREATE USER stream_user WITH PASSWORD :'stream_password';
GRANT ALL PRIVILEGES ON DATABASE stream_db TO stream_user;
EOF

echo "SQL script parameterized successfully"
```

**Verification:**
```bash
# Verify no hardcoded passwords
grep -i "password.*'.*_dev_password" 02-create-roles.sql
# Expected: No output (no hardcoded passwords found)

# Verify parameterized variables
grep "PASSWORD :" 02-create-roles.sql
# Expected: 5 lines with PASSWORD :'variable_name'
```

### Step 3: Create init-db.sh Wrapper Script (30 minutes)

```bash
cd /home/tshepo/projects/tw/config/postgres

# Create init-db.sh wrapper
cat > init-db.sh << 'EOF'
#!/usr/bin/env bash
set -euo pipefail

echo "Initializing PostgreSQL databases with parameterized passwords..."

# Validate required environment variables
REQUIRED_VARS=(
  "UCCP_DB_PASSWORD"
  "NCCS_DB_PASSWORD"
  "USP_DB_PASSWORD"
  "UDPS_DB_PASSWORD"
  "STREAM_DB_PASSWORD"
)

for VAR in "${REQUIRED_VARS[@]}"; do
  if [ -z "${!VAR:-}" ]; then
    echo "ERROR: Environment variable $VAR is not set"
    exit 1
  fi
done

echo "All required environment variables present"

# Run SQL scripts with password variables
echo "Creating databases and users..."
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
  -v uccp_password="$UCCP_DB_PASSWORD" \
  -v nccs_password="$NCCS_DB_PASSWORD" \
  -v usp_password="$USP_DB_PASSWORD" \
  -v udps_password="$UDPS_DB_PASSWORD" \
  -v stream_password="$STREAM_DB_PASSWORD" \
  -f /docker-entrypoint-initdb.d/02-create-roles.sql

echo "Database initialization complete"

# Verify users created
echo "Verifying database users..."
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
  SELECT usename FROM pg_user WHERE usename IN ('uccp_user', 'nccs_user', 'usp_user', 'udps_user', 'stream_user');
EOSQL

echo "Database initialization successful"
EOF

# Make script executable
chmod +x init-db.sh

echo "init-db.sh created successfully"
```

**Verification:**
```bash
# Check script is executable
ls -l init-db.sh | grep -q "x" && echo "PASS: Script is executable"

# Validate script syntax
bash -n init-db.sh && echo "PASS: Script syntax valid"
```

### Step 4: Generate Strong Random Passwords (15 minutes)

```bash
cd /home/tshepo/projects/tw

# Generate 5 strong passwords (32 characters each)
echo "Generating strong random passwords..."

UCCP_DB_PASSWORD=$(openssl rand -base64 32 | tr -d "=+/" | cut -c1-32)
NCCS_DB_PASSWORD=$(openssl rand -base64 32 | tr -d "=+/" | cut -c1-32)
USP_DB_PASSWORD=$(openssl rand -base64 32 | tr -d "=+/" | cut -c1-32)
UDPS_DB_PASSWORD=$(openssl rand -base64 32 | tr -d "=+/" | cut -c1-32)
STREAM_DB_PASSWORD=$(openssl rand -base64 32 | tr -d "=+/" | cut -c1-32)

# Save to temporary file (securely delete after use)
cat > /tmp/db-passwords-$(date +%Y%m%d-%H%M%S).txt << EOF
UCCP_DB_PASSWORD=$UCCP_DB_PASSWORD
NCCS_DB_PASSWORD=$NCCS_DB_PASSWORD
USP_DB_PASSWORD=$USP_DB_PASSWORD
UDPS_DB_PASSWORD=$UDPS_DB_PASSWORD
STREAM_DB_PASSWORD=$STREAM_DB_PASSWORD
EOF

echo "Passwords saved to /tmp/db-passwords-*.txt"
echo "IMPORTANT: Delete this file after updating .env and services"

# Display passwords (one-time viewing)
cat /tmp/db-passwords-*.txt
```

**Security Note:** Store passwords in password manager or USP Vault, then securely delete `/tmp/db-passwords-*.txt`

### Step 5: Update .env File (10 minutes)

```bash
cd /home/tshepo/projects/tw

# Source the new passwords
source /tmp/db-passwords-*.txt

# Update .env file (or create if using SEC-P0-001 externalization)
# If .env exists, update passwords
if [ -f .env ]; then
  sed -i "s/^UCCP_DB_PASSWORD=.*/UCCP_DB_PASSWORD=$UCCP_DB_PASSWORD/" .env
  sed -i "s/^NCCS_DB_PASSWORD=.*/NCCS_DB_PASSWORD=$NCCS_DB_PASSWORD/" .env
  sed -i "s/^USP_DB_PASSWORD=.*/USP_DB_PASSWORD=$USP_DB_PASSWORD/" .env
  sed -i "s/^UDPS_DB_PASSWORD=.*/UDPS_DB_PASSWORD=$UDPS_DB_PASSWORD/" .env
  sed -i "s/^STREAM_DB_PASSWORD=.*/STREAM_DB_PASSWORD=$STREAM_DB_PASSWORD/" .env
else
  # Create .env with new passwords (if following SEC-P0-001)
  echo "UCCP_DB_PASSWORD=$UCCP_DB_PASSWORD" >> .env
  echo "NCCS_DB_PASSWORD=$NCCS_DB_PASSWORD" >> .env
  echo "USP_DB_PASSWORD=$USP_DB_PASSWORD" >> .env
  echo "UDPS_DB_PASSWORD=$UDPS_DB_PASSWORD" >> .env
  echo "STREAM_DB_PASSWORD=$STREAM_DB_PASSWORD" >> .env
fi

# Set file permissions
chmod 600 .env

# Verify .env not staged for commit
git status | grep -q ".env" && echo "WARNING: .env is staged!" || echo "OK: .env not staged"
```

### Step 6: Update Docker Compose (20 minutes)

```bash
cd /home/tshepo/projects/tw

# Update docker-compose.yml to pass environment variables to PostgreSQL
# (This is an example - adjust based on your actual docker-compose.yml structure)

# Backup docker-compose.yml
cp docker-compose.yml docker-compose.yml.backup
```

Edit `docker-compose.yml` to add environment variables to PostgreSQL service:

```yaml
services:
  postgres:
    image: postgres:15-alpine
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: ${POSTGRES_SUPERUSER_PASSWORD}
      POSTGRES_DB: postgres
      # Pass database passwords for init scripts
      UCCP_DB_PASSWORD: ${UCCP_DB_PASSWORD}
      NCCS_DB_PASSWORD: ${NCCS_DB_PASSWORD}
      USP_DB_PASSWORD: ${USP_DB_PASSWORD}
      UDPS_DB_PASSWORD: ${UDPS_DB_PASSWORD}
      STREAM_DB_PASSWORD: ${STREAM_DB_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./config/postgres/init-scripts:/docker-entrypoint-initdb.d
      - ./config/postgres/init-db.sh:/docker-entrypoint-initdb.d/01-init-db.sh  # Run before SQL scripts
```

**Commit changes:**
```bash
git add docker-compose.yml
git commit -m "Add database password environment variables to PostgreSQL

- Pass UCCP, NCCS, USP, UDPS, Stream DB passwords as environment variables
- Enables parameterized database initialization
- Removes need for hardcoded passwords in SQL scripts

Related to: SEC-P0-003"
```

### Step 7: Test Database Initialization (30 minutes)

```bash
cd /home/tshepo/projects/tw

# Stop and remove existing PostgreSQL container and volume
docker-compose stop postgres
docker-compose rm -f postgres
docker volume rm tw_postgres_data  # CAUTION: This deletes all data

# Source passwords
source /tmp/db-passwords-*.txt

# Export passwords for docker-compose
export UCCP_DB_PASSWORD
export NCCS_DB_PASSWORD
export USP_DB_PASSWORD
export UDPS_DB_PASSWORD
export STREAM_DB_PASSWORD

# Start PostgreSQL with parameterized initialization
docker-compose up -d postgres

# Wait for initialization
echo "Waiting for database initialization..."
sleep 30

# Check logs
docker-compose logs postgres | grep -i "database initialization"

# Verify users created
docker-compose exec postgres psql -U postgres -c "\du"

# Expected output:
#   uccp_user, nccs_user, usp_user, udps_user, stream_user

# Verify databases created
docker-compose exec postgres psql -U postgres -c "\l"

# Expected output:
#   uccp_db, nccs_db, usp_db, udps_db, stream_db

# Test connection with new password
docker-compose exec postgres psql -U usp_user -d usp_db -c "SELECT 1"

# Expected: Successfully connected and returns 1
```

**If initialization fails:**
1. Check logs: `docker-compose logs postgres`
2. Verify environment variables: `docker-compose exec postgres env | grep PASSWORD`
3. Verify init-db.sh executed: `docker-compose logs postgres | grep "init-db.sh"`
4. Debug by running init-db.sh manually in container

### Step 8: Update Service Connection Strings (40 minutes)

**For each service, update database connection strings to use new passwords.**

**USP Service (already done in SEC-P0-002 if using user-secrets):**
```bash
cd /home/tshepo/projects/tw/services/usp/src/USP.API

# Update user secrets with new database password
dotnet user-secrets set "Database:Password" "$USP_DB_PASSWORD"

# Verify
dotnet user-secrets list | grep "Database:Password"
```

**UCCP, NCCS, UDPS, Stream Compute Services:**
Update configuration files or environment variables similarly:

```bash
# Example for UCCP (Go service)
# Update .env or environment variable
export UCCP_DATABASE_URL="postgresql://uccp_user:$UCCP_DB_PASSWORD@localhost:5432/uccp_db?sslmode=require"

# Example for NCCS (.NET service)
cd services/nccs/src/NCCS.API
dotnet user-secrets set "Database:Password" "$NCCS_DB_PASSWORD"

# Example for UDPS (Scala/Java service)
# Update application.conf or environment variable
export UDPS_DB_PASSWORD="$UDPS_DB_PASSWORD"

# Example for Stream Compute (Rust service)
# Update .env or config.toml
export STREAM_DB_PASSWORD="$STREAM_DB_PASSWORD"
```

**Test each service connection:**
```bash
# USP
cd services/usp/src/USP.API
dotnet run &
sleep 10
curl -k https://localhost:5001/health
kill %1

# Repeat for UCCP, NCCS, UDPS, Stream Compute when implemented
```

### Step 9: Commit Changes (15 minutes)

```bash
cd /home/tshepo/projects/tw

# Verify no hardcoded passwords in SQL
grep -i "password.*'.*_dev_password" config/postgres/init-scripts/02-create-roles.sql
# Expected: No output

# Add changes
git add config/postgres/init-scripts/02-create-roles.sql
git add config/postgres/init-db.sh

# Commit
git commit -m "Parameterize database passwords in PostgreSQL init scripts

- Remove 5 hardcoded passwords from 02-create-roles.sql
- Add psql variable placeholders for passwords (:'variable_name')
- Create init-db.sh wrapper to inject passwords from environment
- Update Docker Compose to pass password environment variables

Resolves: SEC-P0-003 - Hardcoded Passwords in SQL Init Scripts

Security Impact:
- Eliminates plaintext database passwords in Git
- Enables password rotation without code changes
- Supports different passwords per environment (dev/staging/prod)

🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

### Step 10: Update Documentation (20 minutes)

**Update `/home/tshepo/projects/tw/docs/GETTING_STARTED.md`:**

Add section on database setup:

```markdown
## Database Setup

The platform uses PostgreSQL with separate databases for each service (UCCP, NCCS, USP, UDPS, Stream Compute).

### Configure Database Passwords

1. Database passwords are managed via environment variables (NOT hardcoded in SQL scripts)

2. Add database passwords to your `.env` file:
   ```bash
   # Generate strong passwords
   UCCP_DB_PASSWORD=$(openssl rand -base64 32 | tr -d "=+/" | cut -c1-32)
   NCCS_DB_PASSWORD=$(openssl rand -base64 32 | tr -d "=+/" | cut -c1-32)
   USP_DB_PASSWORD=$(openssl rand -base64 32 | tr -d "=+/" | cut -c1-32)
   UDPS_DB_PASSWORD=$(openssl rand -base64 32 | tr -d "=+/" | cut -c1-32)
   STREAM_DB_PASSWORD=$(openssl rand -base64 32 | tr -d "=+/" | cut -c1-32)

   # Add to .env file
   echo "UCCP_DB_PASSWORD=$UCCP_DB_PASSWORD" >> .env
   # ... (repeat for all passwords)
   ```

3. Initialize database:
   ```bash
   docker-compose up -d postgres
   ```

4. Verify databases created:
   ```bash
   docker-compose exec postgres psql -U postgres -c "\l"
   ```

**Security:** Never commit database passwords to Git. Use .env (local) or USP Vault (production).
```

**Commit documentation:**
```bash
git add docs/GETTING_STARTED.md
git commit -m "Add database setup instructions with parameterized passwords

- Document environment variable configuration for database passwords
- Add password generation instructions
- Explain database initialization process

Related to: SEC-P0-003"
```

### Step 11: Cleanup Sensitive Files (5 minutes)

```bash
# Securely delete password file
shred -uvz /tmp/db-passwords-*.txt 2>/dev/null || rm -f /tmp/db-passwords-*.txt

# Delete backup SQL script
rm config/postgres/init-scripts/02-create-roles.sql.backup

# Clear history
history -c
history -w
```

---

## 7. Testing Strategy

### Unit Tests

**Test 1: Verify No Hardcoded Passwords in SQL**

```bash
#!/usr/bin/env bash
# Test: SEC-P0-003-test-no-hardcoded-sql-passwords.sh

set -e

echo "Testing: No hardcoded passwords in SQL scripts"

if grep -i "password.*'.*_dev_password" config/postgres/init-scripts/*.sql; then
  echo "FAIL: Hardcoded passwords found in SQL scripts"
  exit 1
fi

echo "PASS: No hardcoded passwords in SQL scripts"
```

**Test 2: Verify SQL Scripts Use Parameterized Variables**

```bash
#!/usr/bin/env bash
# Test: SEC-P0-003-test-parameterized-variables.sh

set -e

echo "Testing: SQL scripts use parameterized variables"

if ! grep -q "PASSWORD :" config/postgres/init-scripts/02-create-roles.sql; then
  echo "FAIL: SQL scripts do not use parameterized variables"
  exit 1
fi

# Verify 5 password variables (one per service)
COUNT=$(grep -c "PASSWORD :" config/postgres/init-scripts/02-create-roles.sql)
if [ "$COUNT" -ne 5 ]; then
  echo "FAIL: Expected 5 password variables, found $COUNT"
  exit 1
fi

echo "PASS: SQL scripts use parameterized variables"
```

### Integration Tests

**Test 3: Database Initialization with Parameterized Passwords**

```bash
#!/usr/bin/env bash
# Test: SEC-P0-003-test-db-init.sh

set -e

echo "Testing: Database initialization with parameterized passwords"

# Stop and remove existing database
docker-compose stop postgres
docker-compose rm -f postgres
docker volume rm tw_postgres_data || true

# Set test passwords
export UCCP_DB_PASSWORD="test_uccp_$(openssl rand -hex 16)"
export NCCS_DB_PASSWORD="test_nccs_$(openssl rand -hex 16)"
export USP_DB_PASSWORD="test_usp_$(openssl rand -hex 16)"
export UDPS_DB_PASSWORD="test_udps_$(openssl rand -hex 16)"
export STREAM_DB_PASSWORD="test_stream_$(openssl rand -hex 16)"

# Start PostgreSQL
docker-compose up -d postgres
sleep 30

# Verify users created
USERS=$(docker-compose exec -T postgres psql -U postgres -t -c "SELECT usename FROM pg_user WHERE usename IN ('uccp_user', 'nccs_user', 'usp_user', 'udps_user', 'stream_user');" | wc -l)

if [ "$USERS" -lt 5 ]; then
  echo "FAIL: Expected 5 database users, found $USERS"
  docker-compose down
  exit 1
fi

# Verify can connect with new password
if ! docker-compose exec -T postgres psql -U usp_user -d usp_db -c "SELECT 1" &>/dev/null; then
  echo "FAIL: Cannot connect to database with new password"
  docker-compose down
  exit 1
fi

docker-compose down
echo "PASS: Database initialization successful with parameterized passwords"
```

---

## 8. Rollback Plan

**Rollback Steps:**

1. Restore backup SQL script:
   ```bash
   cp config/postgres/init-scripts/02-create-roles.sql.backup config/postgres/init-scripts/02-create-roles.sql
   ```

2. Remove init-db.sh if causing issues:
   ```bash
   rm config/postgres/init-db.sh
   ```

3. Restore database from backup:
   ```bash
   cat /tmp/postgres-backup-*.sql | docker-compose exec -T postgres psql -U postgres
   ```

---

## 9. Monitoring & Validation

**Metrics:**
- `sql_hardcoded_passwords_found` - Counter (target: 0)
- `database_password_age_days` - Gauge (target: <90)

**Alerts:**
```yaml
- alert: HardcodedSQLPasswords
  expr: sql_hardcoded_passwords_found > 0
  for: 1m
  labels:
    severity: critical
```

---

## 10. Post-Implementation Validation

**Day 0:**
- [ ] SQL scripts parameterized
- [ ] Database initialization successful
- [ ] All services connect with new passwords

**Week 1:**
- [ ] No hardcoded passwords in SQL scripts
- [ ] Password rotation procedure documented

**Month 1:**
- [ ] All database passwords rotated (90-day schedule)

---

## 11. Documentation Updates

- `GETTING_STARTED.md` - Database setup instructions
- `DEPLOYMENT.md` - Production database password management

---

## 12. Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| Database init fails with parameters | High | Test thoroughly, have rollback ready |
| Service connections break | High | Update all service configs, test before commit |

---

## 13. Compliance Evidence

**SOC 2:** Parameterized SQL scripts, password rotation logs
**HIPAA:** Database password protection, access controls
**PCI-DSS:** Strong passwords (32+ chars), no plaintext storage

---

## 14. Sign-Off

- [ ] **Developer:** Implementation complete
- [ ] **DBA:** Database initialization verified
- [ ] **Security Engineer:** Security review passed

---

## 15. Appendix

### Related Documentation

- [SEC-P0-001](SEC-P0-001-hardcoded-env-secrets.md) - .env secrets
- [SEC-P1-011](../P1-HIGH/SEC-P1-011-sql-parameterized-passwords.md) - SQL password parameters

### Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-12-27 | Security Audit Team | Initial version |

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P0-003 Finding Document**

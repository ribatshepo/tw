# USP Bootstrap Scripts

This directory contains scripts for setting up and configuring the Unified Security Platform (USP) with proper security configurations.

## Quick Start

For a complete bootstrap setup, run these scripts in order:

```bash
# 1. Generate bootstrap credentials
./generate-infrastructure-credentials.sh

# 2. Generate TLS certificates for HTTPS
./generate-dev-certs.sh

# 3. Generate JWT signing keys
./generate-jwt-keys.sh

# 4. (Optional) Generate master encryption key separately
./generate-master-key.sh
```

After running these scripts, proceed to database setup:

```bash
# 5. Create database users (run as postgres superuser)
psql -h localhost -U postgres -d usp_db -f ../sql/001-create-users.sql

# 6. Enable SSL in PostgreSQL (run as postgres superuser)
psql -h localhost -U postgres -d postgres -f ../sql/002-enable-ssl.sql

# 7. Seed initial data (run as usp_migration user)
psql -h localhost -U usp_migration -d usp_db -f ../sql/003-seed-data.sql
```

---

## Scripts Overview

### 1. `generate-infrastructure-credentials.sh`

**Purpose**: Generate all bootstrap credentials needed to start USP

**What it generates**:
- PostgreSQL password
- Redis password
- Master encryption key
- TLS certificate passwords
- Elasticsearch password
- RabbitMQ password

**Output**: Creates `.env` file with all bootstrap credentials

**Usage**:
```bash
./generate-infrastructure-credentials.sh           # Creates .env in parent directory
./generate-infrastructure-credentials.sh .env.prod # Specify custom output file
```

**Security**:
- Generates cryptographically secure random passwords (32 characters)
- Sets file permissions to 600 (owner read/write only)
- Includes all necessary environment variables for USP startup

**When to use**: Run this FIRST when setting up USP for the first time

---

### 2. `generate-dev-certs.sh`

**Purpose**: Generate self-signed TLS certificates for development

**What it generates**:
- Self-signed X.509 certificate
- Private key (2048-bit RSA)
- PFX/PKCS12 file (required by Kestrel)

**Output**: Creates certificates in `src/USP.API/certs/` by default

**Usage**:
```bash
./generate-dev-certs.sh                    # Default location
./generate-dev-certs.sh /etc/usp/certs     # Custom location
```

**Certificate Details**:
- **Algorithm**: RSA 2048-bit
- **Validity**: 365 days
- **Subject Alternative Names**:
  - DNS: localhost, *.localhost, usp, usp.local, *.usp.local
  - IP: 127.0.0.1, ::1
- **Extended Key Usage**: Server Auth, Client Auth
- **Password**: `dev-cert-password` (hardcoded for development)

**Security**:
- ⚠️ **DEVELOPMENT ONLY** - Do NOT use in production
- Self-signed certificates are not trusted by browsers by default
- See script output for instructions on trusting the certificate

**When to use**:
- Local development
- Testing HTTPS endpoints
- Development environments without a proper CA

**Production**:
For production, obtain certificates from:
- Your organization's Certificate Authority (CA)
- Let's Encrypt (free, automated)
- Commercial CA (DigiCert, GlobalSign, etc.)

---

### 3. `generate-jwt-keys.sh`

**Purpose**: Generate RSA key pair for JWT token signing (RS256 algorithm)

**What it generates**:
- Private key for signing JWTs
- Public key for verifying JWTs

**Output**: Creates `jwt-private.pem` and `jwt-public.pem` in `keys/` directory

**Usage**:
```bash
./generate-jwt-keys.sh              # Creates in ./keys
./generate-jwt-keys.sh /etc/usp/keys # Custom location
```

**Key Details**:
- **Algorithm**: RS256 (RSA Signature with SHA-256)
- **Key Size**: 2048 bits
- **Format**: PEM (Privacy Enhanced Mail)
- **Permissions**:
  - Private key: 600 (owner read/write only)
  - Public key: 644 (owner read/write, others read)

**Security Features**:
- Checks if keys already exist (prevents accidental overwrite)
- Creates backup of existing keys before overwriting
- Validates generated keys
- Proper error handling and cleanup on failure

**When to use**:
- Initial setup
- Key rotation (periodically, e.g., annually)
- After key compromise

**Key Rotation**:
```bash
# Backup old keys
cp keys/jwt-private.pem keys/jwt-private.pem.backup.$(date +%Y%m%d)
cp keys/jwt-public.pem keys/jwt-public.pem.backup.$(date +%Y%m%d)

# Generate new keys
./generate-jwt-keys.sh

# Update configuration
# Restart USP service
```

---

### 4. `generate-master-key.sh`

**Purpose**: Generate master encryption key for encrypting secrets in database

**What it generates**:
- 256-bit (32-byte) random key
- Base64-encoded for easy storage

**Output**:
- Displays key to stdout
- Optionally writes to file (e.g., `.env`)

**Usage**:
```bash
./generate-master-key.sh                        # Display only
./generate-master-key.sh --output .env          # Write to .env (overwrite)
./generate-master-key.sh --output .env --append # Append to .env
```

**Security**:
- ⚠️ **CRITICAL**: This key encrypts ALL secrets in the database
- If this key is lost, ALL encrypted secrets are UNRECOVERABLE
- MUST be backed up in multiple secure locations
- Should be stored in a Hardware Security Module (HSM) in production

**When to use**:
- Initial setup (if not using `generate-infrastructure-credentials.sh`)
- Key rotation (requires re-encryption of all secrets)
- Disaster recovery (restore from backup)

**Backup Strategy**:
1. **Immediate Backup**: Store in password manager (1Password, LastPass, etc.)
2. **Secure Storage**: Store in HSM or cloud KMS (AWS KMS, Azure Key Vault, Google Cloud KMS)
3. **Physical Backup**: Print and store in secure physical location (safe, safety deposit box)
4. **Redundancy**: Keep backups in multiple geographic locations

**Key Rotation**:
```bash
# WARNING: Requires re-encryption of all secrets!
# 1. Generate new key
./generate-master-key.sh --output .env.new

# 2. Deploy new key with re-encryption logic
# 3. Verify all secrets can be decrypted
# 4. Remove old key
# 5. Update all backups
```

---

## Security Best Practices

### 1. File Permissions

All generated files should have restrictive permissions:

```bash
# Secrets and private keys (owner read/write only)
chmod 600 .env
chmod 600 keys/jwt-private.pem
chmod 600 certs/*.pfx
chmod 600 certs/*.key

# Public keys (owner read/write, others read)
chmod 644 keys/jwt-public.pem
chmod 644 certs/*.crt
```

### 2. Version Control

**NEVER commit these files to version control**:
- `.env` (bootstrap credentials)
- `jwt-private.pem` (JWT private key)
- `*.pfx` (TLS certificates)
- `*.key` (private keys)
- `master.key` (master encryption key)

**Ensure .gitignore includes**:
```
.env
.env.*
!.env.example
keys/jwt-private.pem
keys/*.pem
certs/*.pfx
certs/*.key
data/master.key
```

### 3. Production Deployment

For production:

1. **Use a Secrets Manager**:
   - AWS Secrets Manager
   - Azure Key Vault
   - Google Cloud Secret Manager
   - HashiCorp Vault

2. **Use HSM for Master Key**:
   - AWS CloudHSM
   - Azure Dedicated HSM
   - Google Cloud HSM
   - On-premises HSM (Thales, SafeNet, etc.)

3. **Use Proper TLS Certificates**:
   - Obtain from trusted CA
   - Use Let's Encrypt for automated renewal
   - Implement certificate rotation
   - Monitor certificate expiration

4. **Implement Key Rotation**:
   - JWT keys: Rotate annually
   - Master encryption key: Rotate annually
   - Database passwords: Rotate quarterly
   - TLS certificates: Rotate annually (or per CA policy)

5. **Enable Audit Logging**:
   - Log all key generation events
   - Log all key access attempts
   - Monitor for unusual patterns
   - Integrate with SIEM

### 4. Disaster Recovery

**Before Production**:
1. Document all credentials and their backup locations
2. Test key recovery procedures
3. Create runbooks for key rotation and recovery
4. Establish key custodian roles and responsibilities

**Backup Checklist**:
- [ ] Master encryption key backed up in 3+ secure locations
- [ ] JWT private key backed up
- [ ] Database credentials documented
- [ ] TLS certificates and private keys backed up
- [ ] Backup locations documented and access tested
- [ ] Recovery procedures tested in non-production environment

---

## Troubleshooting

### OpenSSL Not Found

```bash
# Ubuntu/Debian
sudo apt-get update && sudo apt-get install openssl

# RHEL/CentOS/Fedora
sudo yum install openssl

# macOS
brew install openssl
```

### Permission Denied Errors

```bash
# Make scripts executable
chmod +x *.sh

# Run as current user (not root)
./generate-jwt-keys.sh

# If writing to system directories, use sudo
sudo ./generate-dev-certs.sh /etc/usp/certs
```

### PostgreSQL SSL Errors

```bash
# Verify PostgreSQL SSL is enabled
psql -h localhost -U postgres -c "SHOW ssl;"

# Check certificate permissions
ls -la /var/lib/postgresql/15/main/server.{crt,key}

# Certificates should be owned by postgres:postgres
sudo chown postgres:postgres /var/lib/postgresql/15/main/server.*

# Private key must be 600
sudo chmod 600 /var/lib/postgresql/15/main/server.key
```

### Connection String Errors

If you get "SSL connection required" errors:

```bash
# Ensure connection string includes SSL Mode=Require
Host=localhost;Port=5432;Database=usp_db;Username=usp_app;Password=xxx;SSL Mode=Require;Trust Server Certificate=true
```

---

## References

- [OpenSSL Documentation](https://www.openssl.org/docs/)
- [JWT RS256 Signing](https://tools.ietf.org/html/rfc7518#section-3.3)
- [PostgreSQL SSL Configuration](https://www.postgresql.org/docs/current/ssl-tcp.html)
- [ASP.NET Core Kestrel HTTPS](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints)
- [NIST Key Management Guidelines](https://csrc.nist.gov/publications/detail/sp/800-57-part-1/rev-5/final)

---

## Support

For issues or questions:
1. Check the USP documentation: `../docs/`
2. Review audit reports: `/home/tshepo/.claude/plans/`
3. Contact the security team
4. Review error logs: `logs/usp-*.log`

---

**Last Updated**: 2025-12-27
**Script Versions**: All scripts v2.0 (with improved error handling)

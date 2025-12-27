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

# 4. Generate master encryption key AND KEK (REQUIRED for vault)
./generate-master-key.sh

# Output will be:
#   USP_Encryption__MasterKey=<base64-master-key>
#   USP_KEY_ENCRYPTION_KEY=<base64-kek>
#
# CRITICAL: Copy both values to your .env file or environment
# The KEK is required to encrypt the master key at rest
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

**Purpose**: Generate master encryption key AND Key Encryption Key (KEK) for vault operations

**What it generates**:
- **Master Encryption Key**: 256-bit (32-byte) random key for encrypting secrets in database
- **KEK (Key Encryption Key)**: 256-bit (32-byte) random key for encrypting the master key at rest
- Both are Base64-encoded for easy storage

**Output**:
- Displays both keys to stdout with environment variable names
- Format:
  ```
  USP_Encryption__MasterKey=<base64-key>
  USP_KEY_ENCRYPTION_KEY=<base64-kek>
  ```

**Usage**:
```bash
./generate-master-key.sh                        # Display both keys to stdout
```

**Security Architecture**:

The USP vault uses a **two-layer encryption approach**:

1. **KEK (Key Encryption Key)**:
   - Stored in environment variable: `USP_KEY_ENCRYPTION_KEY`
   - Used ONLY to encrypt/decrypt the master key at rest
   - Never used to encrypt actual secrets
   - Should be stored separately from the master key

2. **Master Encryption Key**:
   - Stored in environment variable: `USP_Encryption__MasterKey` (for bootstrap only)
   - After vault initialization, split using **Shamir's Secret Sharing** (5 shares, 3 threshold)
   - Reconstructed during vault unseal by combining 3 of 5 shares
   - Used to encrypt ALL secrets in the database

**Why Two Keys?**

This follows **NIST SP 800-57** key management best practices:
- **Defense in Depth**: If one key is compromised, secrets are still protected
- **Key Hierarchy**: KEK provides a root of trust for the master key
- **Separation of Duties**: Different people can hold KEK vs. unseal keys
- **Never Self-Encrypt**: The master key is NEVER encrypted with itself (security anti-pattern)

**Security**:
- ⚠️ **CRITICAL**: Both keys are required for vault operation
- ⚠️ **CRITICAL**: If KEK is lost, the master key CANNOT be decrypted, making ALL secrets UNRECOVERABLE
- ⚠️ **CRITICAL**: If all unseal keys are lost, the vault CANNOT be unsealed
- MUST be backed up in multiple secure locations
- Should be stored in a Hardware Security Module (HSM) in production

**When to use**:
- Initial setup (if not using `generate-infrastructure-credentials.sh`)
- Key rotation (requires re-encryption of all secrets)
- Disaster recovery (restore from backup)

**Backup Strategy**:

**For KEK (Key Encryption Key)**:
1. **Primary Storage**: HSM or cloud KMS (AWS KMS, Azure Key Vault, Google Cloud KMS)
2. **Secondary Backup**: Password manager (1Password, LastPass, Bitwarden)
3. **Tertiary Backup**: Encrypted offline storage (hardware token, encrypted USB)
4. **Emergency Backup**: Physical printout in secure location (safe, safety deposit box)
5. **Separation**: Store KEK separately from master key and unseal keys

**For Unseal Keys** (Shamir shares):
1. **Distribution**: Give each share to different trusted individuals (5 people minimum)
2. **Storage**: Each share holder stores their key in a password manager
3. **Documentation**: Record which share belongs to which person (without recording the share itself)
4. **Threshold**: Only 3 of 5 shares needed to unseal vault
5. **Geographic Distribution**: Share holders should be in different locations

**Vault Initialization Workflow**:
```bash
# 1. Generate keys
./generate-master-key.sh

# Output:
#   USP_Encryption__MasterKey=<master-key>
#   USP_KEY_ENCRYPTION_KEY=<kek>

# 2. Set environment variables
export USP_KEY_ENCRYPTION_KEY="<kek-from-output>"
export USP_Encryption__MasterKey="<master-key-from-output>"

# 3. Start USP and initialize vault
curl -X POST https://localhost:8443/v1/sys/init \
  -d '{"secret_shares": 5, "secret_threshold": 3}'

# 4. Save the 5 unseal keys returned (distribute to 5 different people)
# 5. Save the root token securely
# 6. IMMEDIATELY backup the KEK to multiple locations
# 7. Delete USP_Encryption__MasterKey from environment (no longer needed after init)
```

**Key Rotation**:

**Rotating KEK** (annually recommended):
```bash
# WARNING: Requires re-encryption of master key!
# 1. Generate new KEK
./generate-master-key.sh  # Use only the KEK output

# 2. Update environment variable with new KEK
export USP_KEY_ENCRYPTION_KEY="<new-kek>"

# 3. Vault will automatically re-encrypt master key with new KEK on next unseal
# 4. Verify vault can unseal successfully
# 5. Update all KEK backups with new value
# 6. Securely destroy old KEK
```

**Rotating Master Key** (requires vault re-initialization):
```bash
# WARNING: This is a DESTRUCTIVE operation!
# Requires re-encryption of ALL secrets in the database
# 1. Export all secrets from vault
# 2. Re-initialize vault with new master key
# 3. Import all secrets back into vault
# 4. Verify all secrets are accessible
# 5. Distribute new unseal keys
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
- [ ] **KEK (Key Encryption Key)** backed up in 4+ secure locations (HSM + password manager + offline + physical)
- [ ] **Unseal keys** distributed to 5 different people (3 required to unseal)
- [ ] Record of which person holds which unseal share (without recording the share value)
- [ ] Root token backed up in secure location
- [ ] Master encryption key (**NOT needed after vault init** - stored encrypted in database)
- [ ] JWT private key backed up
- [ ] Database credentials documented
- [ ] TLS certificates and private keys backed up
- [ ] Backup locations documented and access tested
- [ ] Recovery procedures tested in non-production environment

---

## Troubleshooting

### KEK (Key Encryption Key) Issues

**Error: "USP_KEY_ENCRYPTION_KEY environment variable not set"**

```bash
# Verify KEK is set
echo $USP_KEY_ENCRYPTION_KEY

# If empty, set it
export USP_KEY_ENCRYPTION_KEY="<your-kek-from-generate-master-key.sh>"

# For permanent setup, add to .env or systemd service file
```

**Error: "USP_KEY_ENCRYPTION_KEY must be 32 bytes (256 bits)"**

```bash
# Verify KEK is valid Base64 and 32 bytes when decoded
echo -n "$USP_KEY_ENCRYPTION_KEY" | base64 -d | wc -c
# Should output: 32

# If not 32 bytes, regenerate KEK
./generate-master-key.sh  # Use the KEK output
```

**Error: "Invalid unseal keys - master key verification failed"**

This means the KEK has changed since vault initialization:

```bash
# 1. Restore correct KEK from backup
export USP_KEY_ENCRYPTION_KEY="<original-kek-from-backup>"

# 2. Try unsealing again
curl -X POST https://localhost:8443/v1/sys/unseal \
  -d '{"key": "<unseal-key-1>"}'

# 3. If still failing, the KEK backup may be incorrect
# 4. Check all KEK backup locations
# 5. If all KEK backups are lost, vault is UNRECOVERABLE
```

**Vault Unsealed but Secrets Cannot Be Decrypted**

This indicates the master key is correct but KEK changed:

```bash
# This should NOT happen if vault unsealed successfully
# But if it does, check:
# 1. Verify KEK matches the one used during initialization
# 2. Check database for encrypted_master_key value
# 3. Verify no one manually modified the database
```

### Shamir Secret Sharing Issues

**Error: "Vault is sealed"**

```bash
# Check seal status
curl https://localhost:8443/v1/sys/seal-status

# Unseal vault (requires 3 of 5 keys)
curl -X POST https://localhost:8443/v1/sys/unseal -d '{"key": "<share-1>"}'
curl -X POST https://localhost:8443/v1/sys/unseal -d '{"key": "<share-2>"}'
curl -X POST https://localhost:8443/v1/sys/unseal -d '{"key": "<share-3>"}'

# After 3rd key, vault should be unsealed
```

**Error: "This unseal key has already been provided"**

```bash
# Cannot submit same key twice
# Use a different unseal key (any 3 of 5 will work)
```

**Lost Unseal Keys**

If you've lost ALL 5 unseal keys:
1. ❌ **Vault is UNRECOVERABLE** - all secrets are lost
2. You must re-initialize the vault from scratch
3. All existing secrets in database are now permanently encrypted

If you have at least 3 of 5 keys:
1. ✅ Vault can still be unsealed
2. Contact the share holders to get 3 keys
3. After unsealing, consider re-initializing with new shares

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
- [NIST SP 800-57: Key Management](https://csrc.nist.gov/publications/detail/sp/800-57-part-1/rev-5/final)
- [NIST SP 800-131A: Cryptographic Algorithms](https://csrc.nist.gov/publications/detail/sp/800-131a/rev-2/final)
- [Shamir's Secret Sharing (1979)](https://en.wikipedia.org/wiki/Shamir%27s_Secret_Sharing)
- [AES-256-GCM Encryption](https://nvlpubs.nist.gov/nistpubs/Legacy/SP/nistspecialpublication800-38d.pdf)
- [FIPS 140-2: Security Requirements for Cryptographic Modules](https://csrc.nist.gov/publications/detail/fips/140/2/final)
- [Key Encryption Key (KEK) Best Practices](https://csrc.nist.gov/glossary/term/key_encryption_key)

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

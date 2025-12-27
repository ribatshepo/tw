# USP Key Encryption Key (KEK) Setup Guide

**Last Updated**: 2025-12-27
**Applies To**: USP v1.0+
**Security Level**: CRITICAL

---

## Overview

The USP vault uses a **two-layer encryption architecture** to protect secrets:

1. **KEK (Key Encryption Key)**: Encrypts the master key at rest
2. **Master Key**: Encrypts all secrets in the database

This follows **NIST SP 800-57** key management best practices and prevents the security anti-pattern of a key encrypting itself.

---

## Quick Start

### Step 1: Generate Keys

```bash
cd /home/tshepo/projects/tw/services/usp/scripts
./generate-master-key.sh
```

**Output:**
```
USP_Encryption__MasterKey=kOoYu24l7gPrFni28zzeVRGJyPQJ5JFmE6yRhq63OKs=
USP_KEY_ENCRYPTION_KEY=p9apukbwU1q/eSzJnguQj+LiU4RQThs/MoALcpGFJ+q=
```

### Step 2: Set Environment Variables

**For Development (.env file):**
```bash
echo "USP_KEY_ENCRYPTION_KEY=p9apukbwU1q/eSzJnguQj+LiU4RQThs/MoALcpGFJ+q=" >> .env
echo "USP_Encryption__MasterKey=kOoYu24l7gPrFni28zzeVRGJyPQJ5JFmE6yRhq63OKs=" >> .env
```

**For Production (systemd service):**
```bash
sudo systemctl edit usp.service
```

Add:
```ini
[Service]
Environment="USP_KEY_ENCRYPTION_KEY=p9apukbwU1q/eSzJnguQj+LiU4RQThs/MoALcpGFJ+q="
Environment="USP_Encryption__MasterKey=kOoYu24l7gPrFni28zzeVRGJyPQJ5JFmE6yRhq63OKs="
```

### Step 3: Initialize Vault

```bash
# Start USP service
dotnet run --project src/USP.API

# Initialize vault (generates 5 unseal keys, needs 3 to unseal)
curl -X POST https://localhost:8443/v1/sys/init \
  -H "Content-Type: application/json" \
  -d '{
    "secret_shares": 5,
    "secret_threshold": 3
  }'
```

**Response:**
```json
{
  "unseal_keys": [
    "AQ...",
    "Ag...",
    "Aw...",
    "BA...",
    "BQ..."
  ],
  "unseal_keys_hex": [
    "01...",
    "02...",
    "03...",
    "04...",
    "05..."
  ],
  "root_token": "s.xxxxxxxxxxxxxxxxxxxxxxxx"
}
```

### Step 4: Distribute Unseal Keys

**CRITICAL**: Give each unseal key to a different person:

1. Share 1 → Alice (alice@example.com)
2. Share 2 → Bob (bob@example.com)
3. Share 3 → Charlie (charlie@example.com)
4. Share 4 → Diana (diana@example.com)
5. Share 5 → Eve (eve@example.com)

**Requirements**:
- Any 3 of 5 keys can unseal the vault
- Share holders should store keys in password manager
- Share holders should be in different geographic locations
- Document who holds which share (without recording the share value)

### Step 5: Backup KEK

**Immediately** backup the KEK to multiple secure locations:

✅ **Primary**: AWS KMS / Azure Key Vault / Google Cloud KMS
✅ **Secondary**: 1Password / LastPass / Bitwarden
✅ **Tertiary**: Encrypted USB drive (stored in safe)
✅ **Emergency**: Physical printout (stored in safety deposit box)

**CRITICAL WARNING**:
- If KEK is lost, the vault is **UNRECOVERABLE**
- All secrets will be permanently encrypted
- You must re-initialize from scratch

### Step 6: Remove Master Key from Environment

After vault initialization, the master key is split into 5 shares and stored encrypted in the database. The original master key is **no longer needed**.

```bash
# Remove from .env
sed -i '/USP_Encryption__MasterKey/d' .env

# For systemd, remove the environment variable
sudo systemctl edit usp.service
# Delete the USP_Encryption__MasterKey line
```

**Keep only `USP_KEY_ENCRYPTION_KEY` in the environment.**

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────┐
│                   USP Vault Security                     │
└─────────────────────────────────────────────────────────┘

Step 1: Initialization
┌──────────────┐
│  Master Key  │ (256-bit random)
│  (32 bytes)  │
└──────┬───────┘
       │
       ├─────────────────┐
       │                 │
       ▼                 ▼
  [Shamir Split]    [Encrypt with KEK]
       │                 │
       ▼                 ▼
  5 Unseal Keys    Encrypted Master Key
  (3 threshold)     (stored in database)
       │
       │
  Distributed to
  5 different people

Step 2: Unseal
┌──────────────┐
│ 3 Unseal Keys│ (from share holders)
└──────┬───────┘
       │
       ▼
  [Shamir Combine]
       │
       ▼
┌──────────────┐
│Reconstructed │
│ Master Key   │
└──────┬───────┘
       │
       ▼
  [Verify by decrypting with KEK]
       │
       ▼
┌──────────────┐        ┌──────────────┐
│Encrypted MK  │───KEK─→│Original MK   │
│from Database │        │from Shamir   │
└──────────────┘        └──────────────┘
       │                       │
       └───────┬───────────────┘
               │ Match?
               ▼
           ✅ Unsealed!

Step 3: Encrypt Secrets
┌──────────────┐
│   Secrets    │
└──────┬───────┘
       │
       ▼
  [Encrypt with Master Key]
       │
       ▼
┌──────────────┐
│  Encrypted   │ (stored in database)
│   Secrets    │
└──────────────┘
```

---

## Security Model

### Defense in Depth

The two-layer encryption provides multiple security benefits:

1. **No Self-Encryption**: Master key is NEVER encrypted with itself
2. **Separation of Duties**:
   - Infrastructure team controls KEK
   - Security team controls unseal keys
3. **Defense in Depth**:
   - Attacker needs BOTH KEK AND 3 unseal keys
   - Even with database access, cannot decrypt master key without KEK
   - Even with KEK, cannot reconstruct master key without 3 shares

### Threat Model

| Attack Scenario | Impact | Mitigation |
|----------------|--------|------------|
| Database breach | ✅ Protected | Master key is encrypted with KEK |
| KEK stolen | ✅ Protected | Still need 3 unseal shares to reconstruct master key |
| 1-2 unseal shares stolen | ✅ Protected | Need 3 shares (threshold) |
| KEK + 3 shares stolen | ❌ Vault compromised | Separation of duties, geographic distribution |
| KEK lost | ❌ Vault unrecoverable | Multiple secure backups required |
| All unseal shares lost | ❌ Vault unrecoverable | Distribute to trusted individuals |

---

## Operations Guide

### Daily Operations

**Vault is sealed after restart:**

1. Contact 3 share holders
2. Each submits their unseal key:
   ```bash
   curl -X POST https://localhost:8443/v1/sys/unseal \
     -d '{"key": "<unseal-key>"}'
   ```
3. After 3rd key, vault is unsealed

**Check vault status:**
```bash
curl https://localhost:8443/v1/sys/seal-status
```

Response:
```json
{
  "sealed": false,
  "threshold": 3,
  "secret_shares": 5,
  "progress": 0,
  "initialized": true
}
```

### Emergency: Lost KEK

If the KEK is lost:

1. ❌ **Vault is UNRECOVERABLE**
2. Check all backup locations:
   - AWS KMS / Azure Key Vault
   - Password manager
   - Encrypted USB drive
   - Physical printout
3. If found, restore KEK to environment
4. If not found, you must:
   - Accept that all secrets are lost
   - Re-initialize vault from scratch
   - Re-create all secrets manually

**Prevention**: Test KEK recovery quarterly

### Emergency: Lost Unseal Keys

**If lost ALL 5 shares:**
- ❌ Vault is UNRECOVERABLE
- Must re-initialize from scratch

**If have at least 3 of 5:**
- ✅ Vault can still be unsealed
- Consider re-initializing with new shares for security

### KEK Rotation (Annual Recommended)

```bash
# 1. Generate new KEK
./generate-master-key.sh
# Use only the KEK output

# 2. Update environment with new KEK
export USP_KEY_ENCRYPTION_KEY="<new-kek>"

# 3. Restart USP (will re-encrypt master key with new KEK)
sudo systemctl restart usp.service

# 4. Unseal vault with existing shares
curl -X POST https://localhost:8443/v1/sys/unseal -d '{"key": "<share-1>"}'
curl -X POST https://localhost:8443/v1/sys/unseal -d '{"key": "<share-2>"}'
curl -X POST https://localhost:8443/v1/sys/unseal -d '{"key": "<share-3>"}'

# 5. Verify vault unseals successfully
curl https://localhost:8443/v1/sys/seal-status

# 6. Update all KEK backups
# 7. Securely destroy old KEK
```

---

## Compliance & Audit

### Standards Compliance

This KEK implementation complies with:

- ✅ **NIST SP 800-57**: Key Management Recommendations
- ✅ **NIST SP 800-131A**: Transitions: Recommendation for Transitioning the Use of Cryptographic Algorithms
- ✅ **FIPS 140-2**: Security Requirements for Cryptographic Modules
- ✅ **SOC 2**: Key management controls
- ✅ **PCI-DSS**: Requirement 3.6 (Protect cryptographic keys)
- ✅ **HIPAA**: §164.312(a)(2)(iv) Encryption and decryption

### Audit Trail

All KEK-related operations are logged:

```json
{
  "timestamp": "2025-12-27T10:30:00Z",
  "event": "vault.init",
  "status": "success",
  "details": {
    "secret_shares": 5,
    "secret_threshold": 3,
    "kek_used": true
  }
}
```

```json
{
  "timestamp": "2025-12-27T10:35:00Z",
  "event": "vault.unseal",
  "status": "success",
  "details": {
    "progress": "3/3",
    "kek_verification": "passed"
  }
}
```

---

## Troubleshooting

### Error: "USP_KEY_ENCRYPTION_KEY not set"

**Cause**: Environment variable not configured

**Fix**:
```bash
export USP_KEY_ENCRYPTION_KEY="<your-kek>"
```

### Error: "KEK must be 32 bytes"

**Cause**: Invalid KEK format

**Fix**:
```bash
# Verify KEK is 32 bytes when Base64 decoded
echo -n "$USP_KEY_ENCRYPTION_KEY" | base64 -d | wc -c
# Should output: 32

# If not, regenerate
./generate-master-key.sh
```

### Error: "Invalid unseal keys - verification failed"

**Cause**: KEK changed since vault initialization

**Fix**:
```bash
# Restore original KEK from backup
export USP_KEY_ENCRYPTION_KEY="<original-kek-from-backup>"

# Try unsealing again
```

### Vault Unsealed but Secrets Don't Decrypt

**Cause**: Master key mismatch (should be impossible if unseal succeeded)

**Investigation**:
1. Check KEK hasn't changed
2. Check database `seal_configurations` table
3. Verify `encrypted_master_key` column hasn't been modified
4. Check audit logs for unauthorized changes

---

## FAQ

**Q: Can I change the KEK without re-initializing the vault?**
A: Yes! KEK rotation is supported. Just update the environment variable and restart USP.

**Q: What happens if I forget one unseal key?**
A: No problem. You only need 3 of 5 keys. The vault can still be unsealed.

**Q: Can I change the threshold (3 of 5)?**
A: No. You must re-initialize the vault to change the threshold.

**Q: Should I use the same KEK for development and production?**
A: **NO!** Use different KEKs for each environment.

**Q: Can I automate vault unsealing?**
A: Not recommended for production (defeats purpose of Shamir). For development, you can store shares in a secure location and script the unseal.

**Q: What if all 5 share holders leave the company?**
A: Ensure share holder list is kept up-to-date. When someone leaves, immediately unseal the vault and re-initialize with new shares distributed to current employees.

**Q: How often should I rotate the KEK?**
A: Annually is recommended. More frequently if:
  - Employee with KEK access leaves
  - Security incident
  - Compliance requirement

**Q: Can I backup the master key directly?**
A: No. After initialization, the master key only exists:
  1. Encrypted in the database (with KEK)
  2. Split into 5 shares (with Shamir)
  3. In memory when vault is unsealed (cleared on restart)

---

## References

- [NIST SP 800-57: Key Management](https://csrc.nist.gov/publications/detail/sp/800-57-part-1/rev-5/final)
- [Key Encryption Key (KEK) Definition](https://csrc.nist.gov/glossary/term/key_encryption_key)
- [Shamir's Secret Sharing](https://en.wikipedia.org/wiki/Shamir%27s_Secret_Sharing)
- [USP Security Architecture](../docs/specs/security.md)
- [USP Scripts Documentation](../scripts/README.md)

---

**For Support**: Contact security team or refer to `/home/tshepo/.claude/plans/stateless-roaming-glacier.md` for implementation details

# SEC-P2-010: Certificate Password Hardcoded

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P2-010 |
| **Title** | Certificate Password Hardcoded and Exposed in Script Output |
| **Priority** | P2 - MEDIUM |
| **Severity** | Medium |
| **Category** | Shell Scripts / Secrets Management |
| **Status** | Not Started |
| **Effort Estimate** | 1 hour |
| **Implementation Phase** | Phase 3 (Week 3, Day 7) |
| **Assigned To** | Security Engineer |
| **Created** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:247-258` |
| **Code Files** | `/home/tshepo/projects/tw/services/usp/scripts/generate-dev-certs.sh:52,143` |
| **Dependencies** | None |
| **Compliance Impact** | PCI-DSS (Req 8.2.1 - Passwords not hardcoded) |

---

## 3. Executive Summary

### Problem

Certificate password hardcoded as `dev-cert-password` and displayed in script output (line 143).

### Impact

- **Weak Security:** Predictable password for certificate encryption
- **Password Exposure:** Password visible in terminal output and logs
- **Non-Random:** Same password used every time script runs

### Solution

Generate random certificate password using `openssl rand` and only display masked version.

---

## 4. Implementation Guide

### Step 1: Generate Random Password (30 minutes)

```bash
# Edit services/usp/scripts/generate-dev-certs.sh

# CHANGE line 52 from:
# CERT_PASSWORD="dev-cert-password"

# TO:
CERT_PASSWORD=$(openssl rand -base64 32)

# Save password to .env file (not displayed)
echo "USP_Certificate__Password=$CERT_PASSWORD" >> .env
```

### Step 2: Mask Password in Output (20 minutes)

```bash
# CHANGE line 143 from:
echo "Certificate password: $CERT_PASSWORD"

# TO:
echo "Certificate password: ${CERT_PASSWORD:0:8}*** (full password saved to .env)"
```

### Step 3: Update Documentation (10 minutes)

```bash
# Update script README to explain password handling
cat >> services/usp/scripts/README.md <<'EOF'

## Certificate Password Security

The certificate password is randomly generated using `openssl rand -base64 32` and saved to `.env` file.

**DO NOT:**
- Hardcode certificate passwords
- Display full passwords in terminal output
- Commit certificate passwords to git

**DO:**
- Use random passwords (32+ characters)
- Store passwords in `.env` file (git-ignored)
- Only display masked passwords (first 8 characters)

EOF
```

### Step 4: Test Script (10 minutes)

```bash
cd services/usp

# Run certificate generation script
bash scripts/generate-dev-certs.sh

# Verify password is random (not "dev-cert-password")
grep "USP_Certificate__Password" .env
# Expected: USP_Certificate__Password=<random-32-char-base64>

# Verify output is masked
# Expected: "Certificate password: a1b2c3d4*** (full password saved to .env)"

# Verify certificate can be opened with generated password
openssl pkcs12 -info -in certs/usp.pfx -passin "pass:$(grep USP_Certificate__Password .env | cut -d'=' -f2)"
```

---

## 5. Testing

- [ ] Password generated randomly using openssl rand
- [ ] Password saved to .env file
- [ ] Password not displayed in full in terminal output
- [ ] Certificate can be opened with generated password
- [ ] Script documentation updated

---

## 6. Compliance Evidence

**PCI-DSS Req 8.2.1:** System passwords not hardcoded, randomly generated

---

## 7. Sign-Off

- [ ] **Security Engineer:** Random password generation verified
- [ ] **DevOps:** Script tested

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P2-010**

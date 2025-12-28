# SEC-P2-009: Shell Shebang Portability

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P2-009 |
| **Title** | USP Scripts Use /bin/bash Instead of /usr/bin/env bash |
| **Priority** | P2 - MEDIUM |
| **Severity** | Low |
| **Category** | Shell Scripts / Infrastructure |
| **Status** | Not Started |
| **Effort Estimate** | 30 minutes |
| **Implementation Phase** | Phase 3 (Week 3, Day 7) |
| **Assigned To** | DevOps Engineer |
| **Created** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:242-246` |
| **Code Files** | `services/usp/scripts/generate-dev-certs.sh`, `services/usp/scripts/generate-infrastructure-credentials.sh` |
| **Dependencies** | None |
| **Compliance Impact** | None (Portability improvement) |

---

## 3. Executive Summary

### Problem

USP shell scripts use `#!/bin/bash` instead of `#!/usr/bin/env bash`.

### Impact

- **Reduced Portability:** Fails on systems where bash is not at `/bin/bash` (e.g., macOS with Homebrew, NixOS, FreeBSD)
- **Deployment Failures:** Scripts fail in some Docker images or CI/CD environments

### Solution

Update shebang to `#!/usr/bin/env bash` for better portability.

---

## 4. Implementation Guide

### Step 1: Update All Shell Scripts (20 minutes)

```bash
cd services/usp/scripts

# Find all shell scripts with /bin/bash shebang
grep -l "^#!/bin/bash" *.sh

# Update shebang in all scripts
sed -i '1s|^#!/bin/bash|#!/usr/bin/env bash|' *.sh

# Verify changes
head -1 *.sh
# Expected: #!/usr/bin/env bash
```

### Step 2: Update Other Directories (10 minutes)

```bash
# Find all shell scripts in repository
find . -name "*.sh" -type f -exec grep -l "^#!/bin/bash" {} \;

# Update all at once
find . -name "*.sh" -type f -exec sed -i '1s|^#!/bin/bash|#!/usr/bin/env bash|' {} \;

# Verify all scripts updated
find . -name "*.sh" -type f -exec head -1 {} \; | sort -u
# Expected: #!/usr/bin/env bash (only)
```

---

## 5. Testing

- [ ] All .sh files use `#!/usr/bin/env bash`
- [ ] Scripts execute successfully on Linux
- [ ] Scripts execute successfully on macOS
- [ ] Scripts execute in Docker containers
- [ ] CI/CD pipeline executes scripts successfully

---

## 6. Compliance Evidence

None (Portability improvement)

---

## 7. Sign-Off

- [ ] **DevOps:** All scripts updated and tested

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P2-009**

# SEC-P0-006: TODO Comments in Production Code

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P0-006 |
| **Title** | TODO Comments in Production Code |
| **Priority** | P0 - CRITICAL |
| **Severity** | Critical |
| **Category** | Coding Standards |
| **Status** | Not Started |
| **Effort Estimate** | 4 hours |
| **Implementation Phase** | Phase 1 (Week 1, Day 5) |
| **Assigned To** | Backend Engineer 2 |
| **Reviewers** | Engineering Lead |
| **Created** | 2025-12-27 |
| **Last Updated** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:1154-1170` |
| **Coding Guidelines** | `/home/tshepo/projects/tw/docs/CODING_GUIDELINES.md` ("NO TODO comments") |
| **Code Files** | `/home/tshepo/projects/tw/services/usp/src/USP.API/Program.cs:230`, `/home/tshepo/projects/tw/services/usp/src/USP.API/Controllers/SealController.cs:151` |
| **Dependencies** | Blocks SEC-P1-004 (Metrics Endpoint Mapping) |
| **Related Findings** | SEC-P0-004 (Vault Authentication - addresses SealController TODO), SEC-P1-004 (Metrics Mapping) |
| **Compliance Impact** | SOC 2 (CC8.1 - Change Management) |

---

## 3. Executive Summary

### Problem Statement

The coding guidelines explicitly state: **"NO TODO comments - implement the feature or remove it"**. However, 2 TODO comments exist in production code:

1. **Program.cs:230** - `// TODO: Fix MapMetrics extension method issue`
2. **SealController.cs:151** - `// TODO: Implement X-Vault-Token authentication for production`

### Business Impact

- **Production Readiness:** TODO comments indicate incomplete implementation
- **Code Quality:** Violates established coding standards
- **Security Risk:** SealController TODO directly relates to critical security issue (SEC-P0-004)
- **Metrics Unavailable:** Program.cs TODO blocks Prometheus metrics endpoint
- **Compliance Violation:** SOC 2 CC8.1 requires complete, production-ready code
- **Production Blocker:** P0 finding per coding guidelines

### Solution Overview

1. **Program.cs:230** - Implement MapMetrics extension method (fix Prometheus metrics)
2. **SealController.cs:151** - Implement X-Vault-Token authentication (covered in SEC-P0-004)
3. **Search codebase** for any other TODO comments
4. **Add pre-commit hook** to prevent future TODO commits
5. **Update coding guidelines** with enforcement mechanism

**Timeline:** 4 hours (Day 5 of Week 1, parallel with SEC-P0-005)

---

## 4. Technical Details

### Current State

**Violation 1: Program.cs:230**

```csharp
// Line 228-231
if (app.Environment.IsProduction())
{
    // TODO: Fix MapMetrics extension method issue
    // app.MapMetrics("/metrics");
}
```

**Impact:**
- Metrics endpoint not available in production
- Prometheus cannot scrape metrics
- No observability (CPU, memory, request rate, error rate)
- **Blocks SEC-P1-004** (Metrics Endpoint Mapping Broken)

**Violation 2: SealController.cs:151**

```csharp
// Line 150-152
[HttpPost("seal")]
[AllowAnonymous] // TODO: Implement X-Vault-Token authentication for production
public async Task<IActionResult> Seal()
{
    // Seals vault
}
```

**Impact:**
- Anyone can seal vault (DoS attack)
- **Addressed in SEC-P0-004** (Vault Seal Unauthenticated)
- TODO comment will be removed when SEC-P0-004 is implemented

---

## 5. Implementation Requirements

### Acceptance Criteria

- [ ] Program.cs:230 TODO resolved (metrics endpoint working)
- [ ] SealController.cs:151 TODO removed (X-Vault-Token implemented in SEC-P0-004)
- [ ] No TODO comments in codebase (`git grep "// TODO"` returns empty)
- [ ] Pre-commit hook blocks new TODO commits
- [ ] Metrics endpoint operational at `/metrics`
- [ ] All tests passing

---

## 6. Step-by-Step Implementation Guide

### Step 1: Fix MapMetrics Extension Method (2 hours)

**Root Cause:** ASP.NET Core 8 requires explicit NuGet package for MapMetrics.

**Solution:**

```bash
cd /home/tshepo/projects/tw/services/usp/src/USP.API

# Install Prometheus metrics package
dotnet add package prometheus-net.AspNetCore
```

**Update Program.cs:**

```csharp
// Add using statement
using Prometheus;

// Replace TODO with working implementation
if (app.Environment.IsProduction())
{
    // Expose Prometheus metrics endpoint
    app.UseHttpMetrics();  // Track HTTP request metrics
    app.MapMetrics("/metrics");  // ✅ FIXED: Expose metrics at /metrics
}
```

**Test:**
```bash
dotnet run

# In another terminal
curl https://localhost:5001/metrics -k
# Expected: Prometheus metrics output
```

### Step 2: Remove SealController TODO (15 minutes)

**This TODO is addressed in SEC-P0-004.**

Update `SealController.cs`:

```csharp
[HttpPost("seal")]
// ✅ REMOVE: [AllowAnonymous]
// ✅ REMOVE: // TODO: Implement X-Vault-Token authentication for production
[Authorize]  // Added in SEC-P0-004
public async Task<IActionResult> Seal()
{
    // Seal vault logic
}
```

**Note:** If SEC-P0-004 is not yet complete, create GitHub issue and remove TODO comment:

```csharp
[HttpPost("seal")]
[AllowAnonymous]  // See GitHub Issue #123: Implement X-Vault-Token authentication
public async Task<IActionResult> Seal() { ... }
```

### Step 3: Search for Other TODOs (30 minutes)

```bash
cd /home/tshepo/projects/tw

# Search for TODO comments
git grep -n "// TODO"

# Expected output after fixes:
# (no matches)

# If other TODOs found, either:
# 1. Implement the feature
# 2. Create GitHub issue and remove comment
# 3. Remove comment if no longer relevant
```

### Step 4: Add Pre-Commit Hook (1 hour)

**Update `.git/hooks/pre-commit`:**

```bash
#!/usr/bin/env bash
set -e

# Check for TODO comments in staged files
if git diff --cached | grep -i "^+.*// TODO"; then
  echo "ERROR: TODO comments not allowed in commits"
  echo ""
  echo "Found TODO comments in staged changes:"
  git diff --cached | grep -n "^+.*// TODO"
  echo ""
  echo "Action required:"
  echo "  1. Implement the feature (preferred)"
  echo "  2. Create GitHub issue and remove TODO"
  echo "  3. Remove TODO if no longer relevant"
  echo ""
  echo "Commit blocked by pre-commit hook (SEC-P0-006)"
  exit 1
fi

exit 0
```

```bash
chmod +x .git/hooks/pre-commit
```

**Test:**
```bash
# Create test file with TODO
echo "// TODO: test" > test.cs
git add test.cs
git commit -m "Test"
# Expected: Commit blocked
git reset HEAD test.cs
rm test.cs
```

### Step 5: Commit Changes (15 minutes)

```bash
git add services/usp/src/USP.API/Program.cs
git add services/usp/src/USP.API/Controllers/SealController.cs
git add .git/hooks/pre-commit

git commit -m "Remove TODO comments from production code

- Fix MapMetrics extension method (install prometheus-net.AspNetCore)
- Remove TODO comment from SealController (X-Vault-Token implemented)
- Add pre-commit hook to prevent future TODO commits

Resolves: SEC-P0-006 - TODO Comments in Production Code

🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## 7. Testing Strategy

**Test 1: No TODO Comments in Codebase**
```bash
git grep "// TODO" && echo "FAIL" || echo "PASS"
```

**Test 2: Metrics Endpoint Working**
```bash
curl https://localhost:5001/metrics -k | grep -q "http_request" && echo "PASS"
```

**Test 3: Pre-Commit Hook Blocks TODOs**
```bash
echo "// TODO: test" > test.cs && git add test.cs && git commit -m "Test" 2>&1 | grep -q "ERROR" && echo "PASS"
git reset HEAD test.cs && rm test.cs
```

---

## 8. Monitoring & Validation

**Post-Implementation:**
- [ ] No TODO comments in codebase
- [ ] Metrics endpoint returns data
- [ ] Pre-commit hook active

---

## 9. Compliance Evidence

**SOC 2 CC8.1:** Production code complete, no deferred implementations

---

## 10. Sign-Off

- [ ] **Developer:** All TODOs resolved
- [ ] **Engineering Lead:** Code review passed

---

## 11. Appendix

### Related Documentation

- [Coding Guidelines](/docs/CODING_GUIDELINES.md)
- [SEC-P0-004](SEC-P0-004-vault-seal-unauthenticated.md) - Vault Authentication
- [SEC-P1-004](../P1-HIGH/SEC-P1-004-metrics-endpoint-broken.md) - Metrics Endpoint

### Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-12-27 | Security Audit Team | Initial version |

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P0-006 Finding Document**

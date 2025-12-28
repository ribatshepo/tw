# SEC-P1-005: Metric Recording Inactive

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P1-005 |
| **Title** | Security Metrics Defined But Not Called in Services |
| **Priority** | P1 - HIGH |
| **Severity** | High |
| **Category** | Monitoring/Observability |
| **Status** | Not Started |
| **Effort Estimate** | 4 hours |
| **Implementation Phase** | Phase 2 (Week 2, Day 10) |
| **Assigned To** | Backend Engineer |
| **Created** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:824-829` |
| **Code Files** | `SecurityMetrics.cs`, `AuthorizationService.cs`, `AuthController.cs` |
| **Dependencies** | Blocked by SEC-P1-004 (Metrics Endpoint) |
| **Compliance Impact** | SOC 2 (CC7.2), PCI-DSS (Req 10.2) |

---

## 3. Executive Summary

### Problem

`SecurityMetrics` class defined with methods like `RecordAuthorizationCheck()`, `RecordLoginAttempt()`, but these are **never called** in actual services.

### Impact

- **No Security Monitoring:** Cannot detect brute force, authorization failures, anomalies
- **No Incident Response:** Missing data for security investigations
- **No Compliance:** SOC 2/PCI-DSS require security event monitoring

### Solution

Add metric recording calls to AuthorizationService, AuthController, SecretsController.

---

## 4. Implementation Guide

### Step 1: Record Login Attempts (1 hour)

```csharp
// AuthController.cs

[HttpPost("login")]
public async Task<IActionResult> Login([FromBody] LoginRequest request)
{
    var result = await _authService.AuthenticateAsync(request.Username, request.Password);

    // ✅ ADD: Record login attempt metric
    _securityMetrics.RecordLoginAttempt(
        username: request.Username,
        success: result.Success,
        mfaUsed: result.MfaUsed
    );

    if (!result.Success)
    {
        return Unauthorized();
    }

    return Ok(new { token = result.Token });
}
```

### Step 2: Record Authorization Checks (1 hour)

```csharp
// AuthorizationService.cs

public async Task<bool> CheckPermissionAsync(string userId, string resource, string action)
{
    var hasPermission = await _permissionRepository.HasPermissionAsync(userId, resource, action);

    // ✅ ADD: Record authorization check metric
    _securityMetrics.RecordAuthorizationCheck(
        userId: userId,
        resource: resource,
        action: action,
        allowed: hasPermission
    );

    return hasPermission;
}
```

### Step 3: Record Secret Access (1 hour)

```csharp
// SecretsController.cs

[HttpGet("{id}")]
public async Task<IActionResult> GetSecret(Guid id)
{
    var secret = await _secretService.GetSecretAsync(id);

    // ✅ ADD: Record secret access metric
    _securityMetrics.RecordSecretAccess(
        userId: User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
        secretId: id.ToString(),
        operation: "read"
    );

    return Ok(secret);
}
```

### Step 4: Test Metrics Recording (30 minutes)

```bash
# Make some API requests
curl -X POST https://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"wrong"}' -k

# Check metrics
curl https://localhost:5001/metrics -k | grep login_attempts
# Expected: login_attempts_total{username="admin",success="false",mfa_used="false"} 1
```

---

## 5. Testing

- [ ] Login metrics recorded (success/failure, MFA usage)
- [ ] Authorization metrics recorded (allowed/denied)
- [ ] Secret access metrics recorded
- [ ] Metrics visible in Prometheus

---

## 6. Compliance Evidence

**SOC 2 CC7.2:** Security events monitored
**PCI-DSS Req 10.2:** Audit trail of security events

---

## 7. Sign-Off

- [ ] **Backend Engineer:** Metrics recording implemented
- [ ] **Security:** Security metrics validated

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P1-005**

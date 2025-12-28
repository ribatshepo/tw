# SEC-P1-008: Secrets Endpoints Lack Granular Authorization

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P1-008 |
| **Title** | SecretsController Uses [Authorize] Not [RequirePermission] |
| **Priority** | P1 - HIGH |
| **Severity** | High |
| **Category** | API Authorization |
| **Status** | Not Started |
| **Effort Estimate** | 3 hours |
| **Implementation Phase** | Phase 2 (Week 2, Day 11) |
| **Assigned To** | Backend Engineer |
| **Created** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:647-665` |
| **Code Files** | `SecretsController.cs` |
| **Dependencies** | Blocked by SEC-P0-005 (JWT Middleware) |
| **Compliance Impact** | SOC 2 (CC6.1 - Access Control) |

---

## 3. Executive Summary

### Problem

SecretsController uses generic `[Authorize]` attribute allowing any authenticated user to access all secrets endpoints. No granular permissions enforced.

### Impact

- **Overly Permissive:** Authenticated users can read/write/delete secrets they shouldn't access
- **No Least Privilege:** Violates principle of least privilege
- **Compliance Risk:** SOC 2 requires granular access controls

### Solution

Replace `[Authorize]` with `[RequirePermission]` attributes specifying resource and action per endpoint.

---

## 4. Implementation Guide

### Step 1: Update GET Endpoints (45 minutes)

```csharp
// SecretsController.cs

[ApiController]
[Route("api/v1/secrets")]
public class SecretsController : ControllerBase
{
    // ✅ CHANGE: Generic [Authorize] to granular [RequirePermission]

    [HttpGet]
    [RequirePermission("secrets", "list")]  // List permission
    public async Task<IActionResult> ListSecrets()
    {
        var secrets = await _secretService.ListSecretsAsync();
        return Ok(secrets);
    }

    [HttpGet("{id}")]
    [RequirePermission("secrets", "read")]  // Read permission
    public async Task<IActionResult> GetSecret(Guid id)
    {
        var secret = await _secretService.GetSecretAsync(id);
        return Ok(secret);
    }
}
```

### Step 2: Update POST/PUT Endpoints (45 minutes)

```csharp
[HttpPost]
[RequirePermission("secrets", "write")]  // Write permission
public async Task<IActionResult> CreateSecret([FromBody] CreateSecretRequest request)
{
    var secretId = await _secretService.CreateSecretAsync(request);
    return CreatedAtAction(nameof(GetSecret), new { id = secretId }, null);
}

[HttpPut("{id}")]
[RequirePermission("secrets", "write")]  // Write permission
public async Task<IActionResult> UpdateSecret(Guid id, [FromBody] UpdateSecretRequest request)
{
    await _secretService.UpdateSecretAsync(id, request);
    return NoContent();
}
```

### Step 3: Update DELETE Endpoints (45 minutes)

```csharp
[HttpDelete("{id}")]
[RequirePermission("secrets", "delete")]  // Delete permission
public async Task<IActionResult> DeleteSecret(Guid id)
{
    await _secretService.DeleteSecretAsync(id);
    return NoContent();
}
```

### Step 4: Test Authorization (45 minutes)

```bash
# Test unauthorized user (should fail)
curl -X GET https://localhost:5001/api/v1/secrets \
  -H "Authorization: Bearer $TOKEN_NO_PERMS" -k
# Expected: 403 Forbidden

# Test authorized user (should succeed)
curl -X GET https://localhost:5001/api/v1/secrets \
  -H "Authorization: Bearer $TOKEN_WITH_PERMS" -k
# Expected: 200 OK with secrets list
```

---

## 5. Testing

- [ ] Unauthorized users get 403 Forbidden
- [ ] Users with "secrets:list" can list secrets
- [ ] Users with "secrets:read" can read individual secrets
- [ ] Users with "secrets:write" can create/update secrets
- [ ] Users with "secrets:delete" can delete secrets
- [ ] Authorization metrics recorded

---

## 6. Compliance Evidence

**SOC 2 CC6.1:** Granular access controls enforced on secrets endpoints

---

## 7. Sign-Off

- [ ] **Backend Engineer:** RequirePermission attributes implemented
- [ ] **Security:** Authorization verified

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P1-008**

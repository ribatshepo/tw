# Authentication & Authorization - Category Consolidation

**Category:** Authentication & Authorization
**Total Findings:** 5
**Total Effort:** 18 hours
**Implementation Phase:** Phase 1 (P0: Days 4-5) + Phase 2 (P1: Week 2, Days 11-12)

---

## Overview

This document consolidates all findings related to authentication, authorization, access control, and API security.

## Findings Summary

| Finding ID | Title | Priority | Effort | Phase |
|-----------|-------|----------|--------|-------|
| SEC-P0-004 | Vault Seal/Unseal Endpoints Unauthenticated | P0 - CRITICAL | 2h | Phase 1 |
| SEC-P0-005 | JWT Bearer Middleware Missing | P0 - CRITICAL | 1h | Phase 1 |
| SEC-P1-008 | Secrets Endpoints Lack Granular Authorization | P1 - HIGH | 3h | Phase 2 |
| SEC-P1-009 | Row-Level Security Not Enabled | P1 - HIGH | 4h | Phase 2 |
| SEC-P3-003 | Device Compliance ABAC Missing | P3 - LOW | 8h | Phase 4 |

**Total Critical Path Effort:** 10 hours (P0 + P1)

---

## Critical Path Analysis

### Production Blockers (P0) - Week 1, Day 4-5

**SEC-P0-004: Vault Unauthenticated (2h)**
- **Impact:** Anyone can seal/unseal vault without credentials
- **Risk Level:** 🔴 CRITICAL - Service disruption possible
- **Fix:** Implement X-Vault-Token authentication

**SEC-P0-005: Missing JWT Middleware (1h)**
- **Impact:** No authentication layer for API endpoints
- **Risk Level:** 🔴 CRITICAL - Unauthorized API access
- **Fix:** Add `AddAuthentication().AddJwtBearer()` to Program.cs

### Pre-Production (P1) - Week 2, Day 11-12

**SEC-P1-008: Granular Authorization (3h)**
- **Impact:** Any authenticated user can access all secrets
- **Risk Level:** 🟠 HIGH - Privilege escalation
- **Fix:** Replace `[Authorize]` with `[RequirePermission]` attributes

**SEC-P1-009: Row-Level Security (4h)**
- **Impact:** Database-level access controls missing
- **Risk Level:** 🟠 HIGH - Multi-tenant isolation breach
- **Fix:** Enable PostgreSQL RLS on secrets table

### Post-Production Enhancement (P3)

**SEC-P3-003: Device Compliance ABAC (8h)**
- **Impact:** Cannot enforce device-based access policies
- **Risk Level:** 🟡 MEDIUM - Zero Trust incomplete
- **Fix:** Implement device attribute checking

---

## Security Architecture

### Current State (Insecure)

```
┌──────────────┐
│ Client       │
└──────┬───────┘
       │ HTTP Request (No Auth)
       ▼
┌──────────────────────────┐
│  Vault Seal/Unseal API   │ ❌ No authentication
│  /api/v1/vault/seal/*    │
└──────────────────────────┘

┌──────────────┐
│ Client       │
└──────┬───────┘
       │ HTTP Request (Any Token)
       ▼
┌──────────────────────────┐
│  Secrets API             │ ❌ Generic [Authorize]
│  /api/v1/secrets/*       │    (No granular checks)
└──────────────────────────┘
```

### Target State (Secure)

```
┌──────────────┐
│ Client       │
└──────┬───────┘
       │ X-Vault-Token: <root-token>
       ▼
┌──────────────────────────┐
│ [RequireVaultToken]      │ ✅ Token validation
│  Vault Seal/Unseal API   │
└──────────────────────────┘

┌──────────────┐
│ Client       │
└──────┬───────┘
       │ Authorization: Bearer <jwt>
       ▼
┌──────────────────────────┐
│ [AddJwtBearer]           │ ✅ JWT validation
│  ↓                       │
│ [RequirePermission]      │ ✅ Granular RBAC
│  ↓                       │
│ Row-Level Security       │ ✅ Database isolation
│  ↓                       │
│ Device ABAC (Optional)   │ ✅ Zero Trust
│  ↓                       │
│  Secrets API             │
└──────────────────────────┘
```

---

## Implementation Strategy

### Phase 1: Critical Auth Fixes (Week 1, Day 4-5) - 3 hours

**SEC-P0-004: Vault Token Authentication (2h)**

```csharp
// Attributes/RequireVaultTokenAttribute.cs
public class RequireVaultTokenAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var token = context.HttpContext.Request.Headers["X-Vault-Token"].FirstOrDefault();

        if (string.IsNullOrEmpty(token))
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                error = "vault_token_required",
                message = "X-Vault-Token header required for vault operations"
            });
            return;
        }

        // Validate token against vault root token
        if (token != GetVaultRootToken())
        {
            context.Result = new ForbidResult();
        }
    }
}

// VaultController.cs
[ApiController]
[Route("api/v1/vault")]
public class VaultController : ControllerBase
{
    [HttpPost("seal")]
    [RequireVaultToken]  // ✅ Now requires authentication
    public async Task<IActionResult> Seal()
    {
        await _vaultService.SealAsync();
        return Ok();
    }

    [HttpPost("seal/unseal")]
    [RequireVaultToken]  // ✅ Now requires authentication
    public async Task<IActionResult> Unseal([FromBody] UnsealRequest request)
    {
        await _vaultService.UnsealAsync(request.Key);
        return Ok();
    }
}
```

**SEC-P0-005: JWT Bearer Middleware (1h)**

```csharp
// Program.cs
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"]))
        };
    });

builder.Services.AddAuthorization();

// Add after builder.Build()
app.UseAuthentication();  // ✅ Enable JWT validation
app.UseAuthorization();
```

### Phase 2: Granular Authorization (Week 2, Day 11-12) - 7 hours

**SEC-P1-008: RequirePermission Attributes (3h)**

```csharp
// SecretsController.cs
[ApiController]
[Route("api/v1/secrets")]
public class SecretsController : ControllerBase
{
    [HttpGet]
    [RequirePermission("secrets", "list")]  // ✅ Granular permission
    public async Task<IActionResult> ListSecrets()
    {
        var secrets = await _secretService.ListSecretsAsync();
        return Ok(secrets);
    }

    [HttpGet("{id}")]
    [RequirePermission("secrets", "read")]  // ✅ Granular permission
    public async Task<IActionResult> GetSecret(Guid id)
    {
        var secret = await _secretService.GetSecretAsync(id);
        return Ok(secret);
    }

    [HttpPost]
    [RequirePermission("secrets", "write")]  // ✅ Granular permission
    public async Task<IActionResult> CreateSecret([FromBody] CreateSecretRequest request)
    {
        var id = await _secretService.CreateSecretAsync(request);
        return CreatedAtAction(nameof(GetSecret), new { id }, null);
    }

    [HttpDelete("{id}")]
    [RequirePermission("secrets", "delete")]  // ✅ Granular permission
    public async Task<IActionResult> DeleteSecret(Guid id)
    {
        await _secretService.DeleteSecretAsync(id);
        return NoContent();
    }
}
```

**SEC-P1-009: Row-Level Security (4h)**

```sql
-- migrations/sql/09-enable-rls-secrets.sql

BEGIN;

-- Enable RLS on secrets table
ALTER TABLE usp.secrets ENABLE ROW LEVEL SECURITY;

-- Policy: Users can only see secrets in their namespace
CREATE POLICY secrets_namespace_isolation ON usp.secrets
    FOR SELECT
    USING (
        namespace_id IN (
            SELECT namespace_id
            FROM usp.user_namespaces
            WHERE user_id = current_setting('app.current_user_id')::UUID
        )
    );

-- Policy: Users can only insert in their namespace
CREATE POLICY secrets_insert_policy ON usp.secrets
    FOR INSERT
    WITH CHECK (
        namespace_id IN (
            SELECT namespace_id
            FROM usp.user_namespaces
            WHERE user_id = current_setting('app.current_user_id')::UUID
        )
    );

COMMIT;
```

```csharp
// USPDbContext.cs - Set user context for RLS
public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    var userId = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (!string.IsNullOrEmpty(userId))
    {
        await Database.ExecuteSqlRawAsync($"SET app.current_user_id = '{userId}'");
    }

    return await base.SaveChangesAsync(cancellationToken);
}
```

### Phase 3: Device-Based Access Control (Week 4+) - 8 hours

**SEC-P3-003: Device Compliance ABAC (8h)**

```csharp
// Models/DeviceAttributes.cs
public class DeviceAttributes
{
    public string DeviceId { get; set; }
    public bool DiskEncrypted { get; set; }
    public bool AntivirusEnabled { get; set; }
    public bool IsJailbroken { get; set; }
    public string ComplianceStatus { get; set; }  // Compliant, NonCompliant
}

// Middleware/AbacAuthorizationMiddleware.cs
public async Task InvokeAsync(HttpContext context)
{
    var deviceId = context.Request.Headers["X-Device-Id"].FirstOrDefault();
    var deviceAttributes = await _deviceService.GetDeviceAttributesAsync(deviceId);

    if (deviceAttributes?.ComplianceStatus != "Compliant")
    {
        context.Response.StatusCode = 403;
        await context.Response.WriteAsync("Device not compliant with security policies");
        return;
    }

    await _next(context);
}
```

---

## Testing Strategy

### Security Testing

```bash
# Test vault authentication
curl -X POST https://localhost:5001/api/v1/vault/seal
# Expected: 401 Unauthorized (no X-Vault-Token)

curl -X POST https://localhost:5001/api/v1/vault/seal \
  -H "X-Vault-Token: invalid"
# Expected: 403 Forbidden

curl -X POST https://localhost:5001/api/v1/vault/seal \
  -H "X-Vault-Token: $VAULT_ROOT_TOKEN"
# Expected: 200 OK

# Test JWT authentication
curl -X GET https://localhost:5001/api/v1/secrets
# Expected: 401 Unauthorized (no token)

curl -X GET https://localhost:5001/api/v1/secrets \
  -H "Authorization: Bearer $VALID_JWT"
# Expected: 200 OK

# Test granular permissions
curl -X DELETE https://localhost:5001/api/v1/secrets/123 \
  -H "Authorization: Bearer $READ_ONLY_TOKEN"
# Expected: 403 Forbidden (no delete permission)

# Test RLS
psql -U user1 -c "SELECT * FROM usp.secrets;"
# Expected: Only user1's secrets returned
```

---

## Compliance Mapping

| Finding | SOC 2 | HIPAA | PCI-DSS |
|---------|-------|-------|---------|
| SEC-P0-004 | CC6.1 | 164.312(a)(1) | Req 7.1 |
| SEC-P0-005 | CC6.1 | 164.312(a)(1) | Req 7.1 |
| SEC-P1-008 | CC6.1 | 164.312(a)(1) | Req 7.1 |
| SEC-P1-009 | CC6.1 | 164.312(a)(1) | Req 7.2 |
| SEC-P3-003 | CC6.1 | - | - |

**Compliance Status:** ❌ **NON-COMPLIANT** until P0 + P1 resolved.

---

## Success Criteria

✅ **Complete when:**
- Vault seal/unseal requires X-Vault-Token authentication
- JWT Bearer middleware validates all API requests
- Secrets endpoints use RequirePermission attributes
- Row-Level Security enforces namespace isolation
- All authorization tests pass
- Penetration testing confirms no bypass vulnerabilities

---

**Status:** Not Started
**Last Updated:** 2025-12-27
**Category Owner:** Security + Backend Teams

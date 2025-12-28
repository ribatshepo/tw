# SEC-P0-004: Unauthenticated Vault Seal/Unseal Endpoints

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P0-004 |
| **Title** | Vault Seal/Unseal Endpoints Unauthenticated |
| **Priority** | P0 - CRITICAL |
| **Severity** | Critical |
| **Category** | Authentication/Authorization |
| **Status** | Not Started |
| **Effort Estimate** | 8 hours |
| **Implementation Phase** | Phase 1 (Week 1, Day 3-4) |
| **Assigned To** | Backend Engineer 1 + Security Engineer |
| **Reviewers** | Security Engineer, Engineering Lead |
| **Created** | 2025-12-27 |
| **Last Updated** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:605-627` |
| **Security Spec** | `/home/tshepo/projects/tw/docs/specs/security.md:528-662` (Vault Authentication) |
| **Code Files** | `/home/tshepo/projects/tw/services/usp/src/USP.API/Controllers/SealController.cs` (lines 37, 102, 150, 184, 207) |
| **Dependencies** | Blocked by SEC-P0-001 (Secrets Externalization), SEC-P0-002 (appsettings Secrets) |
| **Blocks** | SEC-P1-008 (Secrets Granular Authorization), Phase 4 (All service implementations) |
| **Related Findings** | SEC-P0-005 (JWT Middleware), SEC-P0-006 (TODO Comments) |
| **Compliance Impact** | SOC 2 (CC6.1, CC6.7), HIPAA (164.312(a)(1), 164.312(d)), PCI-DSS (Req 7.1, Req 8.2), GDPR (Article 32) |

---

## 3. Executive Summary

### Problem Statement

All vault seal/unseal endpoints in `SealController` are marked `[AllowAnonymous]`, allowing **unauthenticated access** to critical vault operations. An attacker can:
- Initialize the vault without authorization
- Submit unseal keys (enabling brute-force attacks)
- **Seal the vault at will** (complete platform-wide service disruption)
- Reset unseal progress
- Retrieve vault operational status (reconnaissance)

A TODO comment at line 151 acknowledges this security gap: `// TODO: Implement X-Vault-Token authentication for production`

### Business Impact

- **Complete Platform Disruption:** Anyone can seal the vault, preventing **all services** from accessing secrets
- **Denial of Service:** Sealing vault stops UCCP, NCCS, UDPS, Stream Compute (entire platform down)
- **Brute Force Attack:** Attackers can submit unlimited unseal key guesses without rate limiting
- **Reconnaissance:** Vault status leakage enables targeted attacks
- **Compliance Violation:** Violates SOC 2 CC6.1 (access controls), HIPAA 164.312(a)(1) (unique user ID), PCI-DSS Req 7.1 (need-to-know access)
- **Production Blocker:** P0 finding that **BLOCKS PRODUCTION DEPLOYMENT**

### Solution Overview

1. **Implement X-Vault-Token authentication** for all vault endpoints
2. **Create VaultAuthenticationMiddleware** to validate tokens
3. **Generate and distribute vault tokens** securely to authorized administrators
4. **Remove [AllowAnonymous] attributes** from all SealController endpoints
5. **Add rate limiting** to unseal endpoint (prevent brute force)
6. **Update API tests and documentation** with authentication requirements

**Timeline:** 8 hours (Day 3-4 of Week 1)

---

## 4. Technical Details

### Current State

**File: `/home/tshepo/projects/tw/services/usp/src/USP.API/Controllers/SealController.cs`**

```csharp
[ApiController]
[Route("api/v1/vault/seal")]
public class SealController : ControllerBase
{
    // Line 37 - Anyone can initialize vault
    [HttpPost("init")]
    [AllowAnonymous]  // ❌ NO AUTHENTICATION
    public async Task<IActionResult> Initialize([FromBody] InitializeRequest request)
    {
        // Creates master key, unseal keys, root token
        // CRITICAL SECURITY ISSUE: No authentication required
    }

    // Line 102 - Anyone can unseal vault
    [HttpPost("unseal")]
    [AllowAnonymous]  // ❌ NO AUTHENTICATION
    public async Task<IActionResult> Unseal([FromBody] UnsealRequest request)
    {
        // Accepts unseal keys to decrypt master key
        // CRITICAL: Enables brute-force attacks on unseal keys
    }

    // Line 150 - Anyone can SEAL vault (service disruption)
    [HttpPost("seal")]
    [AllowAnonymous]  // ❌ NO AUTHENTICATION
    // TODO: Implement X-Vault-Token authentication for production
    public async Task<IActionResult> Seal()
    {
        // Seals vault, making all secrets inaccessible
        // CRITICAL: Complete denial of service attack vector
    }

    // Line 184 - Vault status leaked to anyone
    [HttpGet("seal-status")]
    [AllowAnonymous]  // ❌ NO AUTHENTICATION
    public IActionResult SealStatus()
    {
        // Returns seal status, unseal progress
        // Reconnaissance for targeted attacks
    }

    // Line 207 - Anyone can reset unseal progress
    [HttpPost("unseal-reset")]
    [AllowAnonymous]  // ❌ NO AUTHENTICATION
    public IActionResult UnsealReset()
    {
        // Resets unseal progress
        // Denial of service: prevents legitimate unsealing
    }
}
```

### Vulnerability Analysis

**1. Complete Lack of Authentication:**
- No authentication checks on any vault endpoint
- `[AllowAnonymous]` explicitly bypasses ASP.NET Core authentication
- Even if JWT middleware is registered (SEC-P0-005), these endpoints bypass it

**2. Denial of Service Attack:**
- **Seal Endpoint (`/api/v1/vault/seal/seal`):**
  - Attacker sends single POST request → vault sealed
  - All services lose access to secrets immediately
  - Platform-wide service disruption
  - Requires unseal with 3 of 5 unseal keys to recover
  - **Mean Time To Recovery (MTTR):** 10-30 minutes (gather administrators, submit unseal keys)

**3. Brute Force Attack:**
- **Unseal Endpoint (`/api/v1/vault/seal/unseal`):**
  - Accepts Shamir secret sharing keys
  - No rate limiting (can submit unlimited guesses)
  - Attacker with partial unseal keys can brute-force remaining keys
  - **Mathematical Complexity:** 3 of 5 keys required
    - If attacker has 2 keys, need to brute-force 1 more: ~2^256 attempts (infeasible)
    - If attacker has 0 keys, need to brute-force 3 keys: ~2^768 attempts (infeasible)
  - **However:** Without authentication, attacker can monitor unseal progress (reconnaissance)

**4. Reconnaissance:**
- **Seal Status Endpoint (`/api/v1/vault/seal/seal-status`):**
  - Returns JSON with seal status:
    ```json
    {
      "sealed": false,
      "threshold": 3,
      "totalShares": 5,
      "progress": 0
    }
    ```
  - Attacker learns:
    - Vault is unsealed (secrets accessible)
    - Unseal threshold (3 keys required)
    - Total unseal keys (5 keys exist)
  - Enables targeted social engineering attacks

**5. Initialization Attack:**
- **Initialize Endpoint (`/api/v1/vault/seal/init`):**
  - If vault is uninitialized, attacker can initialize with their own master key
  - Attacker receives root token and unseal keys
  - **Complete compromise:** Attacker controls entire vault

**6. Unseal Reset Attack:**
- **Unseal Reset Endpoint (`/api/v1/vault/seal/unseal-reset`):**
  - Attacker can reset unseal progress during legitimate unsealing
  - Denial of service: prevents administrators from unsealing vault
  - Forces re-start of unseal process

### Gap Analysis

**Security Specification Requirements (docs/specs/security.md:528-662):**

1. **Requirement:** "Vault operations MUST require root token or X-Vault-Token authentication" (line 589)
   - **Current State:** ❌ VIOLATED - All endpoints [AllowAnonymous]

2. **Requirement:** "Vault seal/unseal MUST be logged in tamper-proof audit log" (line 593)
   - **Current State:** ⚠️ PARTIALLY IMPLEMENTED - Logging exists but no authentication to associate with user

3. **Requirement:** "Vault seal MUST require multi-factor authentication (MFA)" (line 592)
   - **Current State:** ❌ NOT IMPLEMENTED - No authentication at all, let alone MFA

4. **Requirement:** "Unseal endpoint MUST have rate limiting (10 attempts per hour)" (line 594)
   - **Current State:** ❌ NOT IMPLEMENTED - No rate limiting

**Compliance Violations:**

- **SOC 2 CC6.1 (Logical Access Controls):** Vault access not restricted to authorized users
- **SOC 2 CC6.7 (Encryption Keys):** Vault seal/unseal keys not protected by authentication
- **HIPAA 164.312(a)(1) (Unique User Identification):** No user identification for vault operations
- **HIPAA 164.312(d) (Encryption and Decryption):** Encryption key management not secured
- **PCI-DSS Req 7.1 (Limit Access to System Components):** Access not limited to authorized personnel
- **PCI-DSS Req 8.2 (Unique User IDs):** No user identification
- **GDPR Article 32 (Security of Processing):** Insufficient access controls for encryption keys

---

## 5. Implementation Requirements

### Acceptance Criteria

- [ ] VaultAuthenticationMiddleware implemented to validate X-Vault-Token header
- [ ] [AllowAnonymous] removed from all SealController endpoints
- [ ] Vault token generation mechanism implemented (or root token used initially)
- [ ] Rate limiting implemented on unseal endpoint (10 attempts per hour per IP)
- [ ] Audit logging captures authenticated user for all vault operations
- [ ] API tests updated to include X-Vault-Token header
- [ ] Documentation updated (API.http, README, deployment guide)
- [ ] Security regression test passing (unauthenticated requests return 401 Unauthorized)
- [ ] TODO comment removed from line 151

### Technical Requirements

1. **VaultAuthenticationMiddleware:**
   - Extract `X-Vault-Token` header from request
   - Validate token against stored vault tokens (database or in-memory)
   - Set `HttpContext.User` with vault user claims
   - Return 401 Unauthorized if token missing or invalid

2. **Token Management:**
   - Generate root token on vault initialization
   - Store tokens in `vault_tokens` database table with expiration
   - Support token creation, revocation, renewal
   - Include token policies (seal, unseal, secrets-read, secrets-write)

3. **Rate Limiting:**
   - Use ASP.NET Core rate limiting middleware
   - Limit unseal endpoint: 10 requests per hour per IP
   - Return 429 Too Many Requests when limit exceeded

4. **Audit Logging:**
   - Log all vault operations with authenticated user ID
   - Include: timestamp, user, operation, success/failure, IP address
   - Store in tamper-proof audit log (append-only, immutable)

### Compliance Requirements

**SOC 2 Evidence:**
- Screenshot of VaultAuthenticationMiddleware code
- Audit log showing authenticated vault operations
- Rate limiting configuration

**HIPAA Evidence:**
- Access control policy for vault operations
- Audit trail of vault seal/unseal events with user IDs

**PCI-DSS Evidence:**
- Vault access restricted to authorized users only
- Unique user IDs for all vault operations

---

## 6. Step-by-Step Implementation Guide

### Prerequisites

- [x] .NET 8 SDK installed
- [x] USP service running locally
- [x] PostgreSQL database initialized
- [x] Secrets externalized (SEC-P0-001, SEC-P0-002, SEC-P0-003 complete)

### Step 1: Create VaultAuthenticationMiddleware (2 hours)

```bash
cd /home/tshepo/projects/tw/services/usp/src/USP.API

# Create Middleware directory if not exists
mkdir -p Middleware
```

**Create `Middleware/VaultAuthenticationMiddleware.cs`:**

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Threading.Tasks;

namespace USP.API.Middleware
{
    /// <summary>
    /// Middleware to authenticate vault operations using X-Vault-Token header
    /// SEC-P0-004: Vault seal/unseal endpoints require authentication
    /// </summary>
    public class VaultAuthenticationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<VaultAuthenticationMiddleware> _logger;

        public VaultAuthenticationMiddleware(
            RequestDelegate next,
            ILogger<VaultAuthenticationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IVaultTokenService vaultTokenService)
        {
            // Only apply to vault seal endpoints
            if (!context.Request.Path.StartsWithSegments("/api/v1/vault/seal"))
            {
                await _next(context);
                return;
            }

            // Extract X-Vault-Token header
            if (!context.Request.Headers.TryGetValue("X-Vault-Token", out var token))
            {
                _logger.LogWarning("Vault operation attempted without X-Vault-Token header. Path: {Path}, IP: {IP}",
                    context.Request.Path, context.Connection.RemoteIpAddress);

                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Unauthorized",
                    message = "X-Vault-Token header required for vault operations"
                });
                return;
            }

            // Validate token
            var vaultToken = await vaultTokenService.ValidateTokenAsync(token);
            if (vaultToken == null)
            {
                _logger.LogWarning("Invalid X-Vault-Token provided. Path: {Path}, IP: {IP}",
                    context.Request.Path, context.Connection.RemoteIpAddress);

                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Unauthorized",
                    message = "Invalid X-Vault-Token"
                });
                return;
            }

            // Set authenticated user principal
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, vaultToken.Id.ToString()),
                new Claim(ClaimTypes.Name, vaultToken.DisplayName),
                new Claim("vault_policy", vaultToken.Policy),
                new Claim("token_type", "vault")
            };

            var identity = new ClaimsIdentity(claims, "VaultToken");
            var principal = new ClaimsPrincipal(identity);
            context.User = principal;

            _logger.LogInformation("Vault operation authenticated. User: {User}, Path: {Path}",
                vaultToken.DisplayName, context.Request.Path);

            await _next(context);
        }
    }
}
```

### Step 2: Implement IVaultTokenService (2 hours)

**Create `Services/IVaultTokenService.cs`:**

```csharp
using System;
using System.Threading.Tasks;
using USP.API.Models;

namespace USP.API.Services
{
    public interface IVaultTokenService
    {
        Task<VaultToken> ValidateTokenAsync(string token);
        Task<VaultToken> CreateTokenAsync(string displayName, string policy, TimeSpan? ttl = null);
        Task RevokeTokenAsync(string token);
    }
}
```

**Create `Services/VaultTokenService.cs`:**

```csharp
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using USP.API.Data;
using USP.API.Models;

namespace USP.API.Services
{
    public class VaultTokenService : IVaultTokenService
    {
        private readonly ApplicationDbContext _context;

        public VaultTokenService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<VaultToken> ValidateTokenAsync(string token)
        {
            var hashedToken = HashToken(token);

            var vaultToken = await _context.VaultTokens
                .Where(t => t.TokenHash == hashedToken)
                .Where(t => !t.Revoked)
                .Where(t => t.ExpiresAt == null || t.ExpiresAt > DateTime.UtcNow)
                .FirstOrDefaultAsync();

            if (vaultToken != null)
            {
                // Update last used timestamp
                vaultToken.LastUsedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return vaultToken;
        }

        public async Task<VaultToken> CreateTokenAsync(string displayName, string policy, TimeSpan? ttl = null)
        {
            // Generate cryptographically secure token
            var token = GenerateToken();
            var hashedToken = HashToken(token);

            var vaultToken = new VaultToken
            {
                Id = Guid.NewGuid(),
                TokenHash = hashedToken,
                DisplayName = displayName,
                Policy = policy,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = ttl.HasValue ? DateTime.UtcNow.Add(ttl.Value) : null,
                Revoked = false
            };

            _context.VaultTokens.Add(vaultToken);
            await _context.SaveChangesAsync();

            // Return token to caller (ONLY time plaintext token is visible)
            vaultToken.TokenPlaintext = token;
            return vaultToken;
        }

        public async Task RevokeTokenAsync(string token)
        {
            var hashedToken = HashToken(token);

            var vaultToken = await _context.VaultTokens
                .Where(t => t.TokenHash == hashedToken)
                .FirstOrDefaultAsync();

            if (vaultToken != null)
            {
                vaultToken.Revoked = true;
                vaultToken.RevokedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        private static string GenerateToken()
        {
            // Generate 32-byte (256-bit) random token
            var tokenBytes = new byte[32];
            RandomNumberGenerator.Fill(tokenBytes);
            return Convert.ToBase64String(tokenBytes);
        }

        private static string HashToken(string token)
        {
            // Hash token before storage (don't store plaintext)
            var tokenBytes = Convert.FromBase64String(token);
            var hashedBytes = SHA256.HashData(tokenBytes);
            return Convert.ToBase64String(hashedBytes);
        }
    }
}
```

**Create `Models/VaultToken.cs`:**

```csharp
using System;

namespace USP.API.Models
{
    public class VaultToken
    {
        public Guid Id { get; set; }
        public string TokenHash { get; set; }  // SHA256 hash of token
        public string DisplayName { get; set; }
        public string Policy { get; set; }  // e.g., "root", "seal", "unseal", "secrets-read"
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public bool Revoked { get; set; }
        public DateTime? RevokedAt { get; set; }

        // Not stored in database, only returned once on creation
        public string TokenPlaintext { get; set; }
    }
}
```

### Step 3: Register Middleware and Services in Program.cs (30 minutes)

**Update `Program.cs`:**

```csharp
// Add VaultTokenService
builder.Services.AddScoped<IVaultTokenService, VaultTokenService>();

// Add VaultAuthenticationMiddleware (BEFORE UseAuthorization)
app.UseMiddleware<VaultAuthenticationMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
```

### Step 4: Remove [AllowAnonymous] from SealController (15 minutes)

**Update `Controllers/SealController.cs`:**

```csharp
[ApiController]
[Route("api/v1/vault/seal")]
[Authorize]  // ✅ ADD: Require authentication for all endpoints
public class SealController : ControllerBase
{
    [HttpPost("init")]
    // ✅ REMOVE: [AllowAnonymous]
    public async Task<IActionResult> Initialize([FromBody] InitializeRequest request) { ... }

    [HttpPost("unseal")]
    // ✅ REMOVE: [AllowAnonymous]
    public async Task<IActionResult> Unseal([FromBody] UnsealRequest request) { ... }

    [HttpPost("seal")]
    // ✅ REMOVE: [AllowAnonymous]
    // ✅ REMOVE: // TODO: Implement X-Vault-Token authentication for production
    public async Task<IActionResult> Seal() { ... }

    [HttpGet("seal-status")]
    // ✅ REMOVE: [AllowAnonymous]
    public IActionResult SealStatus() { ... }

    [HttpPost("unseal-reset")]
    // ✅ REMOVE: [AllowAnonymous]
    public IActionResult UnsealReset() { ... }
}
```

### Step 5: Create Database Migration for VaultTokens (30 minutes)

```bash
cd /home/tshepo/projects/tw/services/usp/src/USP.API

# Create migration
dotnet ef migrations add AddVaultTokens

# Apply migration
dotnet ef database update
```

### Step 6: Generate Root Token on Vault Initialization (1 hour)

**Update `SealController.Initialize()` to create root token:**

```csharp
[HttpPost("init")]
public async Task<IActionResult> Initialize([FromBody] InitializeRequest request)
{
    // ... existing initialization logic ...

    // Generate root token
    var rootToken = await _vaultTokenService.CreateTokenAsync(
        displayName: "Root Token",
        policy: "root",
        ttl: null  // Never expires
    );

    return Ok(new
    {
        keys = unsealKeys,
        rootToken = rootToken.TokenPlaintext  // ⚠️ ONLY shown once, never stored
    });
}
```

### Step 7: Implement Rate Limiting on Unseal Endpoint (1 hour)

**Add rate limiting middleware:**

```csharp
// In Program.cs
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("unseal", options =>
    {
        options.PermitLimit = 10;
        options.Window = TimeSpan.FromHours(1);
        options.QueueLimit = 0;
    });
});

app.UseRateLimiter();
```

**Apply to unseal endpoint:**

```csharp
[HttpPost("unseal")]
[EnableRateLimiting("unseal")]
public async Task<IActionResult> Unseal([FromBody] UnsealRequest request) { ... }
```

### Step 8: Update API Tests (30 minutes)

**Update `USP.API.http`:**

```http
### Initialize Vault (No token required on first init)
POST https://localhost:5001/api/v1/vault/seal/init
Content-Type: application/json

{
  "threshold": 3,
  "shares": 5
}

### Response includes root token (save this for future requests):
# {
#   "keys": ["key1", "key2", "key3", "key4", "key5"],
#   "rootToken": "hvs.XXXXXXXXXXXX"
# }

### Unseal Vault (Requires root token)
POST https://localhost:5001/api/v1/vault/seal/unseal
Content-Type: application/json
X-Vault-Token: hvs.XXXXXXXXXXXX

{
  "key": "key1"
}

### Seal Vault (Requires root token)
POST https://localhost:5001/api/v1/vault/seal/seal
X-Vault-Token: hvs.XXXXXXXXXXXX

### Seal Status (Requires root token)
GET https://localhost:5001/api/v1/vault/seal/seal-status
X-Vault-Token: hvs.XXXXXXXXXXXX
```

### Step 9: Test Vault Authentication (45 minutes)

```bash
cd /home/tshepo/projects/tw/services/usp/src/USP.API

# Start USP service
dotnet run

# In another terminal:
# Test 1: Unauthenticated request should fail
curl -X GET https://localhost:5001/api/v1/vault/seal/seal-status -k
# Expected: 401 Unauthorized

# Test 2: Initialize vault (no token required first time)
curl -X POST https://localhost:5001/api/v1/vault/seal/init \
  -H "Content-Type: application/json" \
  -d '{"threshold": 3, "shares": 5}' -k

# Save root token from response

# Test 3: Seal status with token
curl -X GET https://localhost:5001/api/v1/vault/seal/seal-status \
  -H "X-Vault-Token: <root_token_here>" -k
# Expected: 200 OK with seal status

# Test 4: Seal vault with token
curl -X POST https://localhost:5001/api/v1/vault/seal/seal \
  -H "X-Vault-Token: <root_token_here>" -k
# Expected: 200 OK, vault sealed
```

### Step 10: Commit Changes (15 minutes)

```bash
cd /home/tshepo/projects/tw

git add services/usp/src/USP.API/Middleware/VaultAuthenticationMiddleware.cs
git add services/usp/src/USP.API/Services/VaultTokenService.cs
git add services/usp/src/USP.API/Services/IVaultTokenService.cs
git add services/usp/src/USP.API/Models/VaultToken.cs
git add services/usp/src/USP.API/Controllers/SealController.cs
git add services/usp/src/USP.API/Program.cs
git add services/usp/src/USP.API/USP.API.http

git commit -m "Implement X-Vault-Token authentication for vault seal/unseal

- Add VaultAuthenticationMiddleware to validate X-Vault-Token header
- Implement VaultTokenService for token management (create, validate, revoke)
- Remove [AllowAnonymous] from all SealController endpoints
- Generate root token on vault initialization
- Add rate limiting to unseal endpoint (10 attempts/hour)
- Update API tests with X-Vault-Token examples

Resolves: SEC-P0-004 - Vault Seal/Unseal Endpoints Unauthenticated

Security Impact:
- Prevents unauthorized vault seal (DoS attack)
- Prevents brute-force unseal key attacks
- Audit trail for all vault operations

🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## 7. Testing Strategy

### Security Tests

**Test 1: Unauthenticated Request Returns 401**
```bash
#!/usr/bin/env bash
# Test: SEC-P0-004-test-unauthenticated-401.sh

curl -X GET https://localhost:5001/api/v1/vault/seal/seal-status -k -s -o /dev/null -w "%{http_code}" | grep -q "401"
if [ $? -eq 0 ]; then
  echo "PASS: Unauthenticated request returns 401"
else
  echo "FAIL: Unauthenticated request did not return 401"
  exit 1
fi
```

**Test 2: Valid Token Returns 200**
```bash
#!/usr/bin/env bash
# Test: SEC-P0-004-test-valid-token-200.sh

# Initialize vault to get root token
ROOT_TOKEN=$(curl -X POST https://localhost:5001/api/v1/vault/seal/init \
  -H "Content-Type: application/json" \
  -d '{"threshold": 3, "shares": 5}' -k -s | jq -r '.rootToken')

# Test with valid token
curl -X GET https://localhost:5001/api/v1/vault/seal/seal-status \
  -H "X-Vault-Token: $ROOT_TOKEN" -k -s -o /dev/null -w "%{http_code}" | grep -q "200"

if [ $? -eq 0 ]; then
  echo "PASS: Valid token returns 200"
else
  echo "FAIL: Valid token did not return 200"
  exit 1
fi
```

---

## 8. Rollback Plan

If authentication causes issues:
1. Temporarily add `[AllowAnonymous]` back to endpoints
2. Debug VaultAuthenticationMiddleware
3. Re-test and re-deploy

---

## 9. Monitoring & Validation

**Metrics:**
- `vault_unauthenticated_attempts` - Counter (target: 0)
- `vault_invalid_token_attempts` - Counter
- `vault_seal_operations` - Counter with user label

**Alerts:**
```yaml
- alert: VaultUnauthenticatedAccess
  expr: vault_unauthenticated_attempts > 0
  for: 1m
  labels:
    severity: critical
```

---

## 10. Post-Implementation Validation

**Day 0:**
- [ ] All vault endpoints require X-Vault-Token
- [ ] Unauthenticated requests return 401
- [ ] Root token generated on vault init
- [ ] Rate limiting active on unseal endpoint

**Week 1:**
- [ ] No unauthorized vault access attempts logged
- [ ] All vault operations have authenticated user in audit log

**Month 1:**
- [ ] Vault token rotation policy documented
- [ ] Vault access control policy enforced

---

## 11. Documentation Updates

**Updated:**
- `USP.API.http` - X-Vault-Token examples
- `GETTING_STARTED.md` - Vault initialization with token
- `DEPLOYMENT.md` - Production vault token management

---

## 12. Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| Vault initialization fails to create root token | High | Test thoroughly, have manual token creation fallback |
| Middleware breaks existing authenticated endpoints | Medium | Only apply to `/api/v1/vault/seal` paths |
| Root token leaked | Critical | Store securely, rotate immediately if compromised |

---

## 13. Compliance Evidence

**SOC 2:** X-Vault-Token authentication, access controls enforced
**HIPAA:** Unique user IDs for vault operations, audit logging
**PCI-DSS:** Vault access restricted to authorized users only

---

## 14. Sign-Off

- [ ] **Developer:** Implementation complete
- [ ] **Security Engineer:** Security review passed
- [ ] **Engineering Lead:** Approved for production

---

## 15. Appendix

### Related Documentation

- [SEC-P0-005](SEC-P0-005-jwt-middleware-missing.md) - JWT Middleware
- [SEC-P0-006](SEC-P0-006-todo-comments-production.md) - TODO Comments
- [Vault Authentication Spec](../../../../specs/security.md)

### Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-12-27 | Security Audit Team | Initial version |

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P0-004 Finding Document**

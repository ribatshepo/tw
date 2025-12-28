# SEC-P0-005: JWT Bearer Middleware Missing

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P0-005 |
| **Title** | JWT Bearer Authentication Middleware Not Registered |
| **Priority** | P0 - CRITICAL |
| **Severity** | Critical |
| **Category** | Authentication/Authorization |
| **Status** | Not Started |
| **Effort Estimate** | 4 hours |
| **Implementation Phase** | Phase 1 (Week 1, Day 5) |
| **Assigned To** | Backend Engineer 1 |
| **Reviewers** | Security Engineer |
| **Created** | 2025-12-27 |
| **Last Updated** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:628-646` |
| **Security Spec** | `/home/tshepo/projects/tw/docs/specs/security.md:251-305` (Unified Authentication System) |
| **Code Files** | `/home/tshepo/projects/tw/services/usp/src/USP.API/Program.cs` (JWT Bearer registration missing) |
| **Dependencies** | None |
| **Blocks** | SEC-P1-008 (Secrets Granular Authorization), Phase 4 (All service implementations) |
| **Related Findings** | SEC-P0-004 (Vault Authentication) |
| **Compliance Impact** | SOC 2 (CC6.1), HIPAA (164.312(a)(1)), PCI-DSS (Req 8.2), GDPR (Article 32) |

---

## 3. Executive Summary

### Problem Statement

While `TokenService` implements JWT validation logic and `app.UseAuthentication()` is called in Program.cs, the **JWT Bearer authentication scheme is not registered** in the dependency injection container. This means `[Authorize]` attributes on controllers may not properly validate JWT tokens, potentially allowing unauthorized access to protected endpoints.

### Business Impact

- **Authentication Bypass:** `[Authorize]` attributes may not enforce JWT validation
- **Unauthorized Access:** Users without valid JWT tokens may access protected resources
- **Data Breach:** Secrets, user data, and system resources exposed to unauthenticated users
- **Compliance Violation:** Violates SOC 2 CC6.1 (access controls), HIPAA 164.312(a)(1) (unique user ID)
- **Production Blocker:** P0 finding that **BLOCKS PRODUCTION DEPLOYMENT**

### Solution Overview

1. **Register JWT Bearer scheme** in `Program.cs` with `AddAuthentication().AddJwtBearer()`
2. **Configure TokenValidationParameters** (issuer, audience, signing key, clock skew)
3. **Ensure correct middleware order** (`UseAuthentication()` before `UseAuthorization()`)
4. **Test JWT authentication end-to-end** (generate token, access protected endpoint)
5. **Update integration tests** to verify JWT validation

**Timeline:** 4 hours (Day 5 of Week 1)

---

## 4. Technical Details

### Current State

**File: `/home/tshepo/projects/tw/services/usp/src/USP.API/Program.cs` (line 212)**

```csharp
var app = builder.Build();

// ✅ Authentication middleware IS called
app.UseAuthentication();  // Line 212

// ✅ Authorization middleware IS called
app.UseAuthorization();

// ❌ BUT: No JWT Bearer scheme registered!
// Missing from builder.Services configuration:
// builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
//     .AddJwtBearer(options => { ... });
```

**Current Authentication Setup:**
- ✅ `TokenService` exists and generates/validates JWT tokens
- ✅ `app.UseAuthentication()` middleware is registered
- ✅ `[Authorize]` attributes present on controllers
- ❌ **JWT Bearer authentication scheme NOT registered**
- ❌ **No TokenValidationParameters configured**

### Vulnerability Analysis

**Impact of Missing JWT Bearer Scheme:**

1. **Silent Authentication Failure:**
   - `[Authorize]` attributes present on endpoints (e.g., SecretsController, UsersController)
   - Without JWT Bearer scheme, ASP.NET Core doesn't know HOW to authenticate requests
   - May result in:
     - **Option A:** All requests fail authentication (good, but wrong error messages)
     - **Option B:** All requests pass authentication (CRITICAL VULNERABILITY)
   - Behavior depends on ASP.NET Core version and default authentication handler

2. **No JWT Validation:**
   - Even if `TokenService.ValidateToken()` is called manually in some controllers
   - `[Authorize]` attribute relies on authentication middleware
   - Without JWT Bearer scheme, `HttpContext.User` is not populated from JWT

3. **Inconsistent Security:**
   - Some endpoints may manually call `TokenService.ValidateToken()` (secure)
   - Other endpoints rely solely on `[Authorize]` attribute (potentially insecure)
   - Creates confusion and potential security gaps

**Test to Determine Current Behavior:**
```bash
# Generate JWT token
TOKEN=$(curl -X POST https://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"password"}' -k -s | jq -r '.token')

# Test protected endpoint WITHOUT token
curl -X GET https://localhost:5001/api/v1/secrets -k -v
# If returns 401 Unauthorized: Safe (but wrong error message)
# If returns 200 OK: CRITICAL VULNERABILITY

# Test protected endpoint WITH invalid token
curl -X GET https://localhost:5001/api/v1/secrets \
  -H "Authorization: Bearer invalid_token_here" -k -v
# Should return 401 Unauthorized with JWT validation error
```

### Gap Analysis

**Security Specification Requirements (docs/specs/security.md:251-305):**

1. **Requirement:** "All API endpoints MUST validate JWT tokens using ASP.NET Core JWT Bearer middleware" (line 268)
   - **Current State:** ❌ VIOLATED - JWT Bearer scheme not registered

2. **Requirement:** "Token validation MUST verify signature, issuer, audience, and expiration" (line 272)
   - **Current State:** ❌ NOT IMPLEMENTED - TokenValidationParameters not configured

3. **Requirement:** "Clock skew MUST be limited to 5 minutes maximum" (line 275)
   - **Current State:** ❌ NOT IMPLEMENTED - No clock skew configuration

**Compliance Violations:**
- **SOC 2 CC6.1:** Access controls not properly enforced
- **HIPAA 164.312(a)(1):** Unique user identification not verified
- **PCI-DSS Req 8.2:** Strong authentication not enforced
- **GDPR Article 32:** Security of processing requires access controls

---

## 5. Implementation Requirements

### Acceptance Criteria

- [ ] JWT Bearer authentication scheme registered in `Program.cs`
- [ ] TokenValidationParameters configured (issuer, audience, signing key, expiration, clock skew)
- [ ] Middleware order correct (`UseAuthentication()` before `UseAuthorization()`)
- [ ] JWT tokens validated automatically by `[Authorize]` attributes
- [ ] Invalid JWT tokens return 401 Unauthorized
- [ ] Missing JWT tokens return 401 Unauthorized
- [ ] Integration tests verify JWT authentication
- [ ] Documentation updated

### Technical Requirements

1. **JWT Bearer Registration:**
   ```csharp
   builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
       .AddJwtBearer(options =>
       {
           options.TokenValidationParameters = new TokenValidationParameters
           {
               ValidateIssuer = true,
               ValidIssuer = configuration["Jwt:Issuer"],
               ValidateAudience = true,
               ValidAudience = configuration["Jwt:Audience"],
               ValidateLifetime = true,
               IssuerSigningKey = new SymmetricSecurityKey(
                   Encoding.UTF8.GetBytes(configuration["Jwt:Key"])),
               ValidateIssuerSigningKey = true,
               ClockSkew = TimeSpan.FromMinutes(5)
           };
       });
   ```

2. **Configuration:**
   - `Jwt:Key` from environment variable or user secrets (NOT appsettings.json)
   - `Jwt:Issuer` = `https://usp.example.com` (production URL)
   - `Jwt:Audience` = `https://usp.example.com` (production URL)

3. **Middleware Order:**
   ```csharp
   app.UseAuthentication();   // MUST be before UseAuthorization
   app.UseAuthorization();
   ```

---

## 6. Step-by-Step Implementation Guide

### Prerequisites

- [x] .NET 8 SDK installed
- [x] USP service running locally
- [x] SEC-P0-002 complete (appsettings secrets externalized)

### Step 1: Add JWT Configuration to User Secrets (10 minutes)

```bash
cd /home/tshepo/projects/tw/services/usp/src/USP.API

# Generate JWT signing key (256-bit)
JWT_KEY=$(openssl rand -base64 32 | tr -d "=+/" | cut -c1-32)

# Add to user secrets
dotnet user-secrets set "Jwt:Key" "$JWT_KEY"
dotnet user-secrets set "Jwt:Issuer" "https://localhost:5001"
dotnet user-secrets set "Jwt:Audience" "https://localhost:5001"

# Verify
dotnet user-secrets list | grep "Jwt"
```

### Step 2: Register JWT Bearer Scheme in Program.cs (1 hour)

**Update `Program.cs`:**

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Existing services...

// ADD: JWT Bearer Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],

            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5),  // Allow 5-minute clock skew

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]
                    ?? throw new InvalidOperationException("Jwt:Key configuration missing")))
        };

        // Add custom events for logging
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILogger<Program>>();
                logger.LogWarning("JWT authentication failed: {Message}", context.Exception.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILogger<Program>>();
                logger.LogInformation("JWT token validated for user: {User}",
                    context.Principal?.Identity?.Name);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Ensure correct middleware order
app.UseAuthentication();   // MUST be before UseAuthorization
app.UseAuthorization();

app.MapControllers();

app.Run();
```

### Step 3: Update TokenService to Use Correct Signing Key (30 minutes)

**Ensure `TokenService` uses the SAME signing key from configuration:**

```csharp
public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email)
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

### Step 4: Test JWT Authentication (1 hour)

```bash
cd /home/tshepo/projects/tw/services/usp/src/USP.API

# Start USP service
dotnet run

# In another terminal:

# Test 1: Request without token should fail
curl -X GET https://localhost:5001/api/v1/secrets -k -v
# Expected: 401 Unauthorized

# Test 2: Login to get valid token
TOKEN=$(curl -X POST https://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"password"}' -k -s | jq -r '.token')

echo "Token: $TOKEN"

# Test 3: Request with valid token should succeed
curl -X GET https://localhost:5001/api/v1/secrets \
  -H "Authorization: Bearer $TOKEN" -k -v
# Expected: 200 OK with secrets list

# Test 4: Request with invalid token should fail
curl -X GET https://localhost:5001/api/v1/secrets \
  -H "Authorization: Bearer invalid_token_here" -k -v
# Expected: 401 Unauthorized

# Test 5: Verify token claims in HttpContext.User
# (Check server logs for "JWT token validated for user: admin")
```

### Step 5: Update Integration Tests (1 hour)

**Create `Tests/JwtAuthenticationTests.cs`:**

```csharp
using System.Net;
using System.Net.Http.Headers;
using Xunit;

public class JwtAuthenticationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public JwtAuthenticationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UnauthenticatedRequest_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/secrets");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedRequest_ReturnsOk()
    {
        // Arrange: Login to get valid token
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = "admin",
            password = "password"
        });

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginResult.Token);

        // Act
        var response = await _client.GetAsync("/api/v1/secrets");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task InvalidToken_ReturnsUnauthorized()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "invalid_token");

        // Act
        var response = await _client.GetAsync("/api/v1/secrets");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

### Step 6: Commit Changes (15 minutes)

```bash
cd /home/tshepo/projects/tw

git add services/usp/src/USP.API/Program.cs
git add services/usp/src/USP.API/Services/TokenService.cs
git add services/usp/src/USP.API.Tests/JwtAuthenticationTests.cs

git commit -m "Register JWT Bearer authentication scheme in Program.cs

- Add AddAuthentication().AddJwtBearer() with TokenValidationParameters
- Configure JWT issuer, audience, signing key validation
- Add custom events for authentication logging
- Update TokenService to use configuration for signing key
- Add integration tests for JWT authentication

Resolves: SEC-P0-005 - JWT Bearer Middleware Missing

Security Impact:
- [Authorize] attributes now properly enforce JWT validation
- Invalid JWT tokens rejected with 401 Unauthorized
- Consistent authentication across all protected endpoints

🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## 7. Testing Strategy

**Test 1: Unauthenticated Request Returns 401**
```bash
curl -X GET https://localhost:5001/api/v1/secrets -k | grep -q "401" && echo "PASS"
```

**Test 2: Valid JWT Returns 200**
```bash
TOKEN=$(/* get token */)
curl -X GET https://localhost:5001/api/v1/secrets -H "Authorization: Bearer $TOKEN" -k | grep -q "200" && echo "PASS"
```

**Test 3: Invalid JWT Returns 401**
```bash
curl -X GET https://localhost:5001/api/v1/secrets -H "Authorization: Bearer invalid" -k | grep -q "401" && echo "PASS"
```

---

## 8. Rollback Plan

If JWT authentication breaks:
1. Comment out `AddAuthentication().AddJwtBearer()` registration
2. Revert to manual token validation in controllers
3. Debug and re-test

---

## 9. Monitoring & Validation

**Metrics:**
- `jwt_authentication_failures` - Counter
- `jwt_validation_time_ms` - Histogram

**Alerts:**
```yaml
- alert: HighJWTFailureRate
  expr: rate(jwt_authentication_failures[5m]) > 10
  labels:
    severity: warning
```

---

## 10. Post-Implementation Validation

**Day 0:**
- [ ] JWT Bearer scheme registered
- [ ] Unauthenticated requests return 401
- [ ] Valid JWT tokens authenticate successfully

**Week 1:**
- [ ] All protected endpoints enforce JWT validation
- [ ] Integration tests passing

---

## 11. Documentation Updates

- `USP.API.http` - Add Bearer token examples
- `GETTING_STARTED.md` - JWT configuration setup

---

## 12. Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| Breaking existing endpoints | High | Test thoroughly, have rollback plan |
| Performance impact of JWT validation | Low | JWT validation is fast (~1ms) |

---

## 13. Compliance Evidence

**SOC 2:** JWT Bearer authentication configured and tested
**HIPAA:** Unique user IDs enforced via JWT claims
**PCI-DSS:** Strong authentication enforced

---

## 14. Sign-Off

- [ ] **Developer:** Implementation complete
- [ ] **Security Engineer:** Security review passed

---

## 15. Appendix

### Related Documentation

- [SEC-P0-004](SEC-P0-004-vault-seal-unauthenticated.md) - Vault Authentication
- [Microsoft JWT Bearer Docs](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/jwt-authn)

### Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-12-27 | Security Audit Team | Initial version |

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P0-005 Finding Document**

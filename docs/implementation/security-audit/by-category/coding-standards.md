# Coding Standards - Category Consolidation

**Category:** Coding Standards & Code Quality
**Total Findings:** 7
**Total Effort:** 11.5 hours
**Implementation Phase:** Phase 1 (P0: Day 5) + Phase 3 (P2: Week 3, Day 9) + Phase 4 (P3)

---

## Overview

This document consolidates all findings related to code quality, naming conventions, documentation, and refactoring opportunities.

## Findings Summary

| Finding ID | Title | Priority | Effort | Focus |
|-----------|-------|----------|--------|-------|
| SEC-P0-006 | TODO Comments in Production Code | P0 - CRITICAL | 2h | Production Readiness |
| SEC-P0-007 | NotImplementedException in HSM Support | P0 - CRITICAL | 3h | Feature Completeness |
| SEC-P2-013 | XML Documentation Missing | P2 - MEDIUM | 4h | API Docs |
| SEC-P2-014 | AuthenticationService Parameter Naming | P2 - MEDIUM | 0.25h | Naming Convention |
| SEC-P2-015 | Magic Numbers to Constants | P2 - MEDIUM | 2h | Maintainability |
| SEC-P3-007 | Base Controller Utility Missing | P3 - LOW | 2h | Code Reuse |
| SEC-P3-008 | UserID Validation Extension Method | P3 - LOW | 1h | Code Reuse |

**Total Critical Path Effort:** 5 hours (P0 only)

---

## Critical Path Analysis

### Production Blockers (P0) - Week 1, Day 5

**SEC-P0-006: TODO Comments (2h)**
- **Impact:** Incomplete implementation indicated by TODOs
- **Risk:** Production deployment with known gaps
- **Fix:** Resolve or remove all TODO comments

**SEC-P0-007: NotImplementedException (3h)**
- **Impact:** HSM encryption throws NotImplementedException
- **Risk:** Runtime crashes on HSM code paths
- **Fix:** Implement HSM support or document limitation

### Code Quality Improvements (P2) - Week 3, Day 9

**SEC-P2-013: XML Docs (4h)**
- Add XML documentation to public APIs
- Enable IntelliSense for configuration classes
- Generate Swagger documentation

**SEC-P2-014: Naming Convention (15 minutes)**
- Fix parameter naming violation
- Remove redundant `this.` prefix

**SEC-P2-015: Magic Numbers (2h)**
- Extract constants for AES key lengths, login attempts, lockout duration

### Code Refactoring (P3) - Week 4+

**SEC-P3-007: Base Controller (2h)**
- Create BaseController with common validation
- Reduce code duplication across 10+ controllers

**SEC-P3-008: Extension Methods (1h)**
- Create ClaimsPrincipal extension for user ID extraction
- Eliminate repeated User.FindFirstValue pattern

---

## Code Quality Themes

### 1. Incomplete Implementation

**Current State:**
- 15+ TODO comments in production code
- NotImplementedException in HSM encryption
- MapMetrics method disabled

**Required State:**
- All TODOs resolved or removed
- All code paths functional
- Features complete or properly stubbed

### 2. Missing Documentation

**Current State:**
- No XML comments on public APIs
- Configuration classes undocumented
- No IntelliSense hints

**Required State:**
- All public types documented
- XML docs enabled in project
- Swagger shows parameter descriptions

### 3. Code Duplication

**Current State:**
- User ID extraction repeated 15+ times
- Validation logic duplicated across controllers
- Magic numbers used instead of constants

**Required State:**
- BaseController for common logic
- Extension methods for repeated patterns
- Constants for all magic numbers

---

## Implementation Strategy

### Phase 1: Production Blockers (Week 1, Day 5) - 5 hours

**SEC-P0-006: Resolve TODO Comments (2h)**

```bash
# Find all TODOs
grep -rn "TODO" src/ --include="*.cs"

# Critical TODOs to fix:
# 1. Program.cs:230 - Fix MapMetrics
# 2. EncryptionService.cs:45 - Implement HSM
# 3. VaultService.cs:120 - Add transaction support
```

**Fix MapMetrics (from SEC-P0-006):**
```csharp
// Program.cs:230
// BEFORE:
// TODO: Fix MapMetrics extension method issue
// app.MapMetrics("/metrics");

// AFTER:
using Prometheus;
app.UseHttpMetrics();
app.MapMetrics("/metrics");  // ✅ Fixed
```

**SEC-P0-007: Implement or Stub HSM (3h)**

Option 1: Implement basic HSM support
```csharp
// EncryptionService.cs
public async Task<byte[]> EncryptWithHsmAsync(byte[] plaintext)
{
    // BEFORE:
    // throw new NotImplementedException("HSM encryption not yet implemented");

    // AFTER (basic implementation):
    if (!_hsmOptions.Enabled)
    {
        throw new InvalidOperationException("HSM not configured. Set HSM:Enabled=true");
    }

    // Use PKCS#11 library to communicate with HSM
    using var session = await _hsmProvider.OpenSessionAsync();
    return await session.EncryptAsync(plaintext, _hsmKeyId);
}
```

Option 2: Document limitation
```csharp
// EncryptionService.cs
/// <summary>
/// Encrypts data using Hardware Security Module.
/// </summary>
/// <remarks>
/// ⚠️ HSM encryption is not supported in this version.
/// Use software-based encryption via EncryptAsync() instead.
/// HSM support planned for v2.0.
/// </remarks>
public Task<byte[]> EncryptWithHsmAsync(byte[] plaintext)
{
    throw new NotSupportedException(
        "HSM encryption not available in this release. " +
        "Use EncryptAsync() for software-based encryption.");
}
```

### Phase 2: Documentation & Standards (Week 3, Day 9) - 6.25 hours

**SEC-P2-013: XML Documentation (4h)**

```csharp
// EmailOptions.cs
/// <summary>
/// Configuration options for the email service.
/// </summary>
public class EmailOptions
{
    /// <summary>
    /// Gets or sets the SMTP server hostname.
    /// </summary>
    /// <value>The SMTP server hostname (e.g., "smtp.gmail.com").</value>
    public string SmtpServer { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the SMTP server port number.
    /// </summary>
    /// <value>The SMTP port (typically 587 for TLS, 465 for SSL).</value>
    public int SmtpPort { get; set; } = 587;
}
```

Enable XML generation:
```xml
<!-- USP.API.csproj -->
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```

**SEC-P2-014: Fix Naming (15 minutes)**

```csharp
// AuthenticationService.cs

// BEFORE:
public AuthenticationService(ISessionService _sessionService)
{
    this._sessionService = _sessionService;
}

// AFTER:
public AuthenticationService(ISessionService sessionService)
{
    _sessionService = sessionService;
}
```

**SEC-P2-015: Extract Constants (2h)**

```csharp
// USP.Core/Constants/SecurityConstants.cs
public static class SecurityConstants
{
    /// <summary>
    /// Maximum failed login attempts before account lockout.
    /// </summary>
    public const int MaxFailedLoginAttempts = 5;

    /// <summary>
    /// Account lockout duration in minutes.
    /// </summary>
    public const int LockoutDurationMinutes = 15;
}

// USP.Core/Constants/EncryptionConstants.cs
public static class EncryptionConstants
{
    /// <summary>
    /// AES-256 key length in bytes.
    /// </summary>
    public const int AesKeyByteLength = 32;  // 256 bits

    /// <summary>
    /// Shamir's Secret Sharing threshold.
    /// </summary>
    public const int ShamirThreshold = 3;
}

// Update usage:
// BEFORE: if (kek.Length != 32)
// AFTER:  if (kek.Length != EncryptionConstants.AesKeyByteLength)

// BEFORE: if (user.FailedLoginAttempts >= 5)
// AFTER:  if (user.FailedLoginAttempts >= SecurityConstants.MaxFailedLoginAttempts)
```

### Phase 3: Refactoring (Week 4+) - 3 hours

**SEC-P3-007: Base Controller (2h)**

```csharp
// Controllers/BaseController.cs
public abstract class BaseController : ControllerBase
{
    /// <summary>
    /// Gets current user ID from claims, throwing Unauthorized if not found.
    /// </summary>
    protected IActionResult GetCurrentUserIdOrUnauthorized(out string userId)
    {
        userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new
            {
                error = "authentication_required",
                message = "User must be authenticated"
            });
        }

        return null; // Success
    }

    /// <summary>
    /// Validates that a GUID is not empty.
    /// </summary>
    protected IActionResult ValidateGuid(Guid id, string parameterName)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new
            {
                error = "invalid_parameter",
                message = $"{parameterName} cannot be empty",
                parameter = parameterName
            });
        }

        return null; // Valid
    }

    /// <summary>
    /// Creates standardized not found response.
    /// </summary>
    protected IActionResult NotFoundError(string resourceType, string identifier)
    {
        return NotFound(new
        {
            error = "resource_not_found",
            message = $"{resourceType} '{identifier}' not found",
            resourceType,
            identifier
        });
    }
}

// Update controllers:
// BEFORE: public class SecretsController : ControllerBase
// AFTER:  public class SecretsController : BaseController
```

**SEC-P3-008: Extension Methods (1h)**

```csharp
// Extensions/ClaimsPrincipalExtensions.cs
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Gets the current user ID from claims.
    /// </summary>
    public static string? GetUserId(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    /// <summary>
    /// Tries to get the current user ID.
    /// </summary>
    public static bool TryGetUserId(this ClaimsPrincipal principal, out string userId)
    {
        userId = principal.GetUserId();
        return !string.IsNullOrEmpty(userId);
    }

    /// <summary>
    /// Gets user ID, throwing if not found.
    /// </summary>
    public static string GetUserIdOrThrow(this ClaimsPrincipal principal)
    {
        var userId = principal.GetUserId();
        if (string.IsNullOrEmpty(userId))
            throw new UnauthorizedAccessException("User ID not found in claims");
        return userId;
    }
}

// Usage:
// BEFORE:
var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
if (string.IsNullOrEmpty(userId))
    return Unauthorized();

// AFTER:
if (!User.TryGetUserId(out var userId))
    return Unauthorized();
```

---

## Code Quality Metrics

### Before Implementation

| Metric | Current | Target |
|--------|---------|--------|
| TODO comments | 15 | 0 |
| NotImplementedException | 3 | 0 |
| Code duplication | High | Low |
| XML documentation | 0% | 80% |
| Magic numbers | 20+ | 0 |
| Lines of duplicated code | ~200 | <50 |

### After Implementation

| Metric | Target |
|--------|--------|
| TODO comments | 0 ✅ |
| NotImplementedException | 0 ✅ |
| Code duplication | <5% ✅ |
| XML documentation | 80%+ ✅ |
| Magic numbers | 0 ✅ |
| BaseController usage | 10+ controllers ✅ |

---

## Testing Strategy

### Static Analysis

```bash
# Find remaining TODOs
grep -rn "TODO\|FIXME\|HACK" src/ --include="*.cs"
# Expected: 0 results

# Find NotImplementedException
grep -rn "NotImplementedException" src/ --include="*.cs"
# Expected: 0 results (or documented as NotSupported)

# Find magic numbers
grep -rn "\b[2-9][0-9]\+\b" src/ --include="*.cs" | grep -v const
# Expected: Minimal results (only justified cases)

# Verify XML docs generated
ls bin/Release/net8.0/*.xml
# Expected: USP.API.xml file exists
```

### Code Review Checklist

- [ ] All TODO comments resolved or removed
- [ ] No NotImplementedException in production code paths
- [ ] All public APIs have XML documentation
- [ ] No underscore prefix on parameters
- [ ] All magic numbers extracted to constants
- [ ] BaseController used in all controllers
- [ ] ClaimsPrincipal extensions used consistently

---

## Success Criteria

✅ **Complete when:**
- Zero TODO comments in production code
- All NotImplementedException replaced with proper implementation or NotSupportedException
- 80%+ XML documentation coverage
- All naming conventions followed
- All magic numbers extracted to constants
- BaseController reduces code duplication by 60%+
- Extension methods eliminate 50+ lines of duplicate code

---

**Status:** Not Started
**Last Updated:** 2025-12-27
**Category Owner:** Backend Engineering Team

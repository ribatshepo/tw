# SEC-P3-008: UserID Validation Extension Method

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P3-008 |
| **Title** | User ID Extraction Pattern Should Be Extension Method |
| **Priority** | P3 - LOW |
| **Severity** | Low |
| **Category** | Coding Standards / Code Quality |
| **Status** | Not Started |
| **Effort Estimate** | 1 hour |
| **Implementation Phase** | Phase 4 (Week 4+, Nice to Have) |
| **Assigned To** | Backend Engineer |
| **Created** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:1371-1378` |
| **Code Files** | Multiple controllers |
| **Dependencies** | SEC-P3-007 (Base Controller) |
| **Compliance Impact** | None (Code quality improvement) |

---

## 3. Executive Summary

### Problem

User ID extraction pattern repeated throughout codebase:

```csharp
var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
if (string.IsNullOrEmpty(userId))
    return Unauthorized(...);
```

This appears in 15+ controllers.

### Impact

- **Code Duplication:** Same 2-3 lines repeated everywhere
- **Inconsistent Error Messages:** Different Unauthorized messages
- **Maintenance Burden:** Changes require updating multiple files

### Solution

Create extension method on `ClaimsPrincipal` to extract and validate user ID in one call.

---

## 4. Implementation Guide

### Step 1: Create Extension Method (30 minutes)

```csharp
// Extensions/ClaimsPrincipalExtensions.cs

using System.Security.Claims;

namespace USP.API.Extensions;

/// <summary>
/// Extension methods for ClaimsPrincipal.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Gets the current user ID from claims.
    /// </summary>
    /// <param name="principal">The claims principal.</param>
    /// <returns>The user ID, or null if not present.</returns>
    public static string? GetUserId(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    /// <summary>
    /// Gets the current user ID, throwing exception if not present.
    /// </summary>
    /// <param name="principal">The claims principal.</param>
    /// <returns>The user ID.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when user ID not found.</exception>
    public static string GetUserIdOrThrow(this ClaimsPrincipal principal)
    {
        var userId = principal.GetUserId();

        if (string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException("User ID not found in claims. User must be authenticated.");
        }

        return userId;
    }

    /// <summary>
    /// Tries to get the current user ID.
    /// </summary>
    /// <param name="principal">The claims principal.</param>
    /// <param name="userId">The user ID output parameter.</param>
    /// <returns>True if user ID found, false otherwise.</returns>
    public static bool TryGetUserId(this ClaimsPrincipal principal, out string userId)
    {
        userId = principal.GetUserId();
        return !string.IsNullOrEmpty(userId);
    }

    /// <summary>
    /// Gets the current user's email from claims.
    /// </summary>
    public static string? GetUserEmail(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.Email);
    }

    /// <summary>
    /// Gets the current user's roles from claims.
    /// </summary>
    public static IEnumerable<string> GetUserRoles(this ClaimsPrincipal principal)
    {
        return principal.FindAll(ClaimTypes.Role).Select(c => c.Value);
    }

    /// <summary>
    /// Checks if the user has a specific role.
    /// </summary>
    public static bool HasRole(this ClaimsPrincipal principal, string role)
    {
        return principal.IsInRole(role);
    }

    /// <summary>
    /// Checks if the user has any of the specified roles.
    /// </summary>
    public static bool HasAnyRole(this ClaimsPrincipal principal, params string[] roles)
    {
        return roles.Any(role => principal.IsInRole(role));
    }
}
```

### Step 2: Update Controllers to Use Extension (30 minutes)

**Before:**
```csharp
// SecretsController.cs

[HttpGet]
public async Task<IActionResult> ListSecrets()
{
    // ❌ Duplicated pattern
    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(userId))
        return Unauthorized(new { error = "User not authenticated" });

    var secrets = await _secretService.ListSecretsAsync(userId);
    return Ok(secrets);
}
```

**After:**
```csharp
// SecretsController.cs

using USP.API.Extensions;

[HttpGet]
public async Task<IActionResult> ListSecrets()
{
    // ✅ Clean, one-liner
    if (!User.TryGetUserId(out var userId))
        return Unauthorized(new { error = "User not authenticated" });

    var secrets = await _secretService.ListSecretsAsync(userId);
    return Ok(secrets);
}

// Or even cleaner with exception handling:
[HttpGet]
public async Task<IActionResult> ListSecrets()
{
    var userId = User.GetUserIdOrThrow();  // ✅ Throws if not found
    var secrets = await _secretService.ListSecretsAsync(userId);
    return Ok(secrets);
}
```

**Apply to all controllers that extract user ID:**
- AuthController (10+ methods)
- SecretsController (8+ methods)
- VaultController (5+ methods)
- UsersController (6+ methods)
- RolesController (4+ methods)

**Total lines of code saved:** ~60-80 lines

---

## 5. Testing

- [ ] Extension methods created
- [ ] All controllers updated to use extensions
- [ ] All tests still pass
- [ ] No more duplicated User.FindFirstValue calls
- [ ] Error messages consistent

---

## 6. Compliance Evidence

None (Code quality improvement)

---

## 7. Sign-Off

- [ ] **Backend Engineer:** Extension methods implemented
- [ ] **Tech Lead:** Code duplication eliminated

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P3-008**

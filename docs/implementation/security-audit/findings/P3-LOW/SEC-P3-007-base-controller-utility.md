# SEC-P3-007: Base Controller Utility Missing

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P3-007 |
| **Title** | Base Controller Class Missing for Common Validation Logic |
| **Priority** | P3 - LOW |
| **Severity** | Low |
| **Category** | Coding Standards / Code Quality |
| **Status** | Not Started |
| **Effort Estimate** | 2 hours |
| **Implementation Phase** | Phase 4 (Week 4+, Nice to Have) |
| **Assigned To** | Backend Engineer |
| **Created** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:1371-1378` |
| **Code Files** | Multiple controllers with duplicated validation |
| **Dependencies** | SEC-P3-008 (UserID Validation Extension) |
| **Compliance Impact** | None (Code quality improvement) |

---

## 3. Executive Summary

### Problem

Similar validation logic repeated across multiple controllers. No base controller class to extract common patterns.

### Impact

- **Code Duplication:** Same validation logic in 10+ controllers
- **Maintenance Burden:** Bugs must be fixed in multiple places
- **Inconsistency:** Validation logic slightly different across controllers

### Solution

Create `BaseController` class with common validation helpers, error responses, and user context extraction.

---

## 4. Implementation Guide

### Step 1: Create BaseController (1 hour)

```csharp
// Controllers/BaseController.cs

using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace USP.API.Controllers;

/// <summary>
/// Base controller with common validation and helper methods.
/// </summary>
[ApiController]
public abstract class BaseController : ControllerBase
{
    /// <summary>
    /// Gets the current user ID from claims.
    /// </summary>
    /// <returns>The user ID, or null if not authenticated.</returns>
    protected string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    /// <summary>
    /// Gets the current user ID, throwing UnauthorizedResult if not present.
    /// </summary>
    /// <returns>The user ID.</returns>
    protected IActionResult GetCurrentUserIdOrUnauthorized(out string userId)
    {
        userId = GetCurrentUserId();

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new
            {
                error = "authentication_required",
                message = "User must be authenticated"
            });
        }

        return null; // Success, userId is populated
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
    /// Validates that a string is not null or empty.
    /// </summary>
    protected IActionResult ValidateRequired(string value, string parameterName)
    {
        if (string.IsNullOrEmpty(value))
        {
            return BadRequest(new
            {
                error = "invalid_parameter",
                message = $"{parameterName} is required",
                parameter = parameterName
            });
        }

        return null; // Valid
    }

    /// <summary>
    /// Creates a standardized error response.
    /// </summary>
    protected IActionResult Error(string errorCode, string message, int statusCode = 400)
    {
        return StatusCode(statusCode, new
        {
            error = errorCode,
            message = message,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Creates a standardized success response.
    /// </summary>
    protected IActionResult Success(object data, string message = null)
    {
        return Ok(new
        {
            success = true,
            message = message,
            data = data,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Creates a standardized not found response.
    /// </summary>
    protected IActionResult NotFoundError(string resourceType, string identifier)
    {
        return NotFound(new
        {
            error = "resource_not_found",
            message = $"{resourceType} with identifier '{identifier}' not found",
            resourceType = resourceType,
            identifier = identifier
        });
    }
}
```

### Step 2: Update Controllers to Use BaseController (1 hour)

**Before:**
```csharp
// SecretsController.cs

[ApiController]
[Route("api/v1/secrets")]
public class SecretsController : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> GetSecret(Guid id)
    {
        // ❌ Duplicated validation
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { error = "authentication_required" });

        // ❌ Duplicated validation
        if (id == Guid.Empty)
            return BadRequest(new { error = "invalid_id" });

        var secret = await _secretService.GetSecretAsync(id);
        if (secret == null)
            return NotFound(new { error = "secret_not_found" });

        return Ok(secret);
    }
}
```

**After:**
```csharp
// SecretsController.cs

[Route("api/v1/secrets")]
public class SecretsController : BaseController  // ✅ Inherit from BaseController
{
    [HttpGet("{id}")]
    public async Task<IActionResult> GetSecret(Guid id)
    {
        // ✅ Use base controller helper
        var authResult = GetCurrentUserIdOrUnauthorized(out var userId);
        if (authResult != null) return authResult;

        // ✅ Use base controller validation
        var validationResult = ValidateGuid(id, nameof(id));
        if (validationResult != null) return validationResult;

        var secret = await _secretService.GetSecretAsync(id);
        if (secret == null)
            return NotFoundError("Secret", id.ToString());

        return Success(secret);
    }
}
```

**Apply to all controllers:**
- AuthController
- SecretsController
- VaultController
- UsersController
- RolesController
- PermissionsController

---

## 5. Testing

- [ ] BaseController class created
- [ ] All controllers inherit from BaseController
- [ ] Duplicated validation logic removed
- [ ] All tests still pass
- [ ] API responses consistent across controllers

---

## 6. Compliance Evidence

None (Code quality improvement)

---

## 7. Sign-Off

- [ ] **Backend Engineer:** BaseController implemented
- [ ] **Tech Lead:** Code duplication reduced

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P3-007**

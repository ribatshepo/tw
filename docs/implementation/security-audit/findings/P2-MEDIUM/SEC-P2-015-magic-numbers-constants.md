# SEC-P2-015: Magic Numbers to Constants

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P2-015 |
| **Title** | Magic Numbers Should Be Extracted to Named Constants |
| **Priority** | P2 - MEDIUM |
| **Severity** | Low |
| **Category** | Coding Standards |
| **Status** | Not Started |
| **Effort Estimate** | 2 hours |
| **Implementation Phase** | Phase 3 (Week 3, Day 9) |
| **Assigned To** | Backend Engineers |
| **Created** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:1362-1378` |
| **Code Files** | Multiple service classes with magic numbers |
| **Dependencies** | None |
| **Compliance Impact** | None (Code quality improvement) |

---

## 3. Executive Summary

### Problem

Magic numbers used throughout codebase instead of named constants:

```csharp
if (kek.Length != 32)  // Should be const int KeyByteLength = 32
if (user.FailedLoginAttempts >= 5)  // Should be const int MaxFailedLoginAttempts = 5
user.LockedUntil = DateTime.UtcNow.AddMinutes(15);  // Should be const int LockoutMinutes = 15
```

### Impact

- **Poor Maintainability:** Hard to update values used in multiple places
- **Unclear Intent:** Numbers don't convey meaning
- **Configuration Inflexibility:** Can't easily change values without code changes

### Solution

Extract magic numbers to named constants or configuration options.

---

## 4. Implementation Guide

### Step 1: Create Constants Classes (1 hour)

```csharp
// USP.Core/Constants/SecurityConstants.cs

namespace USP.Core.Constants;

/// <summary>
/// Security-related constants for authentication and authorization.
/// </summary>
public static class SecurityConstants
{
    /// <summary>
    /// Maximum number of failed login attempts before account lockout.
    /// </summary>
    public const int MaxFailedLoginAttempts = 5;

    /// <summary>
    /// Account lockout duration in minutes after max failed attempts.
    /// </summary>
    public const int LockoutDurationMinutes = 15;

    /// <summary>
    /// Minimum password length in characters.
    /// </summary>
    public const int MinPasswordLength = 12;

    /// <summary>
    /// JWT token expiration time in minutes.
    /// </summary>
    public const int JwtExpirationMinutes = 60;

    /// <summary>
    /// Refresh token expiration time in days.
    /// </summary>
    public const int RefreshTokenExpirationDays = 30;

    /// <summary>
    /// TOTP code validity window in seconds.
    /// </summary>
    public const int TotpCodeValiditySeconds = 30;
}
```

```csharp
// USP.Core/Constants/EncryptionConstants.cs

namespace USP.Core.Constants;

/// <summary>
/// Encryption-related constants for cryptographic operations.
/// </summary>
public static class EncryptionConstants
{
    /// <summary>
    /// AES-256 key length in bytes.
    /// </summary>
    public const int AesKeyByteLength = 32;  // 256 bits

    /// <summary>
    /// AES-GCM nonce length in bytes.
    /// </summary>
    public const int AesGcmNonceByteLength = 12;

    /// <summary>
    /// AES-GCM authentication tag length in bytes.
    /// </summary>
    public const int AesGcmTagByteLength = 16;

    /// <summary>
    /// PBKDF2 iteration count for password hashing.
    /// </summary>
    public const int Pbkdf2IterationCount = 100000;

    /// <summary>
    /// Salt length in bytes for password hashing.
    /// </summary>
    public const int SaltByteLength = 32;

    /// <summary>
    /// Shamir's Secret Sharing threshold (minimum shares to reconstruct).
    /// </summary>
    public const int ShamirThreshold = 3;

    /// <summary>
    /// Shamir's Secret Sharing total share count.
    /// </summary>
    public const int ShamirTotalShares = 5;
}
```

```csharp
// USP.Core/Constants/ValidationConstants.cs

namespace USP.Core.Constants;

/// <summary>
/// Validation-related constants for input validation.
/// </summary>
public static class ValidationConstants
{
    /// <summary>
    /// Maximum username length in characters.
    /// </summary>
    public const int MaxUsernameLength = 50;

    /// <summary>
    /// Maximum email length in characters.
    /// </summary>
    public const int MaxEmailLength = 255;

    /// <summary>
    /// Maximum secret path length in characters.
    /// </summary>
    public const int MaxSecretPathLength = 512;

    /// <summary>
    /// Maximum secret data size in bytes (1 MB).
    /// </summary>
    public const int MaxSecretDataBytes = 1_048_576;
}
```

### Step 2: Replace Magic Numbers (1 hour)

**Before:**
```csharp
// EncryptionService.cs
public byte[] GenerateKey()
{
    if (kek.Length != 32)  // ❌ Magic number
        throw new ArgumentException("Invalid key length");

    var key = new byte[32];  // ❌ Magic number
    RandomNumberGenerator.Fill(key);
    return key;
}
```

**After:**
```csharp
// EncryptionService.cs
using USP.Core.Constants;

public byte[] GenerateKey()
{
    if (kek.Length != EncryptionConstants.AesKeyByteLength)
        throw new ArgumentException($"Invalid key length. Expected {EncryptionConstants.AesKeyByteLength} bytes.");

    var key = new byte[EncryptionConstants.AesKeyByteLength];
    RandomNumberGenerator.Fill(key);
    return key;
}
```

**Before:**
```csharp
// AuthenticationService.cs
public async Task<AuthResult> AuthenticateAsync(string username, string password)
{
    user.FailedLoginAttempts++;

    if (user.FailedLoginAttempts >= 5)  // ❌ Magic number
    {
        user.LockedUntil = DateTime.UtcNow.AddMinutes(15);  // ❌ Magic number
        await _userRepository.UpdateAsync(user);
        throw new AuthenticationException("Account locked due to too many failed attempts");
    }
}
```

**After:**
```csharp
// AuthenticationService.cs
using USP.Core.Constants;

public async Task<AuthResult> AuthenticateAsync(string username, string password)
{
    user.FailedLoginAttempts++;

    if (user.FailedLoginAttempts >= SecurityConstants.MaxFailedLoginAttempts)
    {
        user.LockedUntil = DateTime.UtcNow.AddMinutes(SecurityConstants.LockoutDurationMinutes);
        await _userRepository.UpdateAsync(user);
        throw new AuthenticationException(
            $"Account locked for {SecurityConstants.LockoutDurationMinutes} minutes due to {SecurityConstants.MaxFailedLoginAttempts} failed login attempts");
    }
}
```

**Before:**
```csharp
// JwtTokenService.cs
public string GenerateToken(User user)
{
    var expiration = DateTime.UtcNow.AddMinutes(60);  // ❌ Magic number
    // ...
}
```

**After:**
```csharp
// JwtTokenService.cs
using USP.Core.Constants;

public string GenerateToken(User user)
{
    var expiration = DateTime.UtcNow.AddMinutes(SecurityConstants.JwtExpirationMinutes);
    // ...
}
```

### Step 3: Search for Remaining Magic Numbers

```bash
cd services/usp

# Find potential magic numbers (excluding 0, 1, -1, 100 which are usually ok)
grep -rn --include="*.cs" -E '\b[2-9][0-9]+\b' src/ | \
  grep -v "// Constant:" | \
  grep -v "const int" | \
  grep -v "const double"

# Review output and extract remaining magic numbers
```

---

## 5. Testing

- [ ] All magic numbers extracted to constants
- [ ] Constants organized in logical classes
- [ ] XML documentation added to all constants
- [ ] All references updated to use constants
- [ ] Code compiles without errors
- [ ] All tests still pass

---

## 6. Compliance Evidence

None (Code quality improvement)

---

## 7. Sign-Off

- [ ] **Backend Engineers:** All magic numbers replaced
- [ ] **Tech Lead:** Constants usage verified

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P2-015**

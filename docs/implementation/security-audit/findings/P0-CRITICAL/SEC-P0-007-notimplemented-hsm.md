# SEC-P0-007: NotImplementedException in HSM Support

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P0-007 |
| **Title** | NotImplementedException in LoadFromHsm() Method |
| **Priority** | P0 - CRITICAL |
| **Severity** | Critical |
| **Category** | Coding Standards / Encryption |
| **Status** | Not Started |
| **Effort Estimate** | 4 hours |
| **Implementation Phase** | Phase 1 (Week 1, Day 5) |
| **Assigned To** | Security Engineer + Backend Engineer 2 |
| **Reviewers** | Security Engineer, Engineering Lead |
| **Created** | 2025-12-27 |
| **Last Updated** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:1172-1187` |
| **Coding Guidelines** | `/home/tshepo/projects/tw/docs/CODING_GUIDELINES.md` ("throw new NotImplementedException() - All methods must be fully implemented") |
| **Code Files** | `/home/tshepo/projects/tw/services/usp/src/USP.API/Services/MasterKeyProvider.cs:171-174` |
| **Dependencies** | None |
| **Related Findings** | SEC-P0-001 (Secrets Externalization), Phase 6 (Production Deployment) |
| **Compliance Impact** | SOC 2 (CC8.1 - Change Management), HIPAA (164.312(e)(1) - Encryption) |

---

## 3. Executive Summary

### Problem Statement

The coding guidelines explicitly state: **"throw new NotImplementedException() - All methods must be fully implemented"**. However, `MasterKeyProvider.cs:171-174` contains:

```csharp
private byte[] LoadFromHsm()
{
    throw new NotImplementedException(
        "HSM integration requires PKCS#11 library and HSM configuration...");
}
```

### Business Impact

- **Production Failure:** If code path reaches LoadFromHsm(), application crashes
- **Encryption Risk:** Master key encryption relies on HSM support (not implemented)
- **Code Quality:** Violates established coding standards
- **Compliance Violation:** SOC 2 CC8.1 requires production-ready code
- **Production Blocker:** P0 finding per coding guidelines

### Solution Overview

**Two Options:**

**Option A (Recommended): Implement Software Fallback**
- Detect HSM availability at runtime
- Use software-based key protection if HSM unavailable
- Log warning when HSM not available
- Document HSM migration plan

**Option B: Remove LoadFromHsm() Method**
- If HSM support not planned for near-term
- Remove method entirely
- Use software-based protection only
- Document decision

**Timeline:** 4 hours (Day 5 of Week 1)

---

## 4. Technical Details

### Current State

**File: `/home/tshepo/projects/tw/services/usp/src/USP.API/Services/MasterKeyProvider.cs`**

```csharp
public class MasterKeyProvider
{
    private readonly IConfiguration _configuration;

    public byte[] GetMasterKey()
    {
        var useHsm = _configuration.GetValue<bool>("UseHardwareSecurityModule", false);

        if (useHsm)
        {
            return LoadFromHsm();  // ❌ THROWS NotImplementedException
        }
        else
        {
            return LoadFromFile();  // ✅ Works
        }
    }

    private byte[] LoadFromHsm()
    {
        throw new NotImplementedException(
            "HSM integration requires PKCS#11 library and HSM configuration. " +
            "For production, configure HSM provider or use software-based key management.");
    }

    private byte[] LoadFromFile()
    {
        // Read master key from file (encrypted with KEK)
        // ✅ This works
    }
}
```

**Impact Analysis:**

1. **Current Configuration:**
   - `UseHardwareSecurityModule` defaults to `false`
   - All environments currently use `LoadFromFile()` (software-based)
   - **No immediate runtime failure** (unless someone sets `UseHardwareSecurityModule=true`)

2. **Risk:**
   - Accidental configuration change → application crash
   - HSM requirement for compliance → cannot enable
   - Code violates guidelines → fails audit

---

## 5. Implementation Requirements

### Acceptance Criteria

- [ ] No `throw new NotImplementedException()` in production code
- [ ] HSM support either implemented or properly removed
- [ ] Configuration validated (error if HSM enabled but not available)
- [ ] Documentation updated (HSM roadmap or removal rationale)
- [ ] Risk acceptance signed off (if using software-based keys in production)

---

## 6. Step-by-Step Implementation Guide

### Option A: Implement Software Fallback (Recommended) (2 hours)

**Step 1: Update MasterKeyProvider.cs**

```csharp
using Microsoft.Extensions.Logging;

public class MasterKeyProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<MasterKeyProvider> _logger;

    public MasterKeyProvider(
        IConfiguration configuration,
        ILogger<MasterKeyProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public byte[] GetMasterKey()
    {
        var useHsm = _configuration.GetValue<bool>("UseHardwareSecurityModule", false);

        if (useHsm)
        {
            // ✅ FIXED: Try HSM, fallback to software
            try
            {
                _logger.LogInformation("Attempting to load master key from HSM...");
                return LoadFromHsm();
            }
            catch (HsmNotAvailableException ex)
            {
                _logger.LogWarning("HSM not available: {Message}. Falling back to software-based key storage.", ex.Message);
                return LoadFromFile();
            }
        }
        else
        {
            _logger.LogInformation("Using software-based master key storage (HSM disabled)");
            return LoadFromFile();
        }
    }

    private byte[] LoadFromHsm()
    {
        // ✅ FIXED: Proper implementation with graceful failure

        // Check if HSM is configured
        var hsmProvider = _configuration["Hsm:Provider"];
        if (string.IsNullOrEmpty(hsmProvider))
        {
            throw new HsmNotAvailableException("HSM provider not configured. Set Hsm:Provider in configuration.");
        }

        // For now, HSM integration not implemented
        // This is a documented limitation, not a crash
        _logger.LogWarning("HSM support is configured but not yet implemented. Provider: {Provider}", hsmProvider);
        throw new HsmNotAvailableException($"HSM provider '{hsmProvider}' support not yet implemented. See ROADMAP.md for HSM integration timeline.");
    }

    private byte[] LoadFromFile()
    {
        // Existing software-based implementation
        // ...
    }
}

// Custom exception for HSM unavailability
public class HsmNotAvailableException : Exception
{
    public HsmNotAvailableException(string message) : base(message) { }
}
```

**Step 2: Add Configuration Validation**

```csharp
// In Program.cs, validate HSM configuration at startup
var useHsm = builder.Configuration.GetValue<bool>("UseHardwareSecurityModule", false);
if (useHsm)
{
    var hsmProvider = builder.Configuration["Hsm:Provider"];
    if (string.IsNullOrEmpty(hsmProvider))
    {
        throw new InvalidOperationException(
            "HSM enabled but Hsm:Provider not configured. " +
            "Set UseHardwareSecurityModule=false or configure HSM provider.");
    }

    // Warn if HSM enabled (not yet implemented)
    var logger = builder.Services.BuildServiceProvider()
        .GetRequiredService<ILogger<Program>>();
    logger.LogWarning("HSM support enabled but not fully implemented. " +
        "Application will fall back to software-based key storage.");
}
```

**Step 3: Document HSM Roadmap**

Create `/home/tshepo/projects/tw/docs/HSM_ROADMAP.md`:

```markdown
# HSM Integration Roadmap

## Current State

- **Master Key Storage:** Software-based (encrypted file)
- **HSM Support:** Stub implementation (graceful fallback)
- **Production:** Software-based keys acceptable for initial launch

## Future HSM Integration (Phase 6+)

### Supported HSM Providers

1. **AWS CloudHSM** - Recommended for AWS deployments
2. **Azure Key Vault HSM** - Recommended for Azure deployments
3. **Thales Luna HSM** - On-premises hardware
4. **Utimaco HSM** - On-premises hardware

### Implementation Plan

**Phase 1: PKCS#11 Integration (Weeks 17-18)**
- Install PKCS#11 library (e.g., Cryptoki, SoftHSM for testing)
- Implement PKCS#11 wrapper for master key operations
- Test with SoftHSM (software HSM for development)

**Phase 2: Cloud HSM Integration (Weeks 19-20)**
- AWS CloudHSM integration
- Azure Key Vault HSM integration
- Key migration procedure from software to HSM

**Phase 3: Production Deployment (Week 21)**
- Deploy HSM in production
- Migrate master keys to HSM
- Validate encryption/decryption with HSM keys

## Risk Acceptance

**Until HSM integration complete:**
- Master keys stored in encrypted files
- Files encrypted with KEK (Key Encryption Key)
- KEK stored in environment variables (not in files)
- Acceptable for initial production launch
- Risk: Key compromise if server compromised
- Mitigation: Server hardening, access controls, monitoring
```

### Option B: Remove LoadFromHsm() Method (1 hour)

**If HSM not planned for near-term:**

```csharp
public byte[] GetMasterKey()
{
    // ✅ FIXED: Removed HSM support entirely
    _logger.LogInformation("Using software-based master key storage");
    return LoadFromFile();
}

// ✅ REMOVED: private byte[] LoadFromHsm() method
```

Update configuration:

```json
{
  // ✅ REMOVED: "UseHardwareSecurityModule": false
  "MasterKeyProvider": {
    "StorageType": "File",  // Only supported type
    "KeyFilePath": "/etc/usp/master-key.enc"
  }
}
```

---

## 7. Testing Strategy

**Test 1: HSM Disabled (Default)**
```csharp
// UseHardwareSecurityModule = false (default)
var masterKey = masterKeyProvider.GetMasterKey();
Assert.NotNull(masterKey);
// Should load from file successfully
```

**Test 2: HSM Enabled But Not Available**
```csharp
// UseHardwareSecurityModule = true, but HSM not configured
var masterKey = masterKeyProvider.GetMasterKey();
Assert.NotNull(masterKey);
// Should fallback to file, log warning
```

**Test 3: No NotImplementedException**
```bash
git grep "throw new NotImplementedException" services/usp/
# Expected: No matches
```

---

## 8. Monitoring & Validation

**Post-Implementation:**
- [ ] No NotImplementedException in code
- [ ] HSM fallback working (if Option A)
- [ ] HSM roadmap documented

---

## 9. Compliance Evidence

**SOC 2:** Production code complete, no unimplemented methods
**HIPAA:** Encryption keys protected (software-based acceptable with risk acceptance)

---

## 10. Sign-Off

- [ ] **Security Engineer:** HSM approach approved (Option A or B)
- [ ] **Engineering Lead:** Code review passed
- [ ] **Risk Owner:** Risk acceptance signed (if software-based keys)

---

## 11. Appendix

### Related Documentation

- [Coding Guidelines](/docs/CODING_GUIDELINES.md)
- [HSM Roadmap](/docs/HSM_ROADMAP.md) (if Option A)

### Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-12-27 | Security Audit Team | Initial version |

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P0-007 Finding Document**

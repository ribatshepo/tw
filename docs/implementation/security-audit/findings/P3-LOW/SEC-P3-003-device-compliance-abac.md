# SEC-P3-003: Device Compliance ABAC Missing

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P3-003 |
| **Title** | Device Compliance Attribute-Based Access Control Not Implemented |
| **Priority** | P3 - LOW |
| **Severity** | Low |
| **Category** | Authorization / Security |
| **Status** | Not Started |
| **Effort Estimate** | 8 hours |
| **Implementation Phase** | Phase 4 (Week 4+, Nice to Have) |
| **Assigned To** | Backend Engineer + Security Engineer |
| **Created** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:700` |
| **Code Files** | Authorization service |
| **Dependencies** | SEC-P1-008 (Granular Authorization) |
| **Compliance Impact** | SOC 2 (CC6.1 - Enhanced Access Control) |

---

## 3. Executive Summary

### Problem

Authorization system supports RBAC (Role-Based Access Control) but device compliance ABAC (Attribute-Based Access Control) is not implemented.

### Impact

- **No Device Trust:** Cannot enforce access based on device security posture
- **Missing Zero Trust:** Zero Trust requires device compliance checks
- **Compliance Gap:** Some regulations require device-based access controls

### Solution

Implement ABAC policies that check device attributes (OS version, encryption status, antivirus, etc.) before granting access.

---

## 4. Implementation Guide

### Step 1: Define Device Attributes Model (2 hours)

```csharp
// Models/DeviceAttributes.cs

public class DeviceAttributes
{
    public string DeviceId { get; set; }
    public string DeviceType { get; set; } // Desktop, Mobile, Tablet
    public string OperatingSystem { get; set; } // Windows, macOS, Linux, iOS, Android
    public string OsVersion { get; set; }
    public bool DiskEncrypted { get; set; }
    public bool AntivirusEnabled { get; set; }
    public bool FirewallEnabled { get; set; }
    public bool ScreenLockEnabled { get; set; }
    public bool IsJailbroken { get; set; }
    public DateTime LastSecurityScan { get; set; }
    public bool IsManagedDevice { get; set; }
    public string ComplianceStatus { get; set; } // Compliant, NonCompliant, Unknown
}
```

### Step 2: Implement Device Registration (2 hours)

```csharp
// Controllers/DeviceController.cs

[ApiController]
[Route("api/v1/devices")]
public class DeviceController : ControllerBase
{
    private readonly IDeviceService _deviceService;

    [HttpPost("register")]
    public async Task<IActionResult> RegisterDevice([FromBody] DeviceRegistrationRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var deviceAttributes = new DeviceAttributes
        {
            DeviceId = request.DeviceId,
            DeviceType = request.DeviceType,
            OperatingSystem = request.OperatingSystem,
            OsVersion = request.OsVersion,
            DiskEncrypted = request.DiskEncrypted,
            AntivirusEnabled = request.AntivirusEnabled,
            FirewallEnabled = request.FirewallEnabled,
            ScreenLockEnabled = request.ScreenLockEnabled,
            IsJailbroken = request.IsJailbroken,
            IsManagedDevice = request.IsManagedDevice
        };

        // Evaluate compliance
        var complianceStatus = await _deviceService.EvaluateComplianceAsync(deviceAttributes);
        deviceAttributes.ComplianceStatus = complianceStatus;

        await _deviceService.RegisterDeviceAsync(userId, deviceAttributes);

        return Ok(new { deviceId = deviceAttributes.DeviceId, complianceStatus });
    }

    [HttpGet("{deviceId}/compliance")]
    public async Task<IActionResult> CheckCompliance(string deviceId)
    {
        var compliance = await _deviceService.GetComplianceStatusAsync(deviceId);
        return Ok(compliance);
    }
}
```

### Step 3: Implement ABAC Policy Evaluation (3 hours)

```csharp
// Services/AbacPolicyService.cs

public class AbacPolicyService
{
    public async Task<bool> EvaluatePolicyAsync(
        string userId,
        string resource,
        string action,
        DeviceAttributes deviceAttributes)
    {
        // Get ABAC policies for this resource/action
        var policies = await GetPoliciesAsync(resource, action);

        foreach (var policy in policies)
        {
            if (!EvaluateDeviceCompliance(policy, deviceAttributes))
            {
                return false; // Device not compliant with policy
            }
        }

        return true;
    }

    private bool EvaluateDeviceCompliance(AbacPolicy policy, DeviceAttributes device)
    {
        // Example policies:

        // Require disk encryption for accessing secrets
        if (policy.RequiresDiskEncryption && !device.DiskEncrypted)
            return false;

        // Require antivirus for accessing production data
        if (policy.RequiresAntivirus && !device.AntivirusEnabled)
            return false;

        // Block jailbroken/rooted devices
        if (policy.BlockJailbrokenDevices && device.IsJailbroken)
            return false;

        // Require minimum OS version
        if (policy.MinimumOsVersion != null)
        {
            if (!IsOsVersionCompliant(device.OperatingSystem, device.OsVersion, policy.MinimumOsVersion))
                return false;
        }

        // Require managed devices for sensitive resources
        if (policy.RequiresManagedDevice && !device.IsManagedDevice)
            return false;

        return true;
    }

    private bool IsOsVersionCompliant(string os, string currentVersion, string minimumVersion)
    {
        // Version comparison logic
        return Version.Parse(currentVersion) >= Version.Parse(minimumVersion);
    }
}
```

### Step 4: Update Authorization Middleware (1 hour)

```csharp
// Middleware/AbacAuthorizationMiddleware.cs

public class AbacAuthorizationMiddleware
{
    private readonly RequestDelegate _next;

    public async Task InvokeAsync(
        HttpContext context,
        IAbacPolicyService abacService,
        IDeviceService deviceService)
    {
        // Extract device ID from request headers
        var deviceId = context.Request.Headers["X-Device-Id"].FirstOrDefault();

        if (string.IsNullOrEmpty(deviceId))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Device ID required");
            return;
        }

        // Get device attributes
        var deviceAttributes = await deviceService.GetDeviceAttributesAsync(deviceId);

        if (deviceAttributes == null)
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsync("Device not registered");
            return;
        }

        // Check device compliance
        if (deviceAttributes.ComplianceStatus != "Compliant")
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsync("Device not compliant with security policies");
            return;
        }

        // Store device attributes in HttpContext for downstream use
        context.Items["DeviceAttributes"] = deviceAttributes;

        await _next(context);
    }
}
```

---

## 5. Testing

- [ ] Device registration endpoint working
- [ ] Device attributes stored in database
- [ ] Compliance evaluation logic correct
- [ ] ABAC policies enforced
- [ ] Non-compliant devices blocked
- [ ] Compliant devices allowed access

---

## 6. Compliance Evidence

**SOC 2 CC6.1:** Device-based access controls implemented (Zero Trust)

---

## 7. Sign-Off

- [ ] **Backend Engineer:** ABAC implemented
- [ ] **Security Engineer:** Policies validated

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P3-003**

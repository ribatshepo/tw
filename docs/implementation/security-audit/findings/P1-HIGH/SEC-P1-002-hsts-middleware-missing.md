# SEC-P1-002: HSTS Middleware Missing

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P1-002 |
| **Title** | HTTP Strict Transport Security (HSTS) Middleware Not Configured |
| **Priority** | P1 - HIGH |
| **Severity** | High |
| **Category** | TLS/HTTPS Security |
| **Status** | Not Started |
| **Effort Estimate** | 2 hours |
| **Implementation Phase** | Phase 2 (Week 2, Day 8-9) |
| **Assigned To** | Backend Engineer |
| **Created** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:167-171` |
| **Code Files** | `/home/tshepo/projects/tw/services/usp/src/USP.API/Program.cs` |
| **Dependencies** | Blocked by SEC-P0-008 (TrustServerCertificate), SEC-P1-001 (HTTPS Metrics) |
| **Compliance Impact** | SOC 2 (CC6.6), PCI-DSS (Req 4.1) |

---

## 3. Executive Summary

### Problem

No `app.UseHsts()` middleware configured in Program.cs. Browsers won't enforce HTTPS-only connections until after the first HTTP → HTTPS redirect.

### Impact

- **SSL Stripping Attack:** First connection can be intercepted and forced to HTTP
- **Session Hijacking:** Cookies sent over initial HTTP connection can be stolen
- **MITM:** Attackers can intercept first request before HSTS kicks in

### Solution

Add `app.UseHsts()` with `max-age=31536000` (1 year), `includeSubDomains`, and `preload`.

---

## 4. Implementation Guide

### Step 1: Add HSTS Middleware (30 minutes)

```csharp
// Program.cs

var app = builder.Build();

// Add HSTS middleware (BEFORE other middleware)
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();  // ✅ ADD THIS
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
```

### Step 2: Configure HSTS Options (30 minutes)

```csharp
// Program.cs

builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);  // 1 year
    options.IncludeSubDomains = true;
    options.Preload = true;
});
```

### Step 3: Test HSTS Header (30 minutes)

```bash
curl -I https://localhost:5001/health -k | grep -i strict-transport-security
# Expected: Strict-Transport-Security: max-age=31536000; includeSubDomains; preload
```

### Step 4: (Optional) Submit to HSTS Preload List (30 minutes)

Visit https://hstspreload.org/ and submit your production domain.

---

## 5. Testing

- [ ] HSTS header present in all HTTPS responses
- [ ] max-age=31536000 configured
- [ ] includeSubDomains enabled
- [ ] Browser enforces HTTPS on subsequent requests

---

## 6. Compliance Evidence

**SOC 2 CC6.6:** HTTPS enforced via HSTS
**PCI-DSS Req 4.1:** Strong cryptography enforced

---

## 7. Sign-Off

- [ ] **Backend Engineer:** HSTS configured
- [ ] **Security:** HSTS header verified

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P1-002**

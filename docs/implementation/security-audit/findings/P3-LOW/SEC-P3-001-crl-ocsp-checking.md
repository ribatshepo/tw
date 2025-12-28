# SEC-P3-001: CRL/OCSP Checking Missing

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P3-001 |
| **Title** | Certificate Chain Validation Lacks CRL/OCSP Revocation Checking |
| **Priority** | P3 - LOW |
| **Severity** | Low |
| **Category** | TLS/HTTPS Security |
| **Status** | Not Started |
| **Effort Estimate** | 4 hours |
| **Implementation Phase** | Phase 4 (Week 4+, Nice to Have) |
| **Assigned To** | Security Engineer |
| **Created** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:188-189` |
| **Code Files** | TLS certificate validation code |
| **Dependencies** | None |
| **Related Findings** | SEC-P1-012 (Certificate Automation) |
| **Compliance Impact** | SOC 2 (CC6.6 - Enhanced Security) |

---

## 3. Executive Summary

### Problem

TLS certificate validation does not check Certificate Revocation Lists (CRL) or Online Certificate Status Protocol (OCSP) for certificate revocation status.

### Impact

- **Compromised Certificates:** Revoked certificates still accepted
- **Security Gap:** Cannot detect compromised private keys
- **Compliance Enhancement:** Additional security layer for high-security environments

### Solution

Implement CRL/OCSP checking in TLS certificate validation with configurable enforcement.

---

## 4. Implementation Guide

### Step 1: Configure OCSP Stapling (2 hours)

```csharp
// Program.cs - Configure Kestrel with OCSP stapling

builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureHttpsDefaults(httpsOptions =>
    {
        httpsOptions.ServerCertificate = LoadCertificate();

        // Enable OCSP stapling
        httpsOptions.SslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12;
        httpsOptions.CheckCertificateRevocation = true;  // Enable CRL/OCSP checks

        httpsOptions.OnAuthenticate = (context, sslOptions) =>
        {
            sslOptions.CertificateRevocationCheckMode = X509RevocationMode.Online;
            sslOptions.RevocationMode = X509RevocationFlag.EntireChain;
        };
    });
});
```

### Step 2: Implement Custom Certificate Validator (1.5 hours)

```csharp
// Services/CertificateValidationService.cs

using System.Net.Http;
using System.Security.Cryptography.X509Certificates;

public class CertificateValidationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CertificateValidationService> _logger;

    public async Task<bool> ValidateCertificateAsync(X509Certificate2 certificate)
    {
        // Check CRL
        var crlValid = await CheckCrlAsync(certificate);
        if (!crlValid)
        {
            _logger.LogWarning("Certificate {Thumbprint} revoked via CRL", certificate.Thumbprint);
            return false;
        }

        // Check OCSP
        var ocspValid = await CheckOcspAsync(certificate);
        if (!ocspValid)
        {
            _logger.LogWarning("Certificate {Thumbprint} revoked via OCSP", certificate.Thumbprint);
            return false;
        }

        return true;
    }

    private async Task<bool> CheckCrlAsync(X509Certificate2 certificate)
    {
        // Extract CRL distribution points from certificate
        var crlExtension = certificate.Extensions
            .OfType<X509Extension>()
            .FirstOrDefault(e => e.Oid?.Value == "2.5.29.31"); // CRL Distribution Points

        if (crlExtension == null)
        {
            _logger.LogDebug("No CRL distribution points in certificate");
            return true; // No CRL to check
        }

        // Download and verify CRL
        // Implementation details...
        return true;
    }

    private async Task<bool> CheckOcspAsync(X509Certificate2 certificate)
    {
        // Extract OCSP responder URL from certificate
        var ocspExtension = certificate.Extensions
            .OfType<X509Extension>()
            .FirstOrDefault(e => e.Oid?.Value == "1.3.6.1.5.5.7.1.1"); // Authority Info Access

        if (ocspExtension == null)
        {
            _logger.LogDebug("No OCSP responder URL in certificate");
            return true; // No OCSP to check
        }

        // Query OCSP responder
        // Implementation details...
        return true;
    }
}
```

### Step 3: Add Configuration (30 minutes)

```json
// appsettings.json

{
  "CertificateValidation": {
    "EnableCrlChecking": true,
    "EnableOcspChecking": true,
    "CacheRevocationResults": true,
    "CacheDurationMinutes": 60,
    "OcspTimeoutSeconds": 5,
    "CrlTimeoutSeconds": 10
  }
}
```

---

## 5. Testing

- [ ] CRL checking enabled
- [ ] OCSP checking enabled
- [ ] Revoked certificates rejected
- [ ] Valid certificates accepted
- [ ] Timeouts handled gracefully
- [ ] Caching reduces latency

---

## 6. Compliance Evidence

**SOC 2 CC6.6:** Enhanced certificate validation with revocation checking

---

## 7. Sign-Off

- [ ] **Security Engineer:** CRL/OCSP checking implemented
- [ ] **DevOps:** Production certificates tested

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P3-001**

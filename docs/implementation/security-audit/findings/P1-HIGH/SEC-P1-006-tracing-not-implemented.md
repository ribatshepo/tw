# SEC-P1-006: Distributed Tracing Not Implemented

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P1-006 |
| **Title** | OpenTelemetry Distributed Tracing Not Initialized |
| **Priority** | P1 - HIGH |
| **Severity** | High |
| **Category** | Monitoring/Observability |
| **Status** | Not Started |
| **Effort Estimate** | 6 hours |
| **Implementation Phase** | Phase 2 (Week 2, Day 13-14) |
| **Assigned To** | Backend Engineer + DevOps Engineer |
| **Created** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:849-870` |
| **Code Files** | `Program.cs`, `appsettings.json` (Jaeger config present but unused) |
| **Dependencies** | Blocked by SEC-P1-007 (Observability Stack - Jaeger deployment) |
| **Compliance Impact** | SOC 2 (CC7.2) |

---

## 3. Executive Summary

### Problem

Jaeger configuration present in `appsettings.json` but OpenTelemetry never initialized in `Program.cs`. No trace context propagation, no ActivitySource, no spans.

### Impact

- **No Distributed Tracing:** Cannot trace requests across services (NCCS → UCCP → USP)
- **No Performance Debugging:** Cannot identify slow database queries, API calls
- **No Service Dependencies:** Cannot visualize service communication patterns

### Solution

Install OpenTelemetry packages, initialize tracing in Program.cs, create custom spans for key operations.

---

## 4. Implementation Guide

### Step 1: Install Packages (15 minutes)

```bash
cd /home/tshepo/projects/tw/services/usp/src/USP.API

dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
dotnet add package OpenTelemetry.Instrumentation.Http
dotnet add package OpenTelemetry.Instrumentation.EntityFrameworkCore
dotnet add package OpenTelemetry.Exporter.Jaeger
```

### Step 2: Initialize OpenTelemetry (2 hours)

```csharp
// Program.cs
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: "USP", serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddSource("USP.Vault")
        .AddSource("USP.Secrets")
        .AddJaegerExporter(options =>
        {
            options.AgentHost = builder.Configuration["JaegerAgentHost"];
            options.AgentPort = int.Parse(builder.Configuration["JaegerAgentPort"]);
        }));
```

### Step 3: Create Custom Spans (2 hours)

```csharp
// VaultService.cs
using System.Diagnostics;

public class VaultService
{
    private static readonly ActivitySource ActivitySource = new("USP.Vault");

    public async Task UnsealAsync(string key)
    {
        using var activity = ActivitySource.StartActivity("Vault.Unseal");
        activity?.SetTag("vault.key_number", 1);

        // Unseal logic
        await _repository.SubmitUnsealKeyAsync(key);

        activity?.SetTag("vault.unsealed", _vault.IsUnsealed);
    }
}
```

### Step 4: Test Distributed Tracing (1 hour)

```bash
# Make API request
curl -X POST https://localhost:5001/api/v1/vault/seal/unseal \
  -H "Content-Type: application/json" \
  -H "X-Vault-Token: $TOKEN" \
  -d '{"key":"key1"}' -k

# View trace in Jaeger UI
open http://localhost:16686

# Expected: See trace with spans for HTTP request, database query, vault operations
```

---

## 5. Testing

- [ ] OpenTelemetry initialized
- [ ] Traces visible in Jaeger UI
- [ ] HTTP requests traced automatically
- [ ] Database queries traced automatically
- [ ] Custom spans for vault/secrets operations

---

## 6. Compliance Evidence

**SOC 2 CC7.2:** System monitoring includes distributed tracing

---

## 7. Sign-Off

- [ ] **Backend Engineer:** OpenTelemetry configured
- [ ] **DevOps:** Jaeger receiving traces

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P1-006**

# Authorization & Policy Engine Architecture

## System Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                         Client Applications                          │
│              (Web UI, Mobile Apps, API Clients)                     │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             │ HTTP/REST
                             │
┌────────────────────────────▼────────────────────────────────────────┐
│                   Authorization Controller                           │
│                                                                      │
│  Endpoints:                                                          │
│  • POST /api/authz/abac/evaluate                                    │
│  • POST /api/authz/hcl/evaluate                                     │
│  • POST /api/authz/check-batch                                      │
│  • POST /api/authz/column-access/check                              │
│  • POST /api/authz/context/evaluate                                 │
│  • POST /api/authz/policies/simulate                                │
│  • GET  /api/authz/policies/{id}/conflicts                          │
└──────┬──────────┬──────────┬──────────┬──────────┬─────────────────┘
       │          │          │          │          │
       │          │          │          │          │
       ▼          ▼          ▼          ▼          ▼
┌──────────┐ ┌─────────┐ ┌──────────┐ ┌─────────┐ ┌──────────────┐
│  ABAC    │ │   HCL   │ │ Column   │ │ Context │ │ Authorization│
│  Engine  │ │ Policy  │ │ Security │ │ Evaluator│ │ Flow Service │
│          │ │Evaluator│ │  Engine  │ │          │ │              │
└─────┬────┘ └────┬────┘ └────┬─────┘ └────┬────┘ └──────┬───────┘
      │           │           │            │             │
      │           │           │            │             │
      └───────────┴───────────┴────────────┴─────────────┘
                              │
                              │
        ┌─────────────────────┴─────────────────────┐
        │                                           │
        ▼                                           ▼
┌────────────────┐                        ┌─────────────────┐
│ ApplicationDb  │                        │  Memory Cache   │
│    Context     │                        │                 │
│                │                        │ • Policy Cache  │
│ • Users        │                        │ • Rule Cache    │
│ • Roles        │                        │ • TTL: 15min    │
│ • Permissions  │                        │                 │
│ • Policies     │                        └─────────────────┘
│ • RiskProfiles │
│ • TrustedDevices│
│ • Secrets      │
└────────────────┘
```

---

## Component Interaction Flow

### 1. ABAC Evaluation Flow

```
Client Request
     │
     ▼
[AuthorizationController]
     │
     ├──→ Extract Request Context
     │
     ▼
[AbacEngine.EvaluateAsync()]
     │
     ├──→ ExtractAttributesAsync()
     │    │
     │    ├──→ Subject Attributes (20+ types)
     │    │    • User identity, roles, department
     │    │    • Security status, MFA enabled
     │    │    • Risk score, clearance level
     │    │
     │    ├──→ Resource Attributes (12+ types)
     │    │    • Classification, sensitivity
     │    │    • Owner, tags, workspace
     │    │
     │    └──→ Environment Attributes (15+ types)
     │         • Time, location, network
     │         • Device compliance, IP address
     │
     ├──→ Load Active Policies
     │    │
     │    └──→ [ApplicationDbContext]
     │         • Query AccessPolicies table
     │         • Filter by IsActive = true
     │
     ├──→ Evaluate Each Policy
     │    │
     │    ├──→ Parse Policy Rules (JSON)
     │    │
     │    ├──→ Match Conditions
     │    │    • Subject matches?
     │    │    • Resource matches?
     │    │    • Action matches?
     │    │    • Environment matches?
     │    │
     │    └──→ Apply Combining Algorithm
     │         • Deny-overrides
     │         • Permit-overrides
     │         • First-applicable
     │
     └──→ Return Decision
          • Allowed: true/false
          • Reasons: [list of reasons]
          • Applied Policies: [policy names]
```

---

### 2. HCL Policy Evaluation Flow

```
Client Request
     │
     ▼
[AuthorizationController]
     │
     ▼
[HclPolicyEvaluator.EvaluateAsync()]
     │
     ├──→ Load User & Roles
     │    │
     │    └──→ [ApplicationDbContext]
     │         • Get User with Roles
     │         • Extract metadata
     │
     ├──→ Build Template Context
     │    │
     │    └──→ Create variable map
     │         • ${user.department}
     │         • ${user.team}
     │         • ${resource.path}
     │
     ├──→ Load HCL Policies
     │    │
     │    └──→ Check Cache
     │         │
     │         ├─[Cache Hit]──→ Use cached policy
     │         │
     │         └─[Cache Miss]─→ Parse HCL
     │              │           • Extract path blocks
     │              │           • Parse capabilities
     │              │           • Cache for 15 min
     │              │
     │              └──→ [MemoryCache]
     │
     ├──→ For Each Path Policy:
     │    │
     │    ├──→ Substitute Template Variables
     │    │    • Replace ${var} with actual values
     │    │
     │    ├──→ Match Path Pattern
     │    │    • Exact match
     │    │    • Wildcard match (*)
     │    │    • Multi-segment match (+)
     │    │
     │    └──→ Check Capabilities
     │         • Read, write, delete, list
     │         • Sudo, deny, patch
     │
     └──→ Return Decision
          • Authorized: true/false
          • Matched Rules: [rule paths]
          • Deny Reason: explanation
```

---

### 3. Column Security Flow

```
Client Request (Query Data)
     │
     ▼
[AuthorizationController]
     │
     ▼
[ColumnSecurityEngine.CheckColumnAccessAsync()]
     │
     ├──→ Load User Roles
     │    │
     │    └──→ [ApplicationDbContext]
     │         • Get UserRoles
     │
     ├──→ Get Column Rules
     │    │
     │    └──→ [In-Memory Rules Storage]
     │         • Filter by table & operation
     │         • Order by priority
     │
     ├──→ For Each Requested Column:
     │    │
     │    ├──→ Find Applicable Rules
     │    │    • Match column name
     │    │    • Match operation (read/write)
     │    │
     │    ├──→ Evaluate Rules
     │    │    │
     │    │    ├──→ Check Denied Roles (explicit deny)
     │    │    │
     │    │    └──→ Check Allowed Roles
     │    │
     │    └──→ Determine Restriction Type
     │         • allow → Add to allowed list
     │         • deny → Add to denied list
     │         • mask → Add to restrictions
     │         • redact → Add to restrictions
     │         • tokenize → Add to restrictions
     │
     └──→ Return Response
          • AllowedColumns: [list]
          • DeniedColumns: [list]
          • ColumnRestrictions: {column: type}

Then Apply Masking:
     │
     ▼
[ColumnSecurityEngine.ApplyMaskingAsync()]
     │
     ├──→ For Each Column:
     │    │
     │    ├──→ If DENIED: Remove from result
     │    │
     │    ├──→ If MASK:
     │    │    • Email: j***e@example.com
     │    │    • Phone: ***-***-1234
     │    │    • SSN: ***-**-6789
     │    │    • Credit Card: **** **** **** 9010
     │    │
     │    ├──→ If REDACT:
     │    │    • Replace with [REDACTED]
     │    │
     │    └──→ If TOKENIZE:
     │         • Generate token: TOK_[hash]
     │
     └──→ Return Masked Data
```

---

### 4. Context Evaluation Flow

```
Client Request (with Context)
     │
     ▼
[AuthorizationController]
     │
     ▼
[ContextEvaluator.EvaluateContextAsync()]
     │
     ├──→ Get Context Policy
     │    │
     │    └──→ [In-Memory Policy Storage]
     │         • Match by resource type
     │
     ├──→ TIME-BASED CHECK
     │    │
     │    ├──→ Check Day of Week
     │    │    • Is today allowed?
     │    │
     │    └──→ Check Time of Day
     │         • Is current hour allowed?
     │         • Business hours only?
     │
     ├──→ LOCATION-BASED CHECK
     │    │
     │    ├──→ Extract Country from Geo
     │    │
     │    ├──→ Check Denied Countries (explicit deny)
     │    │
     │    ├──→ Check Allowed Countries
     │    │
     │    └──→ Check Network Zone
     │         • internal, vpn → allowed
     │         • external, dmz → check policy
     │
     ├──→ DEVICE-BASED CHECK
     │    │
     │    ├──→ Check Device Compliance
     │    │    │
     │    │    └──→ [ApplicationDbContext]
     │    │         • Query TrustedDevices
     │    │         • Verify IsTrusted & IsActive
     │    │
     │    └──→ Check Device Type
     │         • Windows, Mac, Linux allowed?
     │
     ├──→ RISK-BASED CHECK
     │    │
     │    ├──→ Calculate Risk Score
     │    │    │
     │    │    ├──→ Base Risk (from UserRiskProfile)
     │    │    ├──→ + Device Risk (non-compliant: +25)
     │    │    ├──→ + Travel Risk (impossible: +40)
     │    │    ├──→ + Location Risk (unknown: +15)
     │    │    ├──→ + Network Risk (external: +10)
     │    │    └──→ + Time Risk (off-hours: +10)
     │    │
     │    ├──→ Check Max Risk Threshold
     │    │
     │    └──→ Check Impossible Travel
     │
     ├──→ DETERMINE FINAL DECISION
     │    │
     │    ├──→ All checks passed?
     │    │    │
     │    │    ├─[YES]──→ Check Adaptive Requirements
     │    │    │          │
     │    │    │          ├──→ High Risk (>70)?
     │    │    │          │    • Require MFA
     │    │    │          │
     │    │    │          └──→ Very High Risk (>85)?
     │    │    │               • Require Approval
     │    │    │
     │    │    └─[NO]───→ DENY
     │    │
     │    └──→ Calculate Risk Level
     │         • < 30: Low
     │         • 30-60: Medium
     │         • 60-85: High
     │         • > 85: Critical
     │
     └──→ Return Response
          • Allowed: true/false
          • RiskScore: 0-100
          • RiskLevel: low/medium/high/critical
          • RequiredAction: null/mfa/approval/deny
          • Reasons: [list]
          • ContextChecks: {check: pass/fail}
```

---

## Data Flow Diagram

```
┌──────────────┐
│ User Request │
│              │
│ Headers:     │
│ - User-Agent │
│ - IP Address │
│              │
│ Body:        │
│ - Action     │
│ - Resource   │
└──────┬───────┘
       │
       ▼
┌──────────────────────────────────────┐
│   Extract Request Context            │
│                                      │
│ • User ID from JWT                   │
│ • IP Address from connection         │
│ • Device info from User-Agent        │
│ • Geo location (if available)        │
└──────┬───────────────────────────────┘
       │
       ▼
┌──────────────────────────────────────┐
│   Load User Profile                  │
│                                      │
│ Database Query:                      │
│ SELECT *                             │
│ FROM Users u                         │
│ JOIN UserRoles ur ON u.Id = ur.UserId│
│ JOIN Roles r ON ur.RoleId = r.Id     │
│ JOIN UserRiskProfiles urp            │
│   ON u.Id = urp.UserId               │
│ WHERE u.Id = @userId                 │
└──────┬───────────────────────────────┘
       │
       ▼
┌──────────────────────────────────────┐
│   Extract Attributes                 │
│                                      │
│ Subject:                             │
│ ┌────────────────────────────────┐   │
│ │ • user_id: guid                │   │
│ │ • username: string             │   │
│ │ • department: engineering      │   │
│ │ • clearance_level: confidential│   │
│ │ • risk_score: 25               │   │
│ │ • roles: [DataEngineer]        │   │
│ └────────────────────────────────┘   │
│                                      │
│ Environment:                         │
│ ┌────────────────────────────────┐   │
│ │ • ip_address: 192.168.1.100    │   │
│ │ • network_zone: internal       │   │
│ │ • is_business_hours: true      │   │
│ │ • device_compliant: true       │   │
│ │ • geo_location: US/Seattle     │   │
│ └────────────────────────────────┘   │
└──────┬───────────────────────────────┘
       │
       ▼
┌──────────────────────────────────────┐
│   Load Policies                      │
│                                      │
│ Database Query:                      │
│ SELECT *                             │
│ FROM AccessPolicies                  │
│ WHERE PolicyType = 'ABAC'            │
│   AND IsActive = true                │
│ ORDER BY Priority DESC               │
│                                      │
│ Result: [Policy1, Policy2, Policy3]  │
└──────┬───────────────────────────────┘
       │
       ▼
┌──────────────────────────────────────┐
│   Evaluate Policies                  │
│                                      │
│ For Policy1:                         │
│   Rule1: ALLOW (conditions match)    │
│                                      │
│ For Policy2:                         │
│   Rule1: NOT APPLICABLE              │
│                                      │
│ For Policy3:                         │
│   Rule1: DENY (explicit deny)        │
│                                      │
│ Combining Algorithm: Deny-Overrides  │
│ Final Decision: DENY                 │
└──────┬───────────────────────────────┘
       │
       ▼
┌──────────────────────────────────────┐
│   Log Decision                       │
│                                      │
│ AuditLog.Create({                    │
│   userId: guid,                      │
│   action: "read",                    │
│   resource: "secret",                │
│   decision: "deny",                  │
│   reasons: ["Explicit deny by..."],  │
│   timestamp: now,                    │
│   ipAddress: "192.168.1.100"         │
│ })                                   │
└──────┬───────────────────────────────┘
       │
       ▼
┌──────────────────────────────────────┐
│   Return Response                    │
│                                      │
│ HTTP 403 Forbidden                   │
│ {                                    │
│   "allowed": false,                  │
│   "decision": "deny",                │
│   "reasons": [                       │
│     "Explicit deny by Policy3"       │
│   ],                                 │
│   "appliedPolicies": ["Policy3"]     │
│ }                                    │
└──────────────────────────────────────┘
```

---

## Caching Strategy

```
┌─────────────────────────────────────────────────────┐
│              Memory Cache (IMemoryCache)            │
│                                                     │
│  HCL Policy Cache:                                  │
│  ┌──────────────────────────────────────────────┐  │
│  │ Key: "hcl:policy:[hash]"                     │  │
│  │ Value: ParsedHclPolicy                       │  │
│  │ TTL: 15 minutes                              │  │
│  │ Size: ~10KB per policy                       │  │
│  │ Eviction: Absolute expiration                │  │
│  └──────────────────────────────────────────────┘  │
│                                                     │
│  Cache Hit Flow:                                    │
│  ┌──────────────────────────────────────────────┐  │
│  │ 1. Calculate hash of HCL text               │  │
│  │ 2. Check if "hcl:policy:[hash]" exists      │  │
│  │ 3. If found, return cached parsed policy    │  │
│  │ 4. If not, parse HCL and cache result       │  │
│  └──────────────────────────────────────────────┘  │
│                                                     │
│  Cache Statistics (Expected):                       │
│  • Hit Rate: 85-95%                                 │
│  • Average Latency Reduction: 15-20ms               │
│  • Memory Usage: < 100MB for 1000 policies          │
└─────────────────────────────────────────────────────┘
```

---

## Error Handling Architecture

```
┌────────────────────────────────────────────────┐
│         Error Handling Hierarchy              │
│                                                │
│  Layer 1: Controller                           │
│  ┌──────────────────────────────────────────┐ │
│  │ try {                                    │ │
│  │   var result = await _engine.Evaluate(); │ │
│  │ }                                        │ │
│  │ catch (ValidationException ex) {         │ │
│  │   return BadRequest(ex.Message);         │ │
│  │ }                                        │ │
│  │ catch (NotFoundException ex) {           │ │
│  │   return NotFound(ex.Message);           │ │
│  │ }                                        │ │
│  │ catch (Exception ex) {                   │ │
│  │   _logger.LogError(ex, "Error");         │ │
│  │   return StatusCode(500);                │ │
│  │ }                                        │ │
│  └──────────────────────────────────────────┘ │
│                                                │
│  Layer 2: Service                              │
│  ┌──────────────────────────────────────────┐ │
│  │ try {                                    │ │
│  │   // Business logic                      │ │
│  │   return new Response { Success = true };│ │
│  │ }                                        │ │
│  │ catch (DbException ex) {                 │ │
│  │   _logger.LogError(ex, "DB error");      │ │
│  │   return new Response {                  │ │
│  │     Success = false,                     │ │
│  │     Error = "Database error"             │ │
│  │   };                                     │ │
│  │ }                                        │ │
│  └──────────────────────────────────────────┘ │
│                                                │
│  Layer 3: Data Access                          │
│  ┌──────────────────────────────────────────┐ │
│  │ try {                                    │ │
│  │   return await _context.Users.Find();    │ │
│  │ }                                        │ │
│  │ catch (Exception ex) {                   │ │
│  │   _logger.LogError(ex, "Query failed");  │ │
│  │   throw;                                 │ │
│  │ }                                        │ │
│  └──────────────────────────────────────────┘ │
│                                                │
│  Default Behavior: FAIL CLOSED                 │
│  • On error → DENY access                      │
│  • Log all errors                               │
│  • Return generic error message to client       │
│  • Include correlation ID for troubleshooting   │
└────────────────────────────────────────────────┘
```

---

## Deployment Architecture

```
┌─────────────────────────────────────────────────────────┐
│                Production Environment                    │
│                                                         │
│  ┌──────────────────────────────────────────────────┐  │
│  │              Load Balancer (HTTPS)                │  │
│  └────────────┬──────────────┬──────────────────────┘  │
│               │              │                          │
│        ┌──────▼──────┐  ┌───▼──────┐                   │
│        │  USP API    │  │ USP API  │ (Multiple instances)│
│        │  Instance 1 │  │Instance 2│                   │
│        └──────┬──────┘  └───┬──────┘                   │
│               │              │                          │
│               └──────┬───────┘                          │
│                      │                                  │
│         ┌────────────┴──────────────┐                   │
│         │                           │                   │
│    ┌────▼─────┐            ┌────────▼──────┐           │
│    │PostgreSQL│            │ Redis Cache   │           │
│    │          │            │               │           │
│    │• Users   │            │• HCL Policies │           │
│    │• Roles   │            │• Parsed Rules │           │
│    │• Policies│            │               │           │
│    │• Audit   │            │TTL: 15 min    │           │
│    └──────────┘            └───────────────┘           │
│                                                         │
│  Monitoring:                                            │
│  ┌────────────┐  ┌──────────────┐  ┌────────────────┐ │
│  │Prometheus  │  │  Grafana     │  │  Elasticsearch │ │
│  │(Metrics)   │  │(Dashboards)  │  │   (Logs)       │ │
│  └────────────┘  └──────────────┘  └────────────────┘ │
└─────────────────────────────────────────────────────────┘
```

---

**Document Version:** 1.0
**Last Updated:** December 26, 2025
**Status:** Phase 1 Complete

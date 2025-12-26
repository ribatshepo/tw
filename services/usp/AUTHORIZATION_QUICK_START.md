# Authorization & Policy Engine - Quick Start Guide

## For Developers Using the USP Authorization System

---

## 1. Basic ABAC Authorization Check

```csharp
// Inject the ABAC engine
private readonly IAbacEngine _abacEngine;

// Check if user has access
var request = new AbacEvaluationRequest
{
    SubjectId = userId,
    Action = "read",
    ResourceType = "secret",
    ResourceId = "secret/data/prod/database"
};

var result = await _abacEngine.EvaluateAsync(request);

if (result.Allowed)
{
    // Grant access
}
else
{
    // Deny access - result.Reasons contains explanation
}
```

---

## 2. Column-Level Security

```csharp
// Inject the column security engine
private readonly IColumnSecurityEngine _columnSecurityEngine;

// Check column access
var request = new ColumnAccessRequest
{
    UserId = userId,
    TableName = "users",
    RequestedColumns = new List<string> { "id", "name", "email", "ssn", "salary" },
    Operation = "read"
};

var result = await _columnSecurityEngine.CheckColumnAccessAsync(request);

// result.AllowedColumns contains columns user can see
// result.DeniedColumns contains columns that should be hidden
// result.ColumnRestrictions contains columns that need masking

// Apply masking to actual data
var data = await GetUserData(userId);
var maskedData = await _columnSecurityEngine.ApplyMaskingAsync(userId, "users", data);
// maskedData now has sensitive fields masked/redacted/tokenized
```

---

## 3. Context-Aware Access Control

```csharp
// Inject the context evaluator
private readonly IContextEvaluator _contextEvaluator;

// Evaluate access with context
var request = new ContextEvaluationRequest
{
    UserId = userId,
    Action = "delete",
    ResourceType = "secret",
    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
    NetworkZone = "external", // or "internal", "vpn", "dmz"
    DeviceCompliant = true,
    GeoLocation = "US/Seattle",
    UserAgent = HttpContext.Request.Headers["User-Agent"].ToString()
};

var result = await _contextEvaluator.EvaluateContextAsync(request);

if (result.Allowed)
{
    // Check if additional action required
    switch (result.RequiredAction)
    {
        case "mfa":
            // Redirect to MFA challenge
            return Challenge();

        case "approval":
            // Create approval request
            return RequireApproval();

        case null:
            // No additional action needed
            break;
    }
}
```

---

## 4. HCL Policy Evaluation

```csharp
// Inject the HCL policy evaluator
private readonly IHclPolicyEvaluator _hclEvaluator;

// Evaluate HCL policy
var request = new HclAuthorizationRequest
{
    UserId = userId,
    Action = "read",
    Resource = "secret/data/prod/database"
};

var result = await _hclEvaluator.EvaluateAsync(request);

if (!result.Authorized)
{
    _logger.LogWarning("HCL authorization denied: {Reason}", result.DenyReason);
}
```

---

## 5. Policy Simulation (Testing)

```csharp
// Test a policy before deploying it
var request = new PolicySimulationRequest
{
    PolicyId = policyId,
    UserId = testUserId,
    Action = "delete",
    Resource = "secret",
    Context = new Dictionary<string, object>
    {
        { "ip_address", "192.168.1.100" },
        { "network_zone", "internal" }
    }
};

var result = await _abacEngine.SimulatePolicyAsync(request);

Console.WriteLine($"Decision: {result.Decision}");
Console.WriteLine($"Allowed: {result.Allowed}");
Console.WriteLine($"Explanation: {result.Explanation}");

foreach (var step in result.EvaluationSteps)
{
    Console.WriteLine($"  - {step}");
}
```

---

## 6. Batch Authorization Checks

```csharp
// Check multiple authorizations at once
var requests = new List<BatchAuthorizationRequest>
{
    new() { RequestId = "1", UserId = userId, Action = "read", ResourceType = "secret" },
    new() { RequestId = "2", UserId = userId, Action = "write", ResourceType = "secret" },
    new() { RequestId = "3", UserId = userId, Action = "delete", ResourceType = "secret" }
};

var results = await authzController.CheckBatchAuthorization(requests);

foreach (var result in results)
{
    Console.WriteLine($"Request {result.RequestId}: {result.Decision}");
}
```

---

## 7. Creating ABAC Policies

```csharp
// Create an ABAC policy
var policy = new CreatePolicyRequest
{
    Name = "Engineering Department Access",
    Description = "Allow engineers to read secrets in their department",
    PolicyType = "ABAC",
    Policy = JsonSerializer.Serialize(new
    {
        rules = new[]
        {
            new
            {
                name = "Allow engineers to read department secrets",
                effect = "allow",
                action = "read",
                resource = "secret",
                conditions = new
                {
                    department = "engineering",
                    clearance_level = "confidential"
                }
            }
        }
    }),
    IsActive = true
};

var created = await policyController.CreatePolicy(policy);
```

---

## 8. Creating HCL Policies

```csharp
// Create an HCL policy
var policy = new CreatePolicyRequest
{
    Name = "Department-Based Secret Access",
    Description = "Users can access secrets in their department",
    PolicyType = "HCL",
    Policy = @"
# Template-based path using user's department
path ""secret/data/${user.department}/*"" {
  capabilities = [""read"", ""list""]
}

# Production secrets require approval
path ""secret/data/prod/*"" {
  capabilities = [""read""]
  required_parameters = [""approval_id""]
}
",
    IsActive = true
};

var created = await policyController.CreatePolicy(policy);
```

---

## 9. Creating Column Security Rules

```csharp
// Mask sensitive columns
await _columnSecurityEngine.CreateColumnRuleAsync(new CreateColumnRuleRequest
{
    TableName = "users",
    ColumnName = "email",
    Operation = "read",
    RestrictionType = "mask",
    MaskingPattern = "***",
    AllowedRoles = new List<string> { "DataAnalyst", "Support" },
    Priority = 100
});

// Deny access to salary for non-managers
await _columnSecurityEngine.CreateColumnRuleAsync(new CreateColumnRuleRequest
{
    TableName = "users",
    ColumnName = "salary",
    Operation = "read",
    RestrictionType = "deny",
    DeniedRoles = new List<string> { "DataAnalyst", "Developer", "User" },
    Priority = 200
});

// Redact SSN for all except HR
await _columnSecurityEngine.CreateColumnRuleAsync(new CreateColumnRuleRequest
{
    TableName = "users",
    ColumnName = "ssn",
    Operation = "read",
    RestrictionType = "redact",
    AllowedRoles = new List<string> { "HR", "SecurityAdmin" },
    Priority = 150
});
```

---

## 10. Creating Context Policies

```csharp
// Business hours only policy
await _contextEvaluator.CreateContextPolicyAsync(new CreateContextPolicyRequest
{
    ResourceType = "secret",
    EnableTimeRestriction = true,
    AllowedDaysOfWeek = "Monday,Tuesday,Wednesday,Thursday,Friday",
    AllowedStartTime = new TimeSpan(9, 0, 0),
    AllowedEndTime = new TimeSpan(17, 0, 0)
});

// Geographic restriction
await _contextEvaluator.CreateContextPolicyAsync(new CreateContextPolicyRequest
{
    ResourceType = "pii",
    EnableLocationRestriction = true,
    AllowedCountries = new List<string> { "US", "CA" },
    DeniedCountries = new List<string> { "XX", "Unknown" },
    AllowedNetworkZones = new List<string> { "internal", "vpn" }
});

// Device compliance requirement
await _contextEvaluator.CreateContextPolicyAsync(new CreateContextPolicyRequest
{
    ResourceType = "financial",
    EnableDeviceRestriction = true,
    RequireCompliantDevice = true,
    AllowedDeviceTypes = new List<string> { "Windows", "Mac", "Linux" }
});

// Risk-based access with adaptive MFA
await _contextEvaluator.CreateContextPolicyAsync(new CreateContextPolicyRequest
{
    ResourceType = "*",
    EnableRiskRestriction = true,
    MaxAllowedRiskScore = 85,
    DenyImpossibleTravel = true,
    RequireMfaOnHighRisk = true,
    HighRiskThreshold = 70
});
```

---

## 11. Attribute Types Reference

### Subject Attributes (20+)
```
Basic Identity:
- user_id, username, email, status, is_active

Security:
- mfa_enabled, email_confirmed, phone_confirmed
- lockout_enabled, is_locked_out

Roles:
- roles (list), role_count, primary_role

Organizational:
- department, clearance_level, job_function
- location, employment_type

Risk:
- risk_score, is_high_risk, is_low_risk

Temporal:
- created_at, last_login, account_age_days
```

### Environment Attributes (15+)
```
Time:
- current_time, day_of_week, hour_of_day
- is_business_hours, is_weekend, time_of_day

Network:
- ip_address, network_zone, is_internal_network

Device:
- user_agent, device_compliance_status, is_compliant_device

Location:
- geo_location, geo_country, is_restricted_location

System:
- server_timezone, environment, is_production
```

### Resource Attributes (for Secrets)
```
- resource_id, path, version, created_by
- classification, sensitivity_level
- owner, department, workspace, tags
- lifecycle_stage, age_days
```

---

## 12. Common Patterns

### Pattern 1: Multi-Factor Authentication
```csharp
// Check if user meets baseline requirements
var baselineCheck = await _abacEngine.HasAccessAsync(userId, action, resourceType);

if (!baselineCheck)
{
    return Forbid();
}

// Check context for additional requirements
var contextCheck = await _contextEvaluator.EvaluateContextAsync(new ContextEvaluationRequest
{
    UserId = userId,
    Action = action,
    ResourceType = resourceType,
    IpAddress = ipAddress,
    NetworkZone = networkZone
});

if (contextCheck.RequiredAction == "mfa")
{
    // Challenge for MFA
    return Challenge("MFA");
}
```

### Pattern 2: Sensitive Data Access
```csharp
// 1. Check authorization
var authCheck = await _abacEngine.HasAccessAsync(userId, "read", "users");
if (!authCheck) return Forbid();

// 2. Get allowed columns
var columnCheck = await _columnSecurityEngine.CheckColumnAccessAsync(new ColumnAccessRequest
{
    UserId = userId,
    TableName = "users",
    RequestedColumns = new List<string> { "id", "name", "email", "ssn", "salary" },
    Operation = "read"
});

// 3. Query only allowed columns
var query = BuildQuery("users", columnCheck.AllowedColumns);
var data = await ExecuteQuery(query);

// 4. Apply masking
var maskedData = await _columnSecurityEngine.ApplyMaskingAsync(userId, "users", data);

return Ok(maskedData);
```

### Pattern 3: High-Risk Operation
```csharp
// Calculate risk score
var riskScore = await _contextEvaluator.CalculateAccessRiskScoreAsync(new ContextEvaluationRequest
{
    UserId = userId,
    Action = "delete",
    ResourceType = "production_data",
    ImpossibleTravel = DetectImpossibleTravel(userId, ipAddress),
    DeviceCompliant = await CheckDeviceCompliance(deviceId),
    NetworkZone = GetNetworkZone(ipAddress)
});

if (riskScore > 70)
{
    // Require manager approval for high-risk operations
    await CreateApprovalRequest(userId, action, resourceType);
    return Accepted("Approval required");
}
```

---

## 13. Error Handling

```csharp
try
{
    var result = await _abacEngine.EvaluateAsync(request);

    if (!result.Allowed)
    {
        _logger.LogWarning(
            "Access denied for user {UserId} on {Resource}: {Reasons}",
            userId,
            resourceType,
            string.Join(", ", result.Reasons)
        );

        return Forbid();
    }
}
catch (Exception ex)
{
    _logger.LogError(ex, "Error evaluating authorization");

    // Fail closed - deny on error
    return StatusCode(500, "Authorization evaluation error");
}
```

---

## 14. Logging Best Practices

```csharp
// Log authorization decisions
_logger.LogInformation(
    "Authorization check: User={UserId}, Action={Action}, Resource={ResourceType}, Decision={Decision}, Time={ElapsedMs}ms",
    userId,
    action,
    resourceType,
    result.Decision,
    result.EvaluationTime.TotalMilliseconds
);

// Log high-risk access
if (contextResult.RiskScore > 70)
{
    _logger.LogWarning(
        "High-risk access attempt: User={UserId}, RiskScore={RiskScore}, Reasons={Reasons}",
        userId,
        contextResult.RiskScore,
        string.Join(", ", contextResult.Reasons)
    );
}

// Log denied access
if (!result.Allowed)
{
    _logger.LogWarning(
        "Access denied: User={UserId}, Action={Action}, Resource={ResourceType}, Reasons={Reasons}",
        userId,
        action,
        resourceType,
        string.Join(", ", result.Reasons)
    );
}
```

---

## 15. Performance Tips

1. **Cache policy evaluations** for frequently accessed resources
2. **Use batch operations** when checking multiple authorizations
3. **Extract attributes once** and reuse for multiple checks
4. **Implement early exits** in policy evaluation
5. **Use AsNoTracking** for read-only queries

```csharp
// Good: Extract once, use multiple times
var attributes = await _abacEngine.ExtractAttributesAsync(new AttributeExtractionRequest
{
    UserId = userId,
    ResourceType = resourceType,
    AdditionalContext = context
});

var request1 = new AbacEvaluationRequest
{
    SubjectId = userId,
    Action = "read",
    ResourceType = resourceType,
    SubjectAttributes = attributes.SubjectAttributes,
    ResourceAttributes = attributes.ResourceAttributes,
    EnvironmentAttributes = attributes.EnvironmentAttributes
};

var request2 = new AbacEvaluationRequest
{
    SubjectId = userId,
    Action = "write",
    ResourceType = resourceType,
    SubjectAttributes = attributes.SubjectAttributes,
    ResourceAttributes = attributes.ResourceAttributes,
    EnvironmentAttributes = attributes.EnvironmentAttributes
};
```

---

## 16. Testing Your Policies

```csharp
[Fact]
public async Task MyCustomPolicy_ShouldAllowEngineers()
{
    // Arrange
    var policy = new CreatePolicyRequest
    {
        Name = "Test Policy",
        PolicyType = "ABAC",
        Policy = JsonSerializer.Serialize(new
        {
            rules = new[]
            {
                new
                {
                    name = "Allow engineers",
                    effect = "allow",
                    action = "read",
                    resource = "secret",
                    conditions = new { department = "engineering" }
                }
            }
        }),
        IsActive = true
    };

    await policyService.CreatePolicyAsync(policy);

    // Act
    var result = await _abacEngine.EvaluateAsync(new AbacEvaluationRequest
    {
        SubjectId = engineerUserId,
        Action = "read",
        ResourceType = "secret"
    });

    // Assert
    result.Allowed.Should().BeTrue();
}
```

---

## 17. Migration Checklist

When upgrading to the new authorization system:

- [ ] Audit existing authorization code
- [ ] Identify hardcoded permission checks
- [ ] Create ABAC/HCL policies for existing rules
- [ ] Test policies in staging
- [ ] Run policy simulation for all users
- [ ] Deploy policies to production
- [ ] Monitor authorization decisions
- [ ] Update documentation

---

## 18. Getting Help

**Documentation:**
- Full spec: `/docs/specs/security.md`
- Implementation summary: `/services/usp/AUTHORIZATION_PHASE1_SUMMARY.md`

**Code Examples:**
- Unit tests: `/tests/USP.UnitTests/Services/Authorization/`
- Controller: `/src/USP.Api/Controllers/Authorization/AuthorizationController.cs`

**Common Issues:**
1. **Policy not applying** - Check `IsActive` flag
2. **Unexpected denials** - Review `result.Reasons`
3. **Performance issues** - Enable policy caching
4. **Template not working** - Verify variable syntax `${var}`

---

**Happy Coding!** 🚀

For questions or issues, contact the Security Platform Team.

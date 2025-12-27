# USP Authorization Middleware

This directory contains the authorization middleware components that enable declarative permission-based authorization on ASP.NET Core controllers.

## Overview

The authorization middleware provides:
- Custom `[RequirePermission]` attribute for controllers and actions
- Integration with the `IAuthorizationService` for RBAC, ABAC, and HCL policy evaluation
- Automatic authorization context building from HTTP requests
- ASP.NET Core policy-based authorization integration

---

## Components

### 1. `RequirePermissionAttribute`

Custom authorization attribute that can be applied to controllers or individual actions.

**Usage:**

```csharp
// On a single action
[RequirePermission("secrets:write")]
public IActionResult CreateSecret([FromBody] CreateSecretRequest request)
{
    // Only users with "secrets:write" permission can access this
    return Ok();
}

// With separate resource and action
[RequirePermission("users", "delete")]
public IActionResult DeleteUser(string id)
{
    // Only users with "users:delete" permission can access this
    return Ok();
}

// On entire controller
[RequirePermission("admin:manage")]
public class AdminController : ControllerBase
{
    // All actions require "admin:manage" permission
}

// Multiple permissions (user must have ALL)
[RequirePermission("secrets:read")]
[RequirePermission("secrets:decrypt")]
public IActionResult DecryptSecret(string id)
{
    // Requires both "secrets:read" AND "secrets:decrypt"
    return Ok();
}
```

### 2. `PermissionAuthorizationRequirement`

Represents an authorization requirement for a specific permission.

**Structure:**
- `Resource` - The resource being accessed (e.g., "secrets", "users")
- `Action` - The action being performed (e.g., "read", "write", "delete")
- `Permission` - Full permission string in format "resource:action"

### 3. `PermissionAuthorizationHandler`

Authorization handler that evaluates permission requirements using the `IAuthorizationService`.

**Features:**
- Extracts user ID from JWT claims
- Builds authorization context from HTTP request (IP, user agent, path, method)
- Calls `IAuthorizationService.CheckAuthorizationAsync()`
- Logs all authorization decisions
- Fail-secure design (denies on error)

### 4. `PermissionAuthorizationPolicyProvider`

Custom policy provider that dynamically creates policies for permissions.

**Why it's needed:**
Without this, you would need to register every permission as a policy in `Program.cs`:

```csharp
// Without policy provider (NOT recommended)
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("secrets:write", policy =>
        policy.AddRequirements(new PermissionAuthorizationRequirement("secrets", "write")));
    options.AddPolicy("secrets:read", policy =>
        policy.AddRequirements(new PermissionAuthorizationRequirement("secrets", "read")));
    // ... hundreds more
});
```

With the policy provider, policies are created automatically:

```csharp
// With policy provider (automatically handles all permissions)
builder.Services.AddPermissionBasedAuthorization();
```

---

## Setup

### 1. Register in Program.cs

The middleware is already registered in `Program.cs`:

```csharp
// Authorization Services
builder.Services.AddScoped<IAuthorizationService, AuthorizationService>();

// Permission-based Authorization (custom policy provider and handlers)
builder.Services.AddPermissionBasedAuthorization();
```

### 2. Ensure Authentication is Configured

The authorization middleware requires users to be authenticated:

```csharp
app.UseAuthentication();  // MUST come before UseAuthorization()
app.UseAuthorization();
```

---

## Examples

### Example 1: Simple Permission Check

```csharp
[ApiController]
[Route("api/v1/secrets")]
[Authorize] // Require authentication
public class SecretsController : ControllerBase
{
    [HttpPost]
    [RequirePermission("secrets:write")]
    public async Task<IActionResult> CreateSecret([FromBody] CreateSecretRequest request)
    {
        // Only accessible if user has "secrets:write" permission
        // Permission is checked via RBAC, ABAC, or HCL policies
        return Ok();
    }

    [HttpGet("{path}")]
    [RequirePermission("secrets:read")]
    public async Task<IActionResult> GetSecret(string path)
    {
        // Only accessible if user has "secrets:read" permission
        return Ok();
    }

    [HttpDelete("{path}")]
    [RequirePermission("secrets:delete")]
    public async Task<IActionResult> DeleteSecret(string path)
    {
        // Only accessible if user has "secrets:delete" permission
        return Ok();
    }
}
```

### Example 2: Controller-Level Permission

```csharp
[ApiController]
[Route("api/v1/admin")]
[Authorize]
[RequirePermission("admin:manage")] // All actions require this permission
public class AdminController : ControllerBase
{
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        // Requires "admin:manage" permission (from controller)
        return Ok();
    }

    [HttpPost("roles")]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
    {
        // Requires "admin:manage" permission (from controller)
        return Ok();
    }
}
```

### Example 3: Multiple Permissions (AND logic)

```csharp
[HttpPost("rotate")]
[RequirePermission("secrets:read")]   // User must have BOTH
[RequirePermission("secrets:rotate")] // permissions to access
public async Task<IActionResult> RotateSecret(string id)
{
    // Both permissions are checked
    return Ok();
}
```

### Example 4: Override Controller Permission

```csharp
[ApiController]
[Route("api/v1/data")]
[RequirePermission("data:read")] // Controller requires read
public class DataController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetData()
    {
        // Requires "data:read" (from controller)
        return Ok();
    }

    [HttpPost]
    [RequirePermission("data:write")] // Action requires write
    public async Task<IActionResult> CreateData([FromBody] object data)
    {
        // Requires BOTH "data:read" (controller) AND "data:write" (action)
        return Ok();
    }

    [HttpDelete("{id}")]
    [AllowAnonymous] // Override controller requirement
    public async Task<IActionResult> DeleteData(string id)
    {
        // No permission required (overridden with AllowAnonymous)
        return Ok();
    }
}
```

---

## How It Works

### 1. Request Flow

```
1. User makes HTTP request
   ↓
2. Authentication middleware validates JWT token
   ↓
3. Authorization middleware intercepts request
   ↓
4. [RequirePermission] attribute detected
   ↓
5. PermissionAuthorizationPolicyProvider creates policy dynamically
   ↓
6. PermissionAuthorizationHandler is invoked
   ↓
7. Handler extracts user ID from claims
   ↓
8. Handler builds AuthorizationContext from HTTP request
   ↓
9. Handler calls IAuthorizationService.CheckAuthorizationAsync()
   ↓
10. AuthorizationService evaluates policies (RBAC/ABAC/HCL)
   ↓
11. Authorization result returned
   ↓
12. If authorized: Request continues to action
    If denied: 403 Forbidden returned
```

### 2. Authorization Context

The middleware automatically builds an `AuthorizationContext` from the HTTP request:

```csharp
{
  "IpAddress": "10.0.1.50",
  "UserAgent": "Mozilla/5.0...",
  "Timestamp": "2025-12-27T10:30:00Z",
  "Attributes": {
    "method": "POST",
    "path": "/api/v1/secrets/production/db",
    "protocol": "HTTP/1.1"
  }
}
```

This context is used for:
- **ABAC policies**: Evaluating IP range conditions, time-based conditions
- **Audit logging**: Recording who accessed what from where
- **Risk-based auth**: Detecting unusual patterns

### 3. Policy Evaluation

The authorization service evaluates policies in priority order:

1. **RBAC**: Checks if user has required permission via roles
2. **ABAC**: Matches user/resource attributes and evaluates conditions
3. **HCL**: Matches path patterns and capabilities (Vault-compatible)

First matching policy determines the result (allow/deny).

---

## Permission Format

Permissions follow the format: `resource:action`

**Resource**: The entity being accessed (noun)
- Examples: `secrets`, `users`, `roles`, `policies`, `audit`, `pam`

**Action**: The operation being performed (verb)
- Examples: `create`, `read`, `update`, `delete`, `list`, `manage`, `rotate`, `export`

**Wildcards:**
- `resource:*` - All actions on resource (e.g., `secrets:*`)
- `*:*` - All actions on all resources (superadmin)

---

## Error Responses

### 401 Unauthorized
User is not authenticated (no valid JWT token)

```json
{
  "type": "https://tools.ietf.org/html/rfc7235#section-3.1",
  "title": "Unauthorized",
  "status": 401
}
```

### 403 Forbidden
User is authenticated but lacks required permission

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.3",
  "title": "Forbidden",
  "status": 403
}
```

**Note:** The middleware does NOT expose the specific permission that failed (for security reasons). Administrators can check logs to see why authorization failed.

---

## Logging

The authorization handler logs all authorization decisions:

**Successful Authorization:**
```
[Information] Authorization succeeded for user abc123, permission: secrets:write, reason: Matched RBAC policy 'SecretsAdmin' with effect 'allow'
```

**Failed Authorization:**
```
[Warning] Authorization failed for user abc123, permission: secrets:write, reason: No matching policy found - default deny
```

**Error During Authorization:**
```
[Error] Error evaluating permission requirement secrets:write
```

---

## Testing Authorization

### 1. Create Test User and Roles

```bash
# Create role with permissions
POST /api/v1/roles
{
  "name": "SecretsManager",
  "description": "Can manage secrets"
}

# Assign permissions to role
POST /api/v1/roles/{roleId}/permissions
{
  "permissionIds": ["secrets:read", "secrets:write", "secrets:delete"]
}

# Assign role to user
# (via Identity API or database)
```

### 2. Test Permission Check

```bash
# Login to get JWT token
POST /api/v1/auth/login
{
  "emailOrUsername": "testuser@example.com",
  "password": "password"
}

# Use token to access protected endpoint
GET /api/v1/secrets/my-secret
Authorization: Bearer <token>

# Response:
# - 200 OK if user has "secrets:read" permission
# - 403 Forbidden if user lacks permission
```

### 3. Check User Permissions

```bash
# See what permissions the current user has
GET /api/v1/authz/permissions
Authorization: Bearer <token>

# Response:
{
  "userId": "abc123",
  "permissions": [
    "secrets:read",
    "secrets:write",
    "secrets:delete"
  ]
}
```

---

## Best Practices

### 1. Use Specific Permissions
```csharp
// Good - specific permission
[RequirePermission("secrets:write")]

// Bad - too broad
[RequirePermission("secrets:*")]
```

### 2. Apply Permissions at Appropriate Level
```csharp
// Controller-level for common permission
[RequirePermission("secrets:read")]
public class SecretsController
{
    // Action-level for elevated permission
    [RequirePermission("secrets:delete")]
    public IActionResult DeleteSecret() { }
}
```

### 3. Document Required Permissions
```csharp
/// <summary>
/// Creates a new secret in the vault.
/// Requires permission: secrets:write
/// </summary>
[RequirePermission("secrets:write")]
public IActionResult CreateSecret() { }
```

### 4. Use Consistent Naming
- Resources: plural nouns (`secrets`, `users`, `roles`)
- Actions: verbs (`create`, `read`, `update`, `delete`, `list`)

### 5. Create Hierarchical Permissions
```
secrets:*              (all secret operations)
├── secrets:read       (read secrets)
├── secrets:write      (create/update secrets)
├── secrets:delete     (delete secrets)
└── secrets:rotate     (rotate secret values)
```

---

## Troubleshooting

### Issue: 403 Forbidden but user should have access

**Check:**
1. User's roles: `GET /api/v1/authz/permissions`
2. Role's permissions: `GET /api/v1/roles/{roleId}`
3. Active policies: `GET /api/v1/authz/policies`
4. Logs for authorization failure reason

### Issue: [RequirePermission] not working

**Check:**
1. `AddPermissionBasedAuthorization()` is called in Program.cs
2. `UseAuthentication()` comes before `UseAuthorization()`
3. User is authenticated (has valid JWT)
4. Permission format is correct ("resource:action")

### Issue: All requests return 401 Unauthorized

**Check:**
1. JWT token is included in Authorization header
2. Token is not expired
3. Token signature is valid
4. `UseAuthentication()` middleware is added

---

## Performance Considerations

- **Policy Evaluation**: Policies are evaluated in priority order; first match wins
- **Caching**: Consider caching user permissions for the duration of the request
- **Database Queries**: The handler makes database queries; ensure proper indexing
- **Logging**: Authorization logging is Info/Warning level; production should use Warning+

---

## Security Considerations

1. **Fail-Secure**: Authorization errors result in denial (403 Forbidden)
2. **No Permission Leakage**: 403 responses don't expose which permission failed
3. **Logging**: All authorization decisions are logged for audit
4. **Stateless**: No authorization state stored in memory (all checks query database)

---

**Last Updated**: 2025-12-27
**Version**: 1.0

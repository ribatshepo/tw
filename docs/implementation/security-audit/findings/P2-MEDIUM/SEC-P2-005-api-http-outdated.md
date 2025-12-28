# SEC-P2-005: API.http Outdated

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P2-005 |
| **Title** | USP.API.http Contains Only Placeholder Weatherforecast Endpoint |
| **Priority** | P2 - MEDIUM |
| **Severity** | Low |
| **Category** | Documentation |
| **Status** | Not Started |
| **Effort Estimate** | 3 hours |
| **Implementation Phase** | Phase 3 (Week 3, Day 5) |
| **Assigned To** | Backend Engineer |
| **Created** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:501-505` |
| **Code Files** | `/home/tshepo/projects/tw/services/usp/USP.API.http` |
| **Dependencies** | None |
| **Compliance Impact** | None (Developer productivity) |

---

## 3. Executive Summary

### Problem

USP.API.http file contains only placeholder weatherforecast endpoint. Missing all actual USP endpoints (Auth, Secrets, Seal/Unseal, MFA, etc.).

### Impact

- **Manual Testing Difficult:** Developers must craft HTTP requests manually
- **Outdated Examples:** Placeholder endpoint doesn't reflect actual API
- **Poor Developer Experience:** No quick way to test endpoints

### Solution

Update USP.API.http with all actual USP endpoints organized by feature (Auth, Secrets, Vault, MFA, Authorization).

---

## 4. Implementation Guide

### Step 1: Update USP.API.http (2.5 hours)

```http
### USP API - HTTP Client Requests
### Visual Studio Code: Install "REST Client" extension
### JetBrains Rider: Built-in support

@baseUrl = https://localhost:5001
@token = eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...

### ============================================
### Health & Status
### ============================================

### Health Check
GET {{baseUrl}}/health

### Vault Status
GET {{baseUrl}}/api/v1/vault/seal/status

### ============================================
### Authentication
### ============================================

### Register New User
POST {{baseUrl}}/api/v1/auth/register
Content-Type: application/json

{
  "username": "testuser",
  "email": "testuser@example.com",
  "password": "SecurePassword123!",
  "firstName": "Test",
  "lastName": "User"
}

### Login
POST {{baseUrl}}/api/v1/auth/login
Content-Type: application/json

{
  "username": "testuser",
  "password": "SecurePassword123!"
}

### Refresh Token
POST {{baseUrl}}/api/v1/auth/refresh
Content-Type: application/json
Authorization: Bearer {{token}}

{
  "refreshToken": "{{refreshToken}}"
}

### Logout
POST {{baseUrl}}/api/v1/auth/logout
Authorization: Bearer {{token}}

### ============================================
### Multi-Factor Authentication
### ============================================

### Enable TOTP MFA
POST {{baseUrl}}/api/v1/auth/mfa/totp/enable
Authorization: Bearer {{token}}

### Verify TOTP Code
POST {{baseUrl}}/api/v1/auth/mfa/totp/verify
Content-Type: application/json
Authorization: Bearer {{token}}

{
  "code": "123456"
}

### Disable MFA
POST {{baseUrl}}/api/v1/auth/mfa/disable
Authorization: Bearer {{token}}

### ============================================
### Vault Seal/Unseal
### ============================================

### Unseal Vault (Key 1/3)
POST {{baseUrl}}/api/v1/vault/seal/unseal
Content-Type: application/json

{
  "key": "AQuZNts8MpNzQ6ExbvCyvHnH+bhSXnlcQWfx5g+TvipP"
}

### Unseal Vault (Key 2/3)
POST {{baseUrl}}/api/v1/vault/seal/unseal
Content-Type: application/json

{
  "key": "AtejJPkGqETjjRC7JdqtPo/YFVZIlUpO4HK8LVgRxrMW"
}

### Unseal Vault (Key 3/3)
POST {{baseUrl}}/api/v1/vault/seal/unseal
Content-Type: application/json

{
  "key": "A1pC5vMmNUFaXLIrNuQXp0wDNiv7uCPDR2G0KK2rYxey"
}

### Seal Vault
POST {{baseUrl}}/api/v1/vault/seal
Content-Type: application/json
X-Vault-Token: {{vaultToken}}

### ============================================
### Secrets Management
### ============================================

### List Secrets
GET {{baseUrl}}/api/v1/secrets
Authorization: Bearer {{token}}

### Create Secret
POST {{baseUrl}}/api/v1/secrets
Content-Type: application/json
Authorization: Bearer {{token}}

{
  "path": "/app/database/password",
  "data": {
    "username": "dbuser",
    "password": "SecureDbPassword123!",
    "host": "postgres.example.com",
    "port": "5432"
  },
  "metadata": {
    "environment": "production",
    "owner": "platform-team"
  }
}

### Read Secret
GET {{baseUrl}}/api/v1/secrets/{{secretId}}
Authorization: Bearer {{token}}

### Update Secret
PUT {{baseUrl}}/api/v1/secrets/{{secretId}}
Content-Type: application/json
Authorization: Bearer {{token}}

{
  "data": {
    "username": "dbuser",
    "password": "NewSecureDbPassword456!"
  }
}

### Delete Secret
DELETE {{baseUrl}}/api/v1/secrets/{{secretId}}
Authorization: Bearer {{token}}

### Get Secret Versions
GET {{baseUrl}}/api/v1/secrets/{{secretId}}/versions
Authorization: Bearer {{token}}

### Restore Secret Version
POST {{baseUrl}}/api/v1/secrets/{{secretId}}/versions/{{version}}/restore
Authorization: Bearer {{token}}

### ============================================
### Authorization & Permissions
### ============================================

### List User Permissions
GET {{baseUrl}}/api/v1/authorization/permissions
Authorization: Bearer {{token}}

### Check Permission
POST {{baseUrl}}/api/v1/authorization/check
Content-Type: application/json
Authorization: Bearer {{token}}

{
  "resource": "secrets",
  "action": "read"
}

### Grant Permission
POST {{baseUrl}}/api/v1/authorization/grant
Content-Type: application/json
Authorization: Bearer {{token}}

{
  "userId": "{{userId}}",
  "resource": "secrets",
  "action": "write"
}

### Revoke Permission
POST {{baseUrl}}/api/v1/authorization/revoke
Content-Type: application/json
Authorization: Bearer {{token}}

{
  "userId": "{{userId}}",
  "resource": "secrets",
  "action": "write"
}

### ============================================
### Audit Logs
### ============================================

### Query Audit Logs
GET {{baseUrl}}/api/v1/audit/logs?startDate=2024-01-01&endDate=2024-12-31
Authorization: Bearer {{token}}

### Export Audit Logs
GET {{baseUrl}}/api/v1/audit/export?format=json
Authorization: Bearer {{token}}

### ============================================
### User Management
### ============================================

### List Users
GET {{baseUrl}}/api/v1/users
Authorization: Bearer {{token}}

### Get User Profile
GET {{baseUrl}}/api/v1/users/{{userId}}
Authorization: Bearer {{token}}

### Update User Profile
PUT {{baseUrl}}/api/v1/users/{{userId}}
Content-Type: application/json
Authorization: Bearer {{token}}

{
  "firstName": "Updated",
  "lastName": "Name",
  "email": "updated@example.com"
}

### Deactivate User
POST {{baseUrl}}/api/v1/users/{{userId}}/deactivate
Authorization: Bearer {{token}}

### ============================================
### Metrics
### ============================================

### Prometheus Metrics
GET {{baseUrl}}/metrics
```

### Step 2: Test All Endpoints (30 minutes)

```bash
# Install REST Client extension in VS Code
# Or use built-in HTTP client in Rider

# Execute each request sequentially
# Verify responses match expected status codes
```

---

## 5. Testing

- [ ] All USP endpoints documented in .http file
- [ ] Requests organized by feature
- [ ] Variables used for baseUrl and token
- [ ] All requests tested and working
- [ ] Examples include realistic data

---

## 6. Compliance Evidence

None (Developer productivity improvement)

---

## 7. Sign-Off

- [ ] **Backend Engineer:** All endpoints documented
- [ ] **QA:** Endpoints verified

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P2-005**

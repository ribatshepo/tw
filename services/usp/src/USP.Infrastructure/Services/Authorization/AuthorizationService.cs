using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Domain.Entities.Identity;
using USP.Core.Domain.Entities.Security;
using USP.Core.Domain.Enums;
using USP.Core.Interfaces.Services.Authorization;
using USP.Infrastructure.Persistence;

namespace USP.Infrastructure.Services.Authorization;

/// <summary>
/// Implements authorization operations including RBAC, ABAC, and HCL policy evaluation.
/// </summary>
public class AuthorizationService : IAuthorizationService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ILogger<AuthorizationService> _logger;

    public AuthorizationService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ILogger<AuthorizationService> logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task<AuthorizationResult> CheckAuthorizationAsync(
        string userId,
        string resource,
        string action,
        AuthorizationContext? context = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Checking authorization for user {UserId}, resource: {Resource}, action: {Action}",
            userId, resource, action);

        try
        {
            // Get all active policies ordered by priority (descending)
            var policies = await _context.Policies
                .Where(p => p.IsActive && p.DeletedAt == null)
                .OrderByDescending(p => p.Priority)
                .ToListAsync(cancellationToken);

            // Evaluate policies in order of priority
            foreach (var policy in policies)
            {
                bool matches = false;
                PolicyType? matchedPolicyType = null;

                switch (policy.Type)
                {
                    case PolicyType.RBAC:
                        matches = await EvaluateRBACAsync(userId, resource, action, cancellationToken);
                        matchedPolicyType = PolicyType.RBAC;
                        break;

                    case PolicyType.ABAC:
                        if (context != null)
                        {
                            var abacResult = await EvaluateABACAsync(userId, resource, action, context, cancellationToken);
                            matches = abacResult.IsAuthorized;
                            matchedPolicyType = PolicyType.ABAC;
                        }
                        break;

                    case PolicyType.HCL:
                        matches = await EvaluateHCLAsync(userId, resource, action, cancellationToken);
                        matchedPolicyType = PolicyType.HCL;
                        break;
                }

                // If policy matches, return the decision based on effect
                if (matches)
                {
                    bool isAuthorized = policy.Effect.Equals("allow", StringComparison.OrdinalIgnoreCase);

                    _logger.LogInformation(
                        "Policy {PolicyId} ({PolicyName}) matched. Effect: {Effect}, Authorized: {IsAuthorized}",
                        policy.Id, policy.Name, policy.Effect, isAuthorized);

                    return new AuthorizationResult
                    {
                        IsAuthorized = isAuthorized,
                        Resource = resource,
                        Action = action,
                        Reason = $"Matched policy '{policy.Name}' with effect '{policy.Effect}'",
                        PolicyId = policy.Id,
                        PolicyType = matchedPolicyType
                    };
                }
            }

            // No policy matched - default deny
            _logger.LogWarning(
                "No policy matched for user {UserId}, resource: {Resource}, action: {Action}. Default deny.",
                userId, resource, action);

            return new AuthorizationResult
            {
                IsAuthorized = false,
                Resource = resource,
                Action = action,
                Reason = "No matching policy found - default deny"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error checking authorization for user {UserId}, resource: {Resource}, action: {Action}",
                userId, resource, action);

            // Fail secure - deny on error
            return new AuthorizationResult
            {
                IsAuthorized = false,
                Resource = resource,
                Action = action,
                Reason = $"Authorization check failed: {ex.Message}"
            };
        }
    }

    public async Task<IEnumerable<AuthorizationResult>> CheckBatchAuthorizationAsync(
        string userId,
        IEnumerable<ResourceActionPair> requests,
        AuthorizationContext? context = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<AuthorizationResult>();

        foreach (var request in requests)
        {
            var result = await CheckAuthorizationAsync(
                userId,
                request.Resource,
                request.Action,
                context,
                cancellationToken);

            results.Add(result);
        }

        return results;
    }

    public async Task<bool> HasPermissionAsync(
        string userId,
        string permission,
        CancellationToken cancellationToken = default)
    {
        // Parse permission (format: "resource:action")
        var parts = permission.Split(':', 2);
        if (parts.Length != 2)
        {
            _logger.LogWarning("Invalid permission format: {Permission}. Expected 'resource:action'", permission);
            return false;
        }

        var resource = parts[0];
        var action = parts[1];

        return await EvaluateRBACAsync(userId, resource, action, cancellationToken);
    }

    public async Task<IEnumerable<string>> GetUserPermissionsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found", userId);
            return Enumerable.Empty<string>();
        }

        var roleNames = await _userManager.GetRolesAsync(user);
        var permissions = new HashSet<string>();

        foreach (var roleName in roleNames)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role != null)
            {
                // Load permissions for this role
                await _context.Entry(role)
                    .Collection(r => r.Permissions)
                    .LoadAsync(cancellationToken);

                foreach (var permission in role.Permissions)
                {
                    if (permission.DeletedAt == null)
                    {
                        permissions.Add(permission.FullPermission);
                    }
                }
            }
        }

        return permissions;
    }

    public async Task<PolicyEvaluationResult> SimulatePolicyEvaluationAsync(
        string userId,
        string resource,
        string action,
        AuthorizationContext? context = null,
        CancellationToken cancellationToken = default)
    {
        var result = new PolicyEvaluationResult
        {
            IsAuthorized = false
        };

        // Get user roles
        var user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            var roleNames = await _userManager.GetRolesAsync(user);
            result.UserRoles = roleNames.ToList();

            // Get user permissions
            var permissions = await GetUserPermissionsAsync(userId, cancellationToken);
            result.UserPermissions = permissions.ToList();
        }

        // Get all active policies
        var policies = await _context.Policies
            .Where(p => p.IsActive && p.DeletedAt == null)
            .OrderByDescending(p => p.Priority)
            .ToListAsync(cancellationToken);

        // Evaluate each policy
        foreach (var policy in policies)
        {
            bool matches = false;
            string matchReason = "";

            switch (policy.Type)
            {
                case PolicyType.RBAC:
                    matches = await EvaluateRBACAsync(userId, resource, action, cancellationToken);
                    matchReason = matches ? "User has required role/permission" : "User lacks required role/permission";
                    break;

                case PolicyType.ABAC:
                    if (context != null)
                    {
                        var abacResult = await EvaluateABACAsync(userId, resource, action, context, cancellationToken);
                        matches = abacResult.IsAuthorized;
                        matchReason = abacResult.Reason ?? "";
                    }
                    break;

                case PolicyType.HCL:
                    matches = await EvaluateHCLAsync(userId, resource, action, cancellationToken);
                    matchReason = matches ? "HCL policy matched" : "HCL policy did not match";
                    break;
            }

            result.EvaluatedPolicies.Add(new EvaluatedPolicy
            {
                PolicyId = policy.Id,
                PolicyName = policy.Name,
                PolicyType = policy.Type,
                Matched = matches,
                Effect = policy.Effect,
                Priority = policy.Priority,
                MatchReason = matchReason
            });

            // First matching policy determines the result
            if (matches && !result.IsAuthorized)
            {
                result.IsAuthorized = policy.Effect.Equals("allow", StringComparison.OrdinalIgnoreCase);
                result.Explanation = $"Policy '{policy.Name}' (priority {policy.Priority}) matched with effect '{policy.Effect}'";
            }
        }

        if (!result.IsAuthorized && result.EvaluatedPolicies.Count > 0)
        {
            result.Explanation = "No policy granted access - default deny";
        }

        return result;
    }

    public async Task<bool> EvaluateRBACAsync(
        string userId,
        string resource,
        string action,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Build the permission string
            var requiredPermission = $"{resource}:{action}";

            // Get user's roles
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found for RBAC evaluation", userId);
                return false;
            }

            var roleNames = await _userManager.GetRolesAsync(user);

            // Check each role's permissions
            foreach (var roleName in roleNames)
            {
                var role = await _roleManager.FindByNameAsync(roleName);
                if (role == null) continue;

                // Load permissions for this role
                await _context.Entry(role)
                    .Collection(r => r.Permissions)
                    .LoadAsync(cancellationToken);

                // Check if this role has the required permission
                var hasPermission = role.Permissions.Any(p =>
                    p.DeletedAt == null &&
                    (p.FullPermission == requiredPermission ||
                     p.FullPermission == $"{resource}:*" ||  // Wildcard action
                     p.FullPermission == "*:*"));              // Super admin permission

                if (hasPermission)
                {
                    _logger.LogDebug(
                        "User {UserId} has permission {Permission} via role {RoleName}",
                        userId, requiredPermission, roleName);
                    return true;
                }
            }

            _logger.LogDebug(
                "User {UserId} does not have permission {Permission}",
                userId, requiredPermission);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating RBAC for user {UserId}", userId);
            return false; // Fail secure
        }
    }

    public async Task<ABACEvaluationResult> EvaluateABACAsync(
        string userId,
        string resource,
        string action,
        AuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get user for attributes
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return new ABACEvaluationResult
                {
                    IsAuthorized = false,
                    Reason = "User not found"
                };
            }

            // Get user roles for subject attributes
            var userRoles = await _userManager.GetRolesAsync(user);

            // Build subject attributes from user and context
            var subjectAttributes = new Dictionary<string, object>(context.SubjectAttributes)
            {
                ["user_id"] = userId,
                ["email"] = user.Email ?? "",
                ["username"] = user.UserName ?? "",
                ["roles"] = userRoles.ToArray(),
                ["mfa_enabled"] = user.MfaEnabled
            };

            // Get all active ABAC policies
            var abacPolicies = await _context.Set<AccessPolicy>()
                .Where(p => p.IsActive && p.DeletedAt == null)
                .OrderByDescending(p => p.Priority)
                .ToListAsync(cancellationToken);

            var result = new ABACEvaluationResult
            {
                IsAuthorized = false,
                Reason = "No ABAC policy matched"
            };

            foreach (var policy in abacPolicies)
            {
                try
                {
                    // Parse policy attributes
                    var policySubjects = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                        policy.Subjects) ?? new Dictionary<string, object>();

                    var policyResources = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                        policy.Resources) ?? new Dictionary<string, object>();

                    var policyActions = System.Text.Json.JsonSerializer.Deserialize<List<string>>(
                        policy.Actions) ?? new List<string>();

                    Dictionary<string, object>? policyConditions = null;
                    if (!string.IsNullOrEmpty(policy.Conditions))
                    {
                        policyConditions = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                            policy.Conditions);
                    }

                    // 1. Match subject attributes
                    bool subjectMatches = MatchAttributes(policySubjects, subjectAttributes);
                    if (!subjectMatches)
                    {
                        _logger.LogDebug("ABAC policy {PolicyId}: Subject attributes did not match", policy.Id);
                        continue;
                    }

                    // 2. Match resource attributes
                    bool resourceMatches = MatchAttributes(policyResources, context.ResourceAttributes);
                    if (!resourceMatches)
                    {
                        _logger.LogDebug("ABAC policy {PolicyId}: Resource attributes did not match", policy.Id);
                        continue;
                    }

                    // 3. Match action
                    bool actionMatches = policyActions.Contains(action) || policyActions.Contains("*");
                    if (!actionMatches)
                    {
                        _logger.LogDebug("ABAC policy {PolicyId}: Action '{Action}' not in allowed actions", policy.Id, action);
                        result.FailedConditions.Add($"Action '{action}' not allowed");
                        continue;
                    }

                    // 4. Evaluate conditions
                    if (policyConditions != null)
                    {
                        var (conditionsPassed, failedCondition) = EvaluateConditions(policyConditions, context);
                        if (!conditionsPassed)
                        {
                            _logger.LogDebug("ABAC policy {PolicyId}: Condition failed: {Condition}", policy.Id, failedCondition);
                            result.FailedConditions.Add(failedCondition ?? "Unknown condition failed");
                            continue;
                        }
                    }

                    // All checks passed - policy matches
                    result.IsAuthorized = policy.Effect.Equals("allow", StringComparison.OrdinalIgnoreCase);
                    result.MatchedPolicies.Add(policy.Name);
                    result.Reason = $"Matched ABAC policy '{policy.Name}' with effect '{policy.Effect}'";

                    _logger.LogInformation(
                        "ABAC policy {PolicyId} matched for user {UserId}, resource: {Resource}, action: {Action}",
                        policy.Id, userId, resource, action);

                    return result; // Return first matching policy
                }
                catch (System.Text.Json.JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse ABAC policy {PolicyId} JSON attributes", policy.Id);
                    continue;
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating ABAC policies for user {UserId}", userId);
            return new ABACEvaluationResult
            {
                IsAuthorized = false,
                Reason = $"ABAC evaluation failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Matches policy attributes against actual attributes
    /// </summary>
    private bool MatchAttributes(Dictionary<string, object> policyAttributes, Dictionary<string, object> actualAttributes)
    {
        foreach (var (key, policyValue) in policyAttributes)
        {
            if (!actualAttributes.TryGetValue(key, out var actualValue))
            {
                return false; // Required attribute missing
            }

            // Handle different value types
            if (policyValue is System.Text.Json.JsonElement policyJsonElement)
            {
                if (!MatchJsonValue(policyJsonElement, actualValue))
                {
                    return false;
                }
            }
            else if (!MatchValue(policyValue, actualValue))
            {
                return false;
            }
        }

        return true; // All attributes matched
    }

    /// <summary>
    /// Matches a JSON element value against an actual value
    /// </summary>
    private bool MatchJsonValue(System.Text.Json.JsonElement policyValue, object actualValue)
    {
        switch (policyValue.ValueKind)
        {
            case System.Text.Json.JsonValueKind.String:
                return policyValue.GetString() == actualValue?.ToString();

            case System.Text.Json.JsonValueKind.Number:
                if (actualValue is int intVal)
                    return policyValue.GetInt32() == intVal;
                if (actualValue is long longVal)
                    return policyValue.GetInt64() == longVal;
                if (actualValue is double doubleVal)
                    return Math.Abs(policyValue.GetDouble() - doubleVal) < 0.0001;
                return false;

            case System.Text.Json.JsonValueKind.True:
            case System.Text.Json.JsonValueKind.False:
                return policyValue.GetBoolean() == (actualValue as bool? ?? false);

            case System.Text.Json.JsonValueKind.Array:
                // Check if actualValue is in the array
                var array = policyValue.EnumerateArray().Select(e => e.ToString()).ToList();
                return array.Contains(actualValue?.ToString());

            default:
                return false;
        }
    }

    /// <summary>
    /// Matches a policy value against an actual value
    /// </summary>
    private bool MatchValue(object policyValue, object actualValue)
    {
        if (policyValue == null && actualValue == null) return true;
        if (policyValue == null || actualValue == null) return false;

        // Array matching - check if actualValue is in array
        if (policyValue is object[] policyArray)
        {
            return policyArray.Any(v => v.Equals(actualValue));
        }

        // Direct comparison
        return policyValue.Equals(actualValue) || policyValue.ToString() == actualValue.ToString();
    }

    /// <summary>
    /// Evaluates environmental/contextual conditions
    /// </summary>
    private (bool passed, string? failedCondition) EvaluateConditions(
        Dictionary<string, object> conditions,
        AuthorizationContext context)
    {
        foreach (var (key, value) in conditions)
        {
            switch (key.ToLowerInvariant())
            {
                case "time_of_day":
                    if (value is System.Text.Json.JsonElement timeElement && timeElement.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var timeCondition = timeElement.GetString();
                        if (!EvaluateTimeCondition(timeCondition, context.Timestamp))
                        {
                            return (false, $"Time condition '{timeCondition}' not met");
                        }
                    }
                    break;

                case "ip_address":
                case "ip_range":
                    if (value is System.Text.Json.JsonElement ipElement && ipElement.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var ipRange = ipElement.GetString();
                        if (!string.IsNullOrEmpty(context.IpAddress) && !MatchIpRange(context.IpAddress, ipRange))
                        {
                            return (false, $"IP address '{context.IpAddress}' not in allowed range '{ipRange}'");
                        }
                    }
                    break;

                case "device_compliance":
                    if (value is System.Text.Json.JsonElement complianceElement && complianceElement.ValueKind == System.Text.Json.JsonValueKind.True)
                    {
                        // Check if device is compliant (would need integration with device management)
                        // For now, assume compliant if device fingerprint exists
                        if (string.IsNullOrEmpty(context.DeviceFingerprint))
                        {
                            return (false, "Device compliance check failed: No device fingerprint");
                        }
                    }
                    break;

                case "location":
                    if (value is System.Text.Json.JsonElement locationElement && locationElement.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var requiredLocation = locationElement.GetString();
                        if (context.Location != requiredLocation)
                        {
                            return (false, $"Location '{context.Location}' does not match required '{requiredLocation}'");
                        }
                    }
                    break;

                default:
                    // Unknown condition - log and skip
                    _logger.LogWarning("Unknown ABAC condition: {ConditionKey}", key);
                    break;
            }
        }

        return (true, null); // All conditions passed
    }

    /// <summary>
    /// Evaluates time-based conditions
    /// </summary>
    private bool EvaluateTimeCondition(string? condition, DateTime timestamp)
    {
        if (string.IsNullOrEmpty(condition)) return true;

        var hour = timestamp.Hour;

        return condition.ToLowerInvariant() switch
        {
            "business_hours" => hour >= 9 && hour < 17, // 9 AM - 5 PM
            "after_hours" => hour < 9 || hour >= 17,
            "daytime" => hour >= 6 && hour < 18, // 6 AM - 6 PM
            "nighttime" => hour < 6 || hour >= 18,
            _ => true // Unknown condition - allow
        };
    }

    /// <summary>
    /// Checks if an IP address matches a CIDR range
    /// </summary>
    private bool MatchIpRange(string ipAddress, string? ipRange)
    {
        if (string.IsNullOrEmpty(ipRange)) return true;

        // Simple implementation - exact match or wildcard
        if (ipRange == "*") return true;
        if (ipRange == ipAddress) return true;

        // CIDR notation (basic implementation)
        if (ipRange.Contains('/'))
        {
            // For production, use a proper CIDR library
            // For now, check if IP starts with the network prefix
            var prefix = ipRange.Split('/')[0];
            var prefixParts = prefix.Split('.');
            var ipParts = ipAddress.Split('.');

            for (int i = 0; i < Math.Min(prefixParts.Length - 1, ipParts.Length); i++)
            {
                if (prefixParts[i] != ipParts[i])
                {
                    return false;
                }
            }

            return true;
        }

        return false;
    }

    public async Task<bool> EvaluateHCLAsync(
        string userId,
        string path,
        string operation,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get user roles to determine which HCL policies apply
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found for HCL evaluation", userId);
                return false;
            }

            var userRoles = await _userManager.GetRolesAsync(user);

            // Get all active HCL policies
            var hclPolicies = await _context.Policies
                .Where(p => p.Type == PolicyType.HCL && p.IsActive && p.DeletedAt == null)
                .OrderByDescending(p => p.Priority)
                .ToListAsync(cancellationToken);

            foreach (var policy in hclPolicies)
            {
                if (string.IsNullOrEmpty(policy.Content))
                {
                    _logger.LogWarning("HCL policy {PolicyId} has empty content", policy.Id);
                    continue;
                }

                try
                {
                    // Parse HCL policy content
                    var pathRules = ParseHCLPolicy(policy.Content);

                    foreach (var rule in pathRules)
                    {
                        // Check if the requested path matches the policy path pattern
                        if (MatchHCLPath(path, rule.PathPattern))
                        {
                            // Check if the operation is allowed
                            if (rule.Capabilities.Contains(operation) || rule.Capabilities.Contains("*"))
                            {
                                var effect = policy.Effect.Equals("allow", StringComparison.OrdinalIgnoreCase);

                                _logger.LogInformation(
                                    "HCL policy {PolicyId} matched for path '{Path}', operation '{Operation}', effect: {Effect}",
                                    policy.Id, path, operation, policy.Effect);

                                return effect;
                            }
                            else
                            {
                                _logger.LogDebug(
                                    "HCL policy {PolicyId} path matched but operation '{Operation}' not in capabilities: {Capabilities}",
                                    policy.Id, operation, string.Join(", ", rule.Capabilities));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse HCL policy {PolicyId}", policy.Id);
                    continue;
                }
            }

            _logger.LogDebug("No HCL policy matched for path '{Path}', operation '{Operation}'", path, operation);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating HCL policies for user {UserId}, path '{Path}'", userId, path);
            return false; // Fail secure
        }
    }

    /// <summary>
    /// Parses HCL policy content into path rules
    /// Supports basic Vault HCL syntax:
    /// path "secret/data/*" {
    ///   capabilities = ["create", "read", "update", "delete", "list"]
    /// }
    /// </summary>
    private List<HCLPathRule> ParseHCLPolicy(string hclContent)
    {
        var rules = new List<HCLPathRule>();
        var lines = hclContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        string? currentPath = null;
        List<string>? currentCapabilities = null;
        bool inPathBlock = false;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // Skip comments
            if (trimmedLine.StartsWith("#") || trimmedLine.StartsWith("//"))
            {
                continue;
            }

            // Match: path "secret/data/*" {
            if (trimmedLine.StartsWith("path "))
            {
                var pathMatch = System.Text.RegularExpressions.Regex.Match(
                    trimmedLine,
                    @"path\s+""([^""]+)""\s*\{");

                if (pathMatch.Success)
                {
                    currentPath = pathMatch.Groups[1].Value;
                    currentCapabilities = new List<string>();
                    inPathBlock = true;
                }
            }
            // Match: capabilities = ["read", "list"]
            else if (inPathBlock && trimmedLine.Contains("capabilities"))
            {
                var capabilitiesMatch = System.Text.RegularExpressions.Regex.Match(
                    trimmedLine,
                    @"capabilities\s*=\s*\[(.*?)\]");

                if (capabilitiesMatch.Success)
                {
                    var capabilitiesStr = capabilitiesMatch.Groups[1].Value;
                    var capabilities = capabilitiesStr
                        .Split(',')
                        .Select(c => c.Trim().Trim('"').Trim('\''))
                        .Where(c => !string.IsNullOrEmpty(c))
                        .ToList();

                    currentCapabilities = capabilities;
                }
            }
            // Match: }
            else if (trimmedLine == "}")
            {
                if (inPathBlock && currentPath != null && currentCapabilities != null)
                {
                    rules.Add(new HCLPathRule
                    {
                        PathPattern = currentPath,
                        Capabilities = currentCapabilities
                    });
                }

                inPathBlock = false;
                currentPath = null;
                currentCapabilities = null;
            }
        }

        return rules;
    }

    /// <summary>
    /// Matches a path against an HCL path pattern
    /// Supports wildcards: * (any characters), + (one or more characters)
    /// </summary>
    private bool MatchHCLPath(string path, string pattern)
    {
        // Exact match
        if (path == pattern)
        {
            return true;
        }

        // Convert HCL glob pattern to regex
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")  // * matches any characters
            .Replace("\\+", ".+")  // + matches one or more characters
            + "$";

        try
        {
            return System.Text.RegularExpressions.Regex.IsMatch(path, regexPattern);
        }
        catch
        {
            // If regex fails, fall back to simple prefix matching
            if (pattern.EndsWith("*"))
            {
                var prefix = pattern.TrimEnd('*');
                return path.StartsWith(prefix);
            }

            return false;
        }
    }

    /// <summary>
    /// Represents a parsed HCL path rule
    /// </summary>
    private class HCLPathRule
    {
        public required string PathPattern { get; set; }
        public required List<string> Capabilities { get; set; }
    }

    public async Task<IEnumerable<PolicySummary>> GetApplicablePoliciesAsync(
        string userId,
        PolicyType? policyType = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Policies
            .Where(p => p.IsActive && p.DeletedAt == null);

        if (policyType.HasValue)
        {
            query = query.Where(p => p.Type == policyType.Value);
        }

        var policies = await query
            .OrderByDescending(p => p.Priority)
            .Select(p => new PolicySummary
            {
                Id = p.Id,
                Name = p.Name,
                Type = p.Type,
                Effect = p.Effect,
                Priority = p.Priority,
                IsActive = p.IsActive,
                Description = p.Description
            })
            .ToListAsync(cancellationToken);

        return policies;
    }
}

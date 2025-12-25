using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.Authorization;
using USP.Core.Services.Authorization;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Authorization;

/// <summary>
/// HCL (HashiCorp Configuration Language) policy evaluator
/// Supports Vault-style path-based policies with capabilities
/// </summary>
public class HclPolicyEvaluator : IHclPolicyEvaluator
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<HclPolicyEvaluator> _logger;

    // Supported capabilities
    private static readonly HashSet<string> ValidCapabilities = new()
    {
        "create", "read", "update", "delete", "list",
        "sudo", "deny", "patch"
    };

    public HclPolicyEvaluator(
        ApplicationDbContext context,
        ILogger<HclPolicyEvaluator> logger)
    {
        _context = context;
        _logger = logger;
    }

    public HclPolicy ParsePolicy(string hclText)
    {
        var policy = new HclPolicy
        {
            PathPolicies = new List<HclPathPolicy>()
        };

        try
        {
            // Simple regex-based HCL parser (production would use proper parser)
            // Matches: path "secret/data/*" { capabilities = ["create", "read"] }
            var pathRegex = new Regex(@"path\s+""([^""]+)""\s*\{([^}]+)\}", RegexOptions.Multiline);
            var matches = pathRegex.Matches(hclText);

            foreach (Match match in matches)
            {
                var path = match.Groups[1].Value.Trim();
                var body = match.Groups[2].Value;

                var pathPolicy = new HclPathPolicy
                {
                    Path = path,
                    Capabilities = ExtractCapabilities(body)
                };

                // Extract allowed_parameters
                var allowedParamsRegex = new Regex(@"allowed_parameters\s*=\s*\{([^}]+)\}");
                var allowedMatch = allowedParamsRegex.Match(body);
                if (allowedMatch.Success)
                {
                    pathPolicy.AllowedParameters = ParseAllowedParameters(allowedMatch.Groups[1].Value);
                }

                // Extract denied_parameters
                var deniedParamsRegex = new Regex(@"denied_parameters\s*=\s*\[([^\]]+)\]");
                var deniedMatch = deniedParamsRegex.Match(body);
                if (deniedMatch.Success)
                {
                    pathPolicy.DeniedParameters = ParseListParameter(deniedMatch.Groups[1].Value);
                }

                // Extract min_wrapping_ttl
                var minTtlRegex = new Regex(@"min_wrapping_ttl\s*=\s*(\d+)");
                var minTtlMatch = minTtlRegex.Match(body);
                if (minTtlMatch.Success && int.TryParse(minTtlMatch.Groups[1].Value, out var minTtl))
                {
                    pathPolicy.MinWrappingTtl = minTtl;
                }

                // Extract max_wrapping_ttl
                var maxTtlRegex = new Regex(@"max_wrapping_ttl\s*=\s*(\d+)");
                var maxTtlMatch = maxTtlRegex.Match(body);
                if (maxTtlMatch.Success && int.TryParse(maxTtlMatch.Groups[1].Value, out var maxTtl))
                {
                    pathPolicy.MaxWrappingTtl = maxTtl;
                }

                policy.PathPolicies.Add(pathPolicy);
            }

            _logger.LogDebug("Parsed HCL policy with {PathCount} path policies", policy.PathPolicies.Count);
            return policy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing HCL policy");
            return policy;
        }
    }

    public async Task<HclAuthorizationResponse> EvaluateAsync(HclAuthorizationRequest request)
    {
        var response = new HclAuthorizationResponse
        {
            Authorized = false
        };

        try
        {
            _logger.LogInformation("Evaluating HCL authorization for user {UserId}, action {Action}, resource {Resource}",
                request.UserId, request.Action, request.Resource);

            // Get user's roles
            var user = await _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.RolePermissions)
                            .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(u => u.Id == request.UserId);

            if (user == null)
            {
                response.DenyReason = "User not found";
                return response;
            }

            // Get all HCL policies applicable to user's roles
            var roleIds = user.UserRoles.Select(ur => ur.RoleId).ToList();

            var policies = await _context.AccessPolicies
                .Where(p => p.PolicyType == "HCL" && p.IsActive)
                .ToListAsync();

            if (!policies.Any())
            {
                response.DenyReason = "No HCL policies found";
                return response;
            }

            // Evaluate each policy
            var explicitDeny = false;
            var hasAllow = false;

            foreach (var policyEntity in policies)
            {
                var hclPolicy = ParsePolicy(policyEntity.Policy);

                foreach (var pathPolicy in hclPolicy.PathPolicies)
                {
                    if (PathMatches(request.Resource, pathPolicy.Path))
                    {
                        response.MatchedRules.Add($"{policyEntity.Name}:{pathPolicy.Path}");

                        // Check for explicit deny
                        if (pathPolicy.Capabilities.Contains("deny"))
                        {
                            explicitDeny = true;
                            response.DenyReason = $"Explicitly denied by policy {policyEntity.Name}";
                            response.PolicyName = policyEntity.Name;
                            break;
                        }

                        // Check if action is allowed
                        if (HasRequiredCapability(request.Action, pathPolicy.Capabilities))
                        {
                            hasAllow = true;
                            response.PolicyName = policyEntity.Name;
                        }
                    }
                }

                if (explicitDeny)
                {
                    break;
                }
            }

            // Decision logic: explicit deny overrides allow
            if (explicitDeny)
            {
                response.Authorized = false;
            }
            else if (hasAllow)
            {
                response.Authorized = true;
            }
            else
            {
                response.Authorized = false;
                response.DenyReason = "No policy grants required capability";
            }

            _logger.LogInformation("HCL authorization result for user {UserId}: {Authorized}",
                request.UserId, response.Authorized);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during HCL evaluation");
            response.DenyReason = $"Evaluation error: {ex.Message}";
            return response;
        }
    }

    public (bool IsValid, List<string> Errors) ValidatePolicy(string hclText)
    {
        var errors = new List<string>();

        try
        {
            if (string.IsNullOrWhiteSpace(hclText))
            {
                errors.Add("Policy cannot be empty");
                return (false, errors);
            }

            var policy = ParsePolicy(hclText);

            if (policy.PathPolicies.Count == 0)
            {
                errors.Add("Policy must define at least one path");
                return (false, errors);
            }

            foreach (var pathPolicy in policy.PathPolicies)
            {
                if (string.IsNullOrWhiteSpace(pathPolicy.Path))
                {
                    errors.Add("Path cannot be empty");
                }

                if (pathPolicy.Capabilities.Count == 0)
                {
                    errors.Add($"Path '{pathPolicy.Path}' must define at least one capability");
                }

                foreach (var capability in pathPolicy.Capabilities)
                {
                    if (!ValidCapabilities.Contains(capability))
                    {
                        errors.Add($"Invalid capability '{capability}' on path '{pathPolicy.Path}'");
                    }
                }

                // Check for conflicting capabilities
                if (pathPolicy.Capabilities.Contains("deny") && pathPolicy.Capabilities.Count > 1)
                {
                    errors.Add($"Path '{pathPolicy.Path}' cannot have 'deny' with other capabilities");
                }
            }

            return (errors.Count == 0, errors);
        }
        catch (Exception ex)
        {
            errors.Add($"Parse error: {ex.Message}");
            return (false, errors);
        }
    }

    public async Task<bool> HasCapabilityAsync(Guid userId, string path, string capability)
    {
        var capabilities = await GetCapabilitiesAsync(userId, path);
        return capabilities.Contains(capability);
    }

    public async Task<List<string>> GetCapabilitiesAsync(Guid userId, string path)
    {
        var capabilities = new List<string>();

        try
        {
            var user = await _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return capabilities;
            }

            var policies = await _context.AccessPolicies
                .Where(p => p.PolicyType == "HCL" && p.IsActive)
                .ToListAsync();

            foreach (var policyEntity in policies)
            {
                var hclPolicy = ParsePolicy(policyEntity.Policy);

                foreach (var pathPolicy in hclPolicy.PathPolicies)
                {
                    if (PathMatches(path, pathPolicy.Path))
                    {
                        // Add capabilities if not deny
                        if (!pathPolicy.Capabilities.Contains("deny"))
                        {
                            capabilities.AddRange(pathPolicy.Capabilities.Except(capabilities));
                        }
                        else
                        {
                            // Explicit deny - return empty
                            return new List<string>();
                        }
                    }
                }
            }

            return capabilities.Distinct().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting capabilities for user {UserId} on path {Path}", userId, path);
            return capabilities;
        }
    }

    #region Private Helper Methods

    private List<string> ExtractCapabilities(string body)
    {
        var capabilities = new List<string>();

        try
        {
            // Match: capabilities = ["create", "read", "update"]
            var capRegex = new Regex(@"capabilities\s*=\s*\[([^\]]+)\]");
            var match = capRegex.Match(body);

            if (match.Success)
            {
                var capList = match.Groups[1].Value;
                var caps = capList.Split(',')
                    .Select(c => c.Trim().Trim('"', '\''))
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .ToList();

                capabilities.AddRange(caps);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting capabilities");
        }

        return capabilities;
    }

    private Dictionary<string, List<string>> ParseAllowedParameters(string parametersText)
    {
        var parameters = new Dictionary<string, List<string>>();

        try
        {
            // Simple parser for: "key" = ["value1", "value2"]
            var paramRegex = new Regex(@"""([^""]+)""\s*=\s*\[([^\]]*)\]");
            var matches = paramRegex.Matches(parametersText);

            foreach (Match match in matches)
            {
                var key = match.Groups[1].Value;
                var values = ParseListParameter(match.Groups[2].Value);
                parameters[key] = values;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing allowed parameters");
        }

        return parameters;
    }

    private List<string> ParseListParameter(string listText)
    {
        return listText.Split(',')
            .Select(v => v.Trim().Trim('"', '\''))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();
    }

    private bool PathMatches(string requestPath, string policyPath)
    {
        // Convert HCL glob pattern to regex
        // * matches any sequence except /
        // + matches any sequence including /

        if (policyPath == requestPath)
        {
            return true;
        }

        // Convert glob to regex
        var regexPattern = "^" + Regex.Escape(policyPath)
            .Replace("\\*", "[^/]*")
            .Replace("\\+", ".*") + "$";

        return Regex.IsMatch(requestPath, regexPattern);
    }

    private bool HasRequiredCapability(string action, List<string> capabilities)
    {
        // Map actions to capabilities
        var actionCapabilityMap = new Dictionary<string, string>
        {
            { "create", "create" },
            { "read", "read" },
            { "get", "read" },
            { "update", "update" },
            { "put", "update" },
            { "delete", "delete" },
            { "list", "list" },
            { "patch", "patch" }
        };

        var actionLower = action.ToLowerInvariant();

        if (actionCapabilityMap.TryGetValue(actionLower, out var requiredCapability))
        {
            return capabilities.Contains(requiredCapability) || capabilities.Contains("sudo");
        }

        // Default: action must match capability exactly
        return capabilities.Contains(actionLower) || capabilities.Contains("sudo");
    }

    #endregion
}

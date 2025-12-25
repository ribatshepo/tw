using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.Authorization;
using USP.Core.Services.Authorization;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Authorization;

/// <summary>
/// Enhanced HCL (HashiCorp Configuration Language) policy evaluator
/// Supports Vault-style path-based policies with capabilities, wildcards, and caching
/// </summary>
public class HclPolicyEvaluator : IHclPolicyEvaluator
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<HclPolicyEvaluator> _logger;
    private readonly IMemoryCache _cache;

    private const string PolicyCacheKeyPrefix = "hcl:policy:";
    private const int PolicyCacheExpirationMinutes = 15;

    // Supported capabilities
    private static readonly HashSet<string> ValidCapabilities = new()
    {
        "create", "read", "update", "delete", "list",
        "sudo", "deny", "patch"
    };

    // Compiled regex patterns for better performance
    private static readonly Regex PathBlockRegex = new(@"path\s+""([^""]+)""\s*\{([^}]+)\}", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex CapabilitiesRegex = new(@"capabilities\s*=\s*\[([^\]]+)\]", RegexOptions.Compiled);
    private static readonly Regex AllowedParamsRegex = new(@"allowed_parameters\s*=\s*\{([^}]+)\}", RegexOptions.Compiled);
    private static readonly Regex DeniedParamsRegex = new(@"denied_parameters\s*=\s*\[([^\]]+)\]", RegexOptions.Compiled);
    private static readonly Regex RequiredParamsRegex = new(@"required_parameters\s*=\s*\[([^\]]+)\]", RegexOptions.Compiled);
    private static readonly Regex MinTtlRegex = new(@"min_wrapping_ttl\s*=\s*""?(\d+[smhd]?)""?", RegexOptions.Compiled);
    private static readonly Regex MaxTtlRegex = new(@"max_wrapping_ttl\s*=\s*""?(\d+[smhd]?)""?", RegexOptions.Compiled);
    private static readonly Regex ParamPairRegex = new(@"""([^""]+)""\s*=\s*\[([^\]]*)\]", RegexOptions.Compiled);

    public HclPolicyEvaluator(
        ApplicationDbContext context,
        ILogger<HclPolicyEvaluator> logger,
        IMemoryCache cache)
    {
        _context = context;
        _logger = logger;
        _cache = cache;
    }

    public HclPolicy ParsePolicy(string hclText)
    {
        var cacheKey = $"{PolicyCacheKeyPrefix}{GetPolicyHash(hclText)}";

        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(PolicyCacheExpirationMinutes);

            var policy = new HclPolicy
            {
                PathPolicies = new List<HclPathPolicy>()
            };

            try
            {
                var matches = PathBlockRegex.Matches(hclText);

                foreach (Match match in matches)
                {
                    var path = match.Groups[1].Value.Trim();
                    var body = match.Groups[2].Value;

                    var pathPolicy = ParsePathPolicy(path, body);
                    policy.PathPolicies.Add(pathPolicy);
                }

                _logger.LogDebug("Parsed and cached HCL policy with {PathCount} path policies", policy.PathPolicies.Count);
                return policy;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing HCL policy");
                return policy;
            }
        }) ?? new HclPolicy { PathPolicies = new List<HclPathPolicy>() };
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

            var roleIds = user.UserRoles.Select(ur => ur.RoleId).ToList();

            var policies = await _context.AccessPolicies
                .Where(p => p.PolicyType == "HCL" && p.IsActive)
                .ToListAsync();

            if (!policies.Any())
            {
                response.DenyReason = "No HCL policies found";
                return response;
            }

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

                        if (pathPolicy.Capabilities.Contains("deny"))
                        {
                            explicitDeny = true;
                            response.DenyReason = $"Explicitly denied by policy {policyEntity.Name}";
                            response.PolicyName = policyEntity.Name;
                            break;
                        }

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

                if (!IsValidPathPattern(pathPolicy.Path))
                {
                    errors.Add($"Invalid path pattern '{pathPolicy.Path}'. Use * for single-segment wildcard or + for multi-segment wildcard");
                }

                if (pathPolicy.Capabilities.Count == 0)
                {
                    errors.Add($"Path '{pathPolicy.Path}' must define at least one capability");
                }

                foreach (var capability in pathPolicy.Capabilities)
                {
                    if (!ValidCapabilities.Contains(capability))
                    {
                        errors.Add($"Invalid capability '{capability}' on path '{pathPolicy.Path}'. Valid: {string.Join(", ", ValidCapabilities)}");
                    }
                }

                if (pathPolicy.Capabilities.Contains("deny") && pathPolicy.Capabilities.Count > 1)
                {
                    errors.Add($"Path '{pathPolicy.Path}' cannot have 'deny' with other capabilities");
                }

                if (pathPolicy.MinWrappingTtl.HasValue && pathPolicy.MaxWrappingTtl.HasValue)
                {
                    if (pathPolicy.MinWrappingTtl.Value > pathPolicy.MaxWrappingTtl.Value)
                    {
                        errors.Add($"Path '{pathPolicy.Path}': min_wrapping_ttl cannot exceed max_wrapping_ttl");
                    }
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
                        if (!pathPolicy.Capabilities.Contains("deny"))
                        {
                            capabilities.AddRange(pathPolicy.Capabilities.Except(capabilities));
                        }
                        else
                        {
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

    private HclPathPolicy ParsePathPolicy(string path, string body)
    {
        var pathPolicy = new HclPathPolicy
        {
            Path = path,
            Capabilities = ExtractCapabilities(body)
        };

        var allowedMatch = AllowedParamsRegex.Match(body);
        if (allowedMatch.Success)
        {
            pathPolicy.AllowedParameters = ParseAllowedParameters(allowedMatch.Groups[1].Value);
        }

        var deniedMatch = DeniedParamsRegex.Match(body);
        if (deniedMatch.Success)
        {
            pathPolicy.DeniedParameters = ParseListParameter(deniedMatch.Groups[1].Value);
        }

        var requiredMatch = RequiredParamsRegex.Match(body);
        if (requiredMatch.Success)
        {
            pathPolicy.RequiredParameters = ParseListParameter(requiredMatch.Groups[1].Value);
        }

        var minTtlMatch = MinTtlRegex.Match(body);
        if (minTtlMatch.Success)
        {
            pathPolicy.MinWrappingTtl = ParseDuration(minTtlMatch.Groups[1].Value);
        }

        var maxTtlMatch = MaxTtlRegex.Match(body);
        if (maxTtlMatch.Success)
        {
            pathPolicy.MaxWrappingTtl = ParseDuration(maxTtlMatch.Groups[1].Value);
        }

        return pathPolicy;
    }

    private List<string> ExtractCapabilities(string body)
    {
        var capabilities = new List<string>();

        try
        {
            var match = CapabilitiesRegex.Match(body);

            if (match.Success)
            {
                var capList = match.Groups[1].Value;
                var caps = capList.Split(',')
                    .Select(c => c.Trim().Trim('"', '\'', ' '))
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
            var matches = ParamPairRegex.Matches(parametersText);

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
            .Select(v => v.Trim().Trim('"', '\'', ' '))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();
    }

    private int ParseDuration(string duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
        {
            return 0;
        }

        var numStr = new string(duration.TakeWhile(char.IsDigit).ToArray());
        if (!int.TryParse(numStr, out var value))
        {
            return 0;
        }

        var unit = duration.Substring(numStr.Length).Trim().ToLowerInvariant();

        return unit switch
        {
            "s" or "" => value,
            "m" => value * 60,
            "h" => value * 3600,
            "d" => value * 86400,
            _ => value
        };
    }

    private bool PathMatches(string requestPath, string policyPath)
    {
        if (policyPath == requestPath)
        {
            return true;
        }

        if (!policyPath.Contains('*') && !policyPath.Contains('+'))
        {
            return false;
        }

        var segments = policyPath.Split('/', StringSplitOptions.None);
        var requestSegments = requestPath.Split('/', StringSplitOptions.None);

        return MatchSegments(requestSegments, segments, 0, 0);
    }

    private bool MatchSegments(string[] requestSegments, string[] patternSegments, int reqIdx, int patIdx)
    {
        if (patIdx >= patternSegments.Length && reqIdx >= requestSegments.Length)
        {
            return true;
        }

        if (patIdx >= patternSegments.Length)
        {
            return false;
        }

        var pattern = patternSegments[patIdx];

        if (pattern == "+")
        {
            if (patIdx == patternSegments.Length - 1)
            {
                return true;
            }

            for (int i = reqIdx; i < requestSegments.Length; i++)
            {
                if (MatchSegments(requestSegments, patternSegments, i + 1, patIdx + 1))
                {
                    return true;
                }
            }
            return false;
        }

        if (pattern == "*")
        {
            if (reqIdx >= requestSegments.Length)
            {
                return false;
            }
            return MatchSegments(requestSegments, patternSegments, reqIdx + 1, patIdx + 1);
        }

        if (reqIdx >= requestSegments.Length)
        {
            return false;
        }

        if (pattern == requestSegments[reqIdx])
        {
            return MatchSegments(requestSegments, patternSegments, reqIdx + 1, patIdx + 1);
        }

        return false;
    }

    private bool IsValidPathPattern(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (path.Contains("**") || path.Contains("++"))
        {
            return false;
        }

        if (path.StartsWith('/') || path.EndsWith('/'))
        {
            return false;
        }

        return true;
    }

    private bool HasRequiredCapability(string action, List<string> capabilities)
    {
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

        return capabilities.Contains(actionLower) || capabilities.Contains("sudo");
    }

    private string GetPolicyHash(string hclText)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(hclText));
        return Convert.ToBase64String(hash).Substring(0, 16);
    }

    #endregion
}

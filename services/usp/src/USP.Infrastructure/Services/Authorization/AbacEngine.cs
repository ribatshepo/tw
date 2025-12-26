using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.Authorization;
using USP.Core.Models.Entities;
using USP.Core.Services.Authorization;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Authorization;

/// <summary>
/// ABAC (Attribute-Based Access Control) engine implementation
/// Extracts attributes and evaluates policies based on subject, resource, environment, and context
/// </summary>
public class AbacEngine : IAbacEngine
{
    private readonly ApplicationDbContext _context;
    private readonly IHclPolicyEvaluator _hclEvaluator;
    private readonly ILogger<AbacEngine> _logger;

    public AbacEngine(
        ApplicationDbContext context,
        IHclPolicyEvaluator hclEvaluator,
        ILogger<AbacEngine> logger)
    {
        _context = context;
        _hclEvaluator = hclEvaluator;
        _logger = logger;
    }

    public async Task<AbacEvaluationResponse> EvaluateAsync(AbacEvaluationRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = new AbacEvaluationResponse
        {
            Decision = "deny",
            Allowed = false
        };

        try
        {
            _logger.LogInformation("Evaluating ABAC policy for user {UserId}, action {Action}, resource {ResourceType}",
                request.SubjectId, request.Action, request.ResourceType);

            // Extract attributes if not provided
            var attributes = await ExtractAttributesIfNeededAsync(request);

            // Get applicable ABAC policies
            var policies = await _context.AccessPolicies
                .Where(p => p.PolicyType == "ABAC" && p.IsActive)
                .ToListAsync();

            if (policies.Count == 0)
            {
                _logger.LogWarning("No active ABAC policies found");
                response.Decision = "not_applicable";
                response.Reasons.Add("No active ABAC policies configured");
                return response;
            }

            // Evaluate each policy
            var allowDecisions = new List<string>();
            var denyDecisions = new List<string>();

            foreach (var policy in policies)
            {
                var policyEvaluation = await EvaluatePolicyAsync(policy, attributes, request);

                if (policyEvaluation.Decision == "allow")
                {
                    allowDecisions.Add(policy.Name);
                    response.AppliedPolicies.Add(policy.Name);
                }
                else if (policyEvaluation.Decision == "deny")
                {
                    denyDecisions.Add(policy.Name);
                }

                response.Reasons.AddRange(policyEvaluation.Reasons);
            }

            // Decision logic: explicit deny overrides allow
            if (denyDecisions.Any())
            {
                response.Decision = "deny";
                response.Allowed = false;
                response.Reasons.Insert(0, $"Denied by policies: {string.Join(", ", denyDecisions)}");
            }
            else if (allowDecisions.Any())
            {
                response.Decision = "allow";
                response.Allowed = true;
                response.Reasons.Insert(0, $"Allowed by policies: {string.Join(", ", allowDecisions)}");
            }
            else
            {
                response.Decision = "deny";
                response.Allowed = false;
                response.Reasons.Add("No policies explicitly allowed access");
            }

            response.EvaluationContext = new Dictionary<string, object>
            {
                { "subject_attributes", attributes.SubjectAttributes },
                { "resource_attributes", attributes.ResourceAttributes },
                { "environment_attributes", attributes.EnvironmentAttributes },
                { "policies_evaluated", policies.Count }
            };

            stopwatch.Stop();
            response.EvaluationTime = stopwatch.Elapsed;

            _logger.LogInformation("ABAC evaluation completed: {Decision} in {ElapsedMs}ms",
                response.Decision, stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ABAC evaluation");
            stopwatch.Stop();
            response.EvaluationTime = stopwatch.Elapsed;
            response.Reasons.Add($"Evaluation error: {ex.Message}");
            return response;
        }
    }

    public async Task<ExtractedAttributes> ExtractAttributesAsync(AttributeExtractionRequest request)
    {
        var attributes = new ExtractedAttributes();

        try
        {
            // Extract subject (user) attributes - comprehensive attribute extraction
            var user = await _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == request.UserId);

            if (user != null)
            {
                // Determine department from metadata
                var department = "unknown";
                var clearanceLevel = "public";
                var jobFunction = "user";
                var location = "unknown";
                var employmentType = "full-time";

                if (!string.IsNullOrEmpty(user.Metadata))
                {
                    try
                    {
                        var metadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(user.Metadata);
                        if (metadata != null)
                        {
                            if (metadata.TryGetValue("department", out var deptValue))
                                department = deptValue?.ToString() ?? "unknown";
                            if (metadata.TryGetValue("clearance_level", out var clearanceValue))
                                clearanceLevel = clearanceValue?.ToString() ?? "public";
                            if (metadata.TryGetValue("job_function", out var jobValue))
                                jobFunction = jobValue?.ToString() ?? "user";
                            if (metadata.TryGetValue("location", out var locValue))
                                location = locValue?.ToString() ?? "unknown";
                            if (metadata.TryGetValue("employment_type", out var empValue))
                                employmentType = empValue?.ToString() ?? "full-time";
                        }
                    }
                    catch
                    {
                        // Ignore metadata parsing errors
                    }
                }

                attributes.SubjectAttributes = new Dictionary<string, object>
                {
                    // Basic identity attributes
                    { "user_id", user.Id },
                    { "username", user.UserName ?? string.Empty },
                    { "email", user.Email ?? string.Empty },
                    { "status", user.Status },
                    { "is_active", user.Status == "active" },

                    // Security attributes
                    { "mfa_enabled", user.MfaEnabled },
                    { "email_confirmed", user.EmailConfirmed },
                    { "phone_confirmed", user.PhoneNumberConfirmed },
                    { "lockout_enabled", user.LockoutEnabled },
                    { "is_locked_out", user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow },

                    // Role attributes
                    { "roles", user.UserRoles.Select(ur => ur.Role.Name).ToList() },
                    { "role_count", user.UserRoles.Count },
                    { "primary_role", user.UserRoles.FirstOrDefault()?.Role.Name ?? "User" },

                    // Organizational attributes (from metadata)
                    { "department", department },
                    { "clearance_level", clearanceLevel },
                    { "job_function", jobFunction },
                    { "location", location },
                    { "employment_type", employmentType },

                    // Risk attributes (parse from JSON string)
                    { "risk_score", ExtractRiskScoreFromProfile(user.RiskProfile) },
                    { "is_high_risk", ExtractRiskScoreFromProfile(user.RiskProfile) > 70 },
                    { "is_low_risk", ExtractRiskScoreFromProfile(user.RiskProfile) < 30 },

                    // Temporal attributes
                    { "created_at", user.CreatedAt },
                    { "last_login", user.LastLoginAt ?? DateTime.MinValue },
                    { "account_age_days", (DateTime.UtcNow - user.CreatedAt).TotalDays }
                };
            }

            // Extract resource attributes
            if (!string.IsNullOrEmpty(request.ResourceType))
            {
                attributes.ResourceAttributes = await ExtractResourceAttributesAsync(
                    request.ResourceType,
                    request.ResourceId);
            }

            // Extract environment attributes - comprehensive environment context
            var now = DateTime.UtcNow;
            var isBusinessHours = IsBusinessHours(now);
            var isWeekend = now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday;

            // Extract IP and geolocation from context if available
            var ipAddress = request.AdditionalContext?.TryGetValue("ip_address", out var ipValue) == true
                ? ipValue?.ToString() ?? "unknown"
                : "unknown";
            var userAgent = request.AdditionalContext?.TryGetValue("user_agent", out var uaValue) == true
                ? uaValue?.ToString() ?? "unknown"
                : "unknown";
            var networkZone = request.AdditionalContext?.TryGetValue("network_zone", out var nzValue) == true
                ? nzValue?.ToString() ?? "external"
                : "external";
            var deviceCompliance = request.AdditionalContext?.TryGetValue("device_compliance", out var dcValue) == true
                ? dcValue?.ToString() ?? "unknown"
                : "unknown";
            var geoLocation = request.AdditionalContext?.TryGetValue("geo_location", out var geoValue) == true
                ? geoValue?.ToString() ?? "unknown"
                : "unknown";

            attributes.EnvironmentAttributes = new Dictionary<string, object>
            {
                // Time attributes
                { "current_time", now },
                { "day_of_week", now.DayOfWeek.ToString() },
                { "hour_of_day", now.Hour },
                { "is_business_hours", isBusinessHours },
                { "is_weekend", isWeekend },
                { "is_business_day", !isWeekend },
                { "time_of_day", GetTimeOfDay(now.Hour) }, // morning, afternoon, evening, night

                // Network attributes
                { "ip_address", ipAddress },
                { "network_zone", networkZone }, // "internal", "external", "vpn", "dmz"
                { "is_internal_network", networkZone == "internal" || networkZone == "vpn" },

                // Device attributes
                { "user_agent", userAgent },
                { "device_compliance_status", deviceCompliance }, // "compliant", "non-compliant", "unknown"
                { "is_compliant_device", deviceCompliance == "compliant" },

                // Location attributes
                { "geo_location", geoLocation },
                { "geo_country", ExtractCountryFromGeo(geoLocation) },
                { "is_restricted_location", IsRestrictedLocation(geoLocation) },

                // System attributes
                { "server_timezone", TimeZoneInfo.Local.Id },
                { "environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production" },
                { "is_production", (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production") == "Production" }
            };

            // Extract context attributes
            if (request.AdditionalContext != null)
            {
                attributes.ContextAttributes = request.AdditionalContext;
            }

            _logger.LogDebug("Extracted {SubjectCount} subject, {ResourceCount} resource, {EnvCount} environment attributes",
                attributes.SubjectAttributes.Count,
                attributes.ResourceAttributes.Count,
                attributes.EnvironmentAttributes.Count);

            return attributes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting attributes");
            return attributes;
        }
    }

    public async Task<bool> HasAccessAsync(
        Guid userId,
        string action,
        string resourceType,
        string? resourceId = null,
        Dictionary<string, object>? context = null)
    {
        var request = new AbacEvaluationRequest
        {
            SubjectId = userId,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Context = context
        };

        var result = await EvaluateAsync(request);
        return result.Allowed;
    }

    public async Task<List<PolicyDto>> GetApplicablePoliciesAsync(Guid userId, string action, string resourceType)
    {
        var policies = await _context.AccessPolicies
            .Where(p => p.IsActive && (p.PolicyType == "ABAC" || p.PolicyType == "HCL"))
            .ToListAsync();

        return policies.Select(p => new PolicyDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description ?? string.Empty,
            PolicyType = p.PolicyType,
            Policy = p.Policy,
            IsActive = p.IsActive,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
            CreatedBy = p.CreatedBy
        }).ToList();
    }

    public async Task<PolicySimulationResponse> SimulatePolicyAsync(PolicySimulationRequest request)
    {
        var response = new PolicySimulationResponse();
        var evaluationSteps = new List<string>();

        try
        {
            evaluationSteps.Add($"Starting policy simulation for policy {request.PolicyId}");

            var policy = await _context.AccessPolicies.FindAsync(request.PolicyId);
            if (policy == null)
            {
                response.Decision = "error";
                response.Explanation = "Policy not found";
                return response;
            }

            evaluationSteps.Add($"Policy found: {policy.Name} (Type: {policy.PolicyType})");

            // Extract attributes
            var attributes = new ExtractedAttributes
            {
                SubjectAttributes = request.SubjectAttributes ?? new Dictionary<string, object>(),
                ResourceAttributes = request.ResourceAttributes ?? new Dictionary<string, object>()
            };

            if (attributes.SubjectAttributes.Count == 0)
            {
                var extractionRequest = new AttributeExtractionRequest
                {
                    UserId = request.UserId,
                    ResourceType = request.Resource
                };
                attributes = await ExtractAttributesAsync(extractionRequest);
                evaluationSteps.Add($"Extracted {attributes.SubjectAttributes.Count} subject attributes");
            }

            // Build evaluation request
            var evalRequest = new AbacEvaluationRequest
            {
                SubjectId = request.UserId,
                Action = request.Action,
                ResourceType = request.Resource,
                SubjectAttributes = attributes.SubjectAttributes,
                ResourceAttributes = attributes.ResourceAttributes,
                Context = request.Context
            };

            // Evaluate policy
            var policyEvaluation = await EvaluatePolicyAsync(policy, attributes, evalRequest);
            evaluationSteps.Add($"Policy evaluation result: {policyEvaluation.Decision}");

            response.Allowed = policyEvaluation.Decision == "allow";
            response.Decision = policyEvaluation.Decision;
            response.AppliedRules = policyEvaluation.MatchedRules;
            response.EvaluationSteps = evaluationSteps;
            response.AttributesUsed = new Dictionary<string, object>
            {
                { "subject", attributes.SubjectAttributes },
                { "resource", attributes.ResourceAttributes },
                { "environment", attributes.EnvironmentAttributes }
            };
            response.Explanation = string.Join("\n", policyEvaluation.Reasons);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error simulating policy");
            response.Decision = "error";
            response.Explanation = ex.Message;
            response.EvaluationSteps = evaluationSteps;
            return response;
        }
    }

    #region Private Helper Methods

    private async Task<ExtractedAttributes> ExtractAttributesIfNeededAsync(AbacEvaluationRequest request)
    {
        if (request.SubjectAttributes != null &&
            request.ResourceAttributes != null &&
            request.EnvironmentAttributes != null)
        {
            return new ExtractedAttributes
            {
                SubjectAttributes = request.SubjectAttributes,
                ResourceAttributes = request.ResourceAttributes,
                EnvironmentAttributes = request.EnvironmentAttributes,
                ContextAttributes = request.Context ?? new Dictionary<string, object>()
            };
        }

        var extractionRequest = new AttributeExtractionRequest
        {
            UserId = request.SubjectId,
            ResourceType = request.ResourceType,
            ResourceId = request.ResourceId,
            AdditionalContext = request.Context
        };

        return await ExtractAttributesAsync(extractionRequest);
    }

    private async Task<Dictionary<string, object>> ExtractResourceAttributesAsync(string resourceType, string? resourceId)
    {
        var attributes = new Dictionary<string, object>
        {
            { "resource_type", resourceType }
        };

        if (string.IsNullOrEmpty(resourceId))
        {
            return attributes;
        }

        try
        {
            switch (resourceType.ToLowerInvariant())
            {
                case "secret":
                    var secret = await _context.Secrets
                        .Where(s => s.Id.ToString() == resourceId || s.Path == resourceId)
                        .FirstOrDefaultAsync();
                    if (secret != null)
                    {
                        // Determine classification from metadata or path
                        var classification = "internal";
                        var sensitivityLevel = "medium";
                        var owner = secret.CreatedBy.ToString();
                        var tags = new List<string>();

                        if (secret.Metadata != null)
                        {
                            try
                            {
                                if (secret.Metadata.RootElement.TryGetProperty("classification", out var classValue))
                                    classification = classValue.GetString() ?? "internal";
                                if (secret.Metadata.RootElement.TryGetProperty("sensitivity", out var sensValue))
                                    sensitivityLevel = sensValue.GetString() ?? "medium";
                                if (secret.Metadata.RootElement.TryGetProperty("owner", out var ownerValue))
                                    owner = ownerValue.GetString() ?? owner;
                                if (secret.Metadata.RootElement.TryGetProperty("tags", out var tagsValue) && tagsValue.ValueKind == JsonValueKind.Array)
                                {
                                    tags = tagsValue.EnumerateArray().Select(t => t.GetString() ?? "").Where(t => !string.IsNullOrEmpty(t)).ToList();
                                }
                            }
                            catch { }
                        }

                        attributes.Add("resource_id", secret.Id);
                        attributes.Add("path", secret.Path);
                        attributes.Add("version", secret.Version);
                        attributes.Add("created_by", secret.CreatedBy);
                        attributes.Add("is_deleted", secret.IsDeleted);
                        attributes.Add("classification", classification); // public, internal, confidential, secret, top-secret
                        attributes.Add("sensitivity_level", sensitivityLevel); // low, medium, high, critical
                        attributes.Add("owner", owner);
                        attributes.Add("department", ExtractDepartmentFromPath(secret.Path));
                        attributes.Add("workspace", ExtractWorkspaceFromPath(secret.Path));
                        attributes.Add("tags", tags);
                        attributes.Add("lifecycle_stage", secret.IsDeleted ? "deleted" : "active");
                        attributes.Add("age_days", (DateTime.UtcNow - secret.CreatedAt).TotalDays);
                    }
                    break;

                case "role":
                    if (Guid.TryParse(resourceId, out var roleId))
                    {
                        var role = await _context.Roles.FindAsync(roleId);
                        if (role != null)
                        {
                            attributes.Add("resource_id", role.Id);
                            attributes.Add("role_name", role.Name ?? string.Empty);
                            attributes.Add("is_built_in", role.IsBuiltIn);
                        }
                    }
                    break;

                case "policy":
                    if (Guid.TryParse(resourceId, out var policyId))
                    {
                        var policy = await _context.AccessPolicies.FindAsync(policyId);
                        if (policy != null)
                        {
                            attributes.Add("resource_id", policy.Id);
                            attributes.Add("policy_name", policy.Name);
                            attributes.Add("policy_type", policy.PolicyType);
                            attributes.Add("is_active", policy.IsActive);
                        }
                    }
                    break;

                default:
                    attributes.Add("resource_id", resourceId);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting resource attributes for {ResourceType} {ResourceId}",
                resourceType, resourceId);
        }

        return attributes;
    }

    private async Task<PolicyEvaluationResult> EvaluatePolicyAsync(
        AccessPolicy policy,
        ExtractedAttributes attributes,
        AbacEvaluationRequest request)
    {
        var result = new PolicyEvaluationResult
        {
            Decision = "deny",
            Reasons = new List<string>(),
            MatchedRules = new List<string>()
        };

        try
        {
            if (policy.PolicyType == "ABAC")
            {
                // Parse JSON ABAC policy
                var policyDoc = JsonDocument.Parse(policy.Policy);
                result = EvaluateAbacPolicy(policyDoc, attributes, request);
            }
            else if (policy.PolicyType == "HCL")
            {
                var hclRequest = new HclAuthorizationRequest
                {
                    UserId = request.SubjectId,
                    Resource = request.ResourceType,
                    Action = request.Action
                };
                var hclResult = await _hclEvaluator.EvaluateAsync(hclRequest);
                result.Decision = hclResult.Authorized ? "allow" : "deny";
                result.Reasons.Add(hclResult.DenyReason ?? "HCL policy evaluated");
                if (hclResult.MatchedRules.Any())
                {
                    result.MatchedRules.AddRange(hclResult.MatchedRules);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating policy {PolicyName}", policy.Name);
            result.Reasons.Add($"Policy evaluation error: {ex.Message}");
            return result;
        }
    }

    private PolicyEvaluationResult EvaluateAbacPolicy(
        JsonDocument policyDoc,
        ExtractedAttributes attributes,
        AbacEvaluationRequest request)
    {
        var result = new PolicyEvaluationResult
        {
            Decision = "deny",
            Reasons = new List<string>(),
            MatchedRules = new List<string>()
        };

        try
        {
            var root = policyDoc.RootElement;

            // Check if policy has rules
            if (!root.TryGetProperty("rules", out var rulesElement))
            {
                result.Reasons.Add("Policy has no rules defined");
                return result;
            }

            // Evaluate each rule
            foreach (var rule in rulesElement.EnumerateArray())
            {
                var ruleMatches = EvaluateAbacRule(rule, attributes, request);

                if (ruleMatches.Matches)
                {
                    result.MatchedRules.Add(ruleMatches.RuleName);

                    if (ruleMatches.Effect == "allow")
                    {
                        result.Decision = "allow";
                        result.Reasons.Add($"Rule '{ruleMatches.RuleName}' allowed access");
                    }
                    else if (ruleMatches.Effect == "deny")
                    {
                        result.Decision = "deny";
                        result.Reasons.Add($"Rule '{ruleMatches.RuleName}' denied access");
                        // Explicit deny, stop evaluation
                        return result;
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating ABAC policy");
            result.Reasons.Add($"ABAC evaluation error: {ex.Message}");
            return result;
        }
    }

    private AbacRuleMatch EvaluateAbacRule(
        JsonElement rule,
        ExtractedAttributes attributes,
        AbacEvaluationRequest request)
    {
        var match = new AbacRuleMatch
        {
            Matches = false,
            RuleName = rule.TryGetProperty("name", out var nameElem) ? nameElem.GetString() ?? "unnamed" : "unnamed",
            Effect = rule.TryGetProperty("effect", out var effectElem) ? effectElem.GetString() ?? "deny" : "deny"
        };

        try
        {
            // Check action match
            if (rule.TryGetProperty("action", out var actionElem))
            {
                var requiredAction = actionElem.GetString();
                if (requiredAction != "*" && requiredAction != request.Action)
                {
                    return match;
                }
            }

            // Check resource match
            if (rule.TryGetProperty("resource", out var resourceElem))
            {
                var requiredResource = resourceElem.GetString();
                if (requiredResource != "*" && requiredResource != request.ResourceType)
                {
                    return match;
                }
            }

            // Check conditions
            if (rule.TryGetProperty("conditions", out var conditionsElem))
            {
                if (!EvaluateConditions(conditionsElem, attributes))
                {
                    return match;
                }
            }

            match.Matches = true;
            return match;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error evaluating ABAC rule {RuleName}", match.RuleName);
            return match;
        }
    }

    private bool EvaluateConditions(JsonElement conditions, ExtractedAttributes attributes)
    {
        // Simple condition evaluation (can be extended)
        foreach (var condition in conditions.EnumerateObject())
        {
            var attributeName = condition.Name;
            var expectedValue = condition.Value;

            // Check subject attributes
            if (attributes.SubjectAttributes.TryGetValue(attributeName, out var actualValue))
            {
                if (!CompareValues(actualValue, expectedValue))
                {
                    return false;
                }
            }
            else if (attributes.ResourceAttributes.TryGetValue(attributeName, out actualValue))
            {
                if (!CompareValues(actualValue, expectedValue))
                {
                    return false;
                }
            }
            else if (attributes.EnvironmentAttributes.TryGetValue(attributeName, out actualValue))
            {
                if (!CompareValues(actualValue, expectedValue))
                {
                    return false;
                }
            }
            else
            {
                // Attribute not found
                return false;
            }
        }

        return true;
    }

    private bool CompareValues(object actual, JsonElement expected)
    {
        try
        {
            switch (expected.ValueKind)
            {
                case JsonValueKind.String:
                    return actual.ToString() == expected.GetString();
                case JsonValueKind.Number:
                    return Convert.ToDouble(actual) == expected.GetDouble();
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return Convert.ToBoolean(actual) == expected.GetBoolean();
                default:
                    return false;
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool IsBusinessHours(DateTime time)
    {
        return time.DayOfWeek != DayOfWeek.Saturday &&
               time.DayOfWeek != DayOfWeek.Sunday &&
               time.Hour >= 9 &&
               time.Hour < 17;
    }

    private static string GetTimeOfDay(int hour)
    {
        return hour switch
        {
            >= 6 and < 12 => "morning",
            >= 12 and < 17 => "afternoon",
            >= 17 and < 21 => "evening",
            _ => "night"
        };
    }

    private static string ExtractCountryFromGeo(string geoLocation)
    {
        if (string.IsNullOrEmpty(geoLocation) || geoLocation == "unknown")
            return "unknown";

        // Format: "Country/City" or "Country"
        var parts = geoLocation.Split('/');
        return parts.Length > 0 ? parts[0] : "unknown";
    }

    private static bool IsRestrictedLocation(string geoLocation)
    {
        if (string.IsNullOrEmpty(geoLocation) || geoLocation == "unknown")
            return false;

        // Define restricted countries/locations
        var restrictedLocations = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "XX", "Unknown", "Anonymous"
        };

        var country = ExtractCountryFromGeo(geoLocation);
        return restrictedLocations.Contains(country);
    }

    private static string ExtractDepartmentFromPath(string path)
    {
        // Format: "department/workspace/secret"
        if (string.IsNullOrEmpty(path))
            return "unknown";

        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : "unknown";
    }

    private static string ExtractWorkspaceFromPath(string path)
    {
        // Format: "department/workspace/secret"
        if (string.IsNullOrEmpty(path))
            return "unknown";

        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[1] : "unknown";
    }

    private static int ExtractRiskScoreFromProfile(string? riskProfileJson)
    {
        if (string.IsNullOrEmpty(riskProfileJson))
            return 0;

        try
        {
            var doc = JsonDocument.Parse(riskProfileJson);
            if (doc.RootElement.TryGetProperty("CurrentRiskScore", out var scoreElement))
            {
                return scoreElement.GetInt32();
            }
            // Try alternative property name
            if (doc.RootElement.TryGetProperty("currentRiskScore", out scoreElement))
            {
                return scoreElement.GetInt32();
            }
            // Try alternative property name
            if (doc.RootElement.TryGetProperty("risk_score", out scoreElement))
            {
                return scoreElement.GetInt32();
            }
        }
        catch
        {
            // Ignore JSON parsing errors
        }

        return 0; // Default to 0 if parsing fails
    }

    #endregion
}

#region Helper Classes

internal class PolicyEvaluationResult
{
    public string Decision { get; set; } = "deny";
    public List<string> Reasons { get; set; } = new();
    public List<string> MatchedRules { get; set; } = new();
}

internal class AbacRuleMatch
{
    public bool Matches { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string Effect { get; set; } = "deny";
}

#endregion

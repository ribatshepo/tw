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
            // Extract subject (user) attributes
            var user = await _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == request.UserId);

            if (user != null)
            {
                attributes.SubjectAttributes = new Dictionary<string, object>
                {
                    { "user_id", user.Id },
                    { "username", user.UserName ?? string.Empty },
                    { "email", user.Email ?? string.Empty },
                    { "status", user.Status },
                    { "is_active", user.Status == "active" },
                    { "mfa_enabled", user.MfaEnabled },
                    { "email_confirmed", user.EmailConfirmed },
                    { "created_at", user.CreatedAt },
                    { "roles", user.UserRoles.Select(ur => ur.Role.Name).ToList() },
                    { "role_count", user.UserRoles.Count }
                };
            }

            // Extract resource attributes
            if (!string.IsNullOrEmpty(request.ResourceType))
            {
                attributes.ResourceAttributes = await ExtractResourceAttributesAsync(
                    request.ResourceType,
                    request.ResourceId);
            }

            // Extract environment attributes
            attributes.EnvironmentAttributes = new Dictionary<string, object>
            {
                { "current_time", DateTime.UtcNow },
                { "day_of_week", DateTime.UtcNow.DayOfWeek.ToString() },
                { "hour_of_day", DateTime.UtcNow.Hour },
                { "is_business_hours", IsBusinessHours(DateTime.UtcNow) },
                { "server_timezone", TimeZoneInfo.Local.Id },
                { "environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production" }
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
                        attributes.Add("resource_id", secret.Id);
                        attributes.Add("path", secret.Path);
                        attributes.Add("version", secret.Version);
                        attributes.Add("created_by", secret.CreatedBy);
                        attributes.Add("is_deleted", secret.IsDeleted);
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

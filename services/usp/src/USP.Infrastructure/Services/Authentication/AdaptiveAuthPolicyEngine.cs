using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.Authentication;
using USP.Core.Models.Entities;
using USP.Core.Services.Authentication;
using USP.Core.Services.Mfa;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Authentication;

/// <summary>
/// Adaptive authentication policy engine implementation
/// Provides risk-based authentication with step-up MFA challenges
/// </summary>
public class AdaptiveAuthPolicyEngine : IAdaptiveAuthPolicyEngine
{
    private readonly ApplicationDbContext _context;
    private readonly IRiskAssessmentService _riskAssessment;
    private readonly IMfaService _mfaService;
    private readonly ILogger<AdaptiveAuthPolicyEngine> _logger;

    public AdaptiveAuthPolicyEngine(
        ApplicationDbContext context,
        IRiskAssessmentService riskAssessment,
        IMfaService mfaService,
        ILogger<AdaptiveAuthPolicyEngine> logger)
    {
        _context = context;
        _riskAssessment = riskAssessment;
        _mfaService = mfaService;
        _logger = logger;
    }

    public async Task<PolicyEvaluationResultDto> EvaluatePolicyAsync(
        Guid userId,
        int riskScore,
        string? resourcePath = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? deviceFingerprint = null)
    {
        _logger.LogInformation("Evaluating adaptive auth policy for user {UserId}, risk score {RiskScore}", userId, riskScore);

        // Get active policies ordered by priority (highest first)
        var policies = await _context.AdaptiveAuthPolicies
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.Priority)
            .ToListAsync();

        // Find matching policy
        AdaptiveAuthPolicy? matchedPolicy = null;
        foreach (var policy in policies)
        {
            // Check if risk score is in policy range
            if (riskScore >= policy.MinRiskScore && riskScore <= policy.MaxRiskScore)
            {
                // Check resource pattern match if specified
                if (policy.ResourcePatterns != null && resourcePath != null)
                {
                    var patterns = JsonSerializer.Deserialize<List<string>>(policy.ResourcePatterns) ?? new List<string>();
                    if (patterns.Any() && !MatchesResourcePattern(resourcePath, patterns))
                    {
                        continue; // Resource doesn't match, try next policy
                    }
                }

                matchedPolicy = policy;
                break; // Use highest priority matching policy
            }
        }

        // Default to allow if no policy matches
        if (matchedPolicy == null)
        {
            _logger.LogInformation("No matching policy found for risk score {RiskScore}, allowing access", riskScore);
            return new PolicyEvaluationResultDto
            {
                Action = "allow",
                RiskScore = riskScore,
                RiskLevel = DetermineRiskLevel(riskScore),
                Reason = "No matching policy, default allow"
            };
        }

        // Build result based on matched policy
        var requiredFactors = JsonSerializer.Deserialize<List<string>>(matchedPolicy.RequiredFactors) ?? new List<string>();

        var result = new PolicyEvaluationResultDto
        {
            Action = matchedPolicy.Action,
            RiskScore = riskScore,
            RiskLevel = DetermineRiskLevel(riskScore),
            PolicyId = matchedPolicy.Id,
            PolicyName = matchedPolicy.Name,
            RequiredFactors = requiredFactors,
            RequiredFactorCount = matchedPolicy.RequiredFactorCount,
            StepUpValidityMinutes = matchedPolicy.StepUpValidityMinutes,
            Reason = $"Policy '{matchedPolicy.Name}' triggered (risk {riskScore}, range {matchedPolicy.MinRiskScore}-{matchedPolicy.MaxRiskScore})"
        };

        // Record evaluation event
        await RecordAuthenticationEventAsync(
            userId,
            "policy_evaluation",
            riskScore,
            $"policy_matched_{matchedPolicy.Action}",
            null,
            matchedPolicy.Id,
            new Dictionary<string, string>
            {
                { "resource_path", resourcePath ?? "" },
                { "ip_address", ipAddress ?? "" },
                { "policy_action", matchedPolicy.Action }
            });

        _logger.LogInformation(
            "Policy '{PolicyName}' matched for user {UserId}, action: {Action}",
            matchedPolicy.Name, userId, matchedPolicy.Action);

        return result;
    }

    public async Task<StepUpChallengeDto> InitiateStepUpAsync(
        Guid userId,
        List<string> requiredFactors,
        string? resourcePath = null,
        int validityMinutes = 15)
    {
        _logger.LogInformation("Initiating step-up challenge for user {UserId}", userId);

        // Generate session token
        var sessionToken = GenerateSessionToken();
        var expiresAt = DateTime.UtcNow.AddMinutes(validityMinutes);

        // Create step-up session
        var session = new StepUpSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SessionToken = sessionToken,
            CompletedFactors = "[]",
            InitiatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            ResourcePath = resourcePath,
            IsCompleted = false,
            IsValid = true
        };

        _context.StepUpSessions.Add(session);
        await _context.SaveChangesAsync();

        // Build challenge data (factor-specific info)
        var challengeData = new Dictionary<string, object>();
        foreach (var factor in requiredFactors)
        {
            if (factor == "totp")
            {
                challengeData["totp"] = new { message = "Enter TOTP code from authenticator app" };
            }
            else if (factor == "sms")
            {
                // Trigger SMS send (would integrate with MfaService)
                challengeData["sms"] = new { message = "SMS code sent to registered phone" };
            }
            else if (factor == "webauthn")
            {
                challengeData["webauthn"] = new { message = "Use security key or biometric" };
            }
            else if (factor == "push")
            {
                challengeData["push"] = new { message = "Push notification sent to device" };
            }
        }

        _logger.LogInformation(
            "Step-up challenge created with session token, expires at {ExpiresAt}",
            expiresAt);

        return new StepUpChallengeDto
        {
            SessionToken = sessionToken,
            RequiredFactors = requiredFactors,
            RequiredFactorCount = requiredFactors.Count,
            ExpiresAt = expiresAt,
            ValidityMinutes = validityMinutes,
            ResourcePath = resourcePath,
            ChallengeData = challengeData
        };
    }

    public async Task<StepUpValidationResultDto> ValidateStepUpFactorAsync(
        string sessionToken,
        Guid userId,
        string factor,
        string credential)
    {
        _logger.LogInformation("Validating step-up factor {Factor} for session {SessionToken}", factor, sessionToken);

        // Get session
        var session = await _context.StepUpSessions
            .FirstOrDefaultAsync(s => s.SessionToken == sessionToken && s.UserId == userId);

        if (session == null)
        {
            _logger.LogWarning("Step-up session not found: {SessionToken}", sessionToken);
            return new StepUpValidationResultDto
            {
                IsValid = false,
                Factor = factor,
                ErrorMessage = "Invalid or expired session"
            };
        }

        // Check if session is valid and not expired
        if (!session.IsValid || session.ExpiresAt < DateTime.UtcNow)
        {
            _logger.LogWarning("Step-up session expired or invalid: {SessionToken}", sessionToken);
            return new StepUpValidationResultDto
            {
                IsValid = false,
                Factor = factor,
                ErrorMessage = "Session expired or invalid"
            };
        }

        // Validate the factor credential
        bool isFactorValid = await ValidateFactorCredential(userId, factor, credential);

        if (!isFactorValid)
        {
            _logger.LogWarning("Step-up factor validation failed for {Factor}", factor);
            return new StepUpValidationResultDto
            {
                IsValid = false,
                Factor = factor,
                ErrorMessage = $"Invalid {factor} credential"
            };
        }

        // Add factor to completed factors
        var completedFactors = JsonSerializer.Deserialize<List<string>>(session.CompletedFactors) ?? new List<string>();
        if (!completedFactors.Contains(factor))
        {
            completedFactors.Add(factor);
            session.CompletedFactors = JsonSerializer.Serialize(completedFactors);
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation("Step-up factor {Factor} validated successfully", factor);

        // Determine remaining factors (this would need to be tracked in session metadata)
        // For simplicity, assuming we just track what's completed
        var allFactorsCompleted = true; // Simplified - in reality, compare with required factors

        return new StepUpValidationResultDto
        {
            IsValid = true,
            Factor = factor,
            CompletedFactors = completedFactors,
            RemainingFactors = new List<string>(), // Would calculate based on required vs completed
            AllFactorsCompleted = allFactorsCompleted
        };
    }

    public async Task<StepUpCompletionResultDto> CompleteStepUpAsync(
        string sessionToken,
        Guid userId)
    {
        _logger.LogInformation("Completing step-up session {SessionToken}", sessionToken);

        var session = await _context.StepUpSessions
            .FirstOrDefaultAsync(s => s.SessionToken == sessionToken && s.UserId == userId);

        if (session == null)
        {
            return new StepUpCompletionResultDto
            {
                IsCompleted = false,
                SessionToken = sessionToken,
                ErrorMessage = "Session not found"
            };
        }

        if (!session.IsValid || session.ExpiresAt < DateTime.UtcNow)
        {
            return new StepUpCompletionResultDto
            {
                IsCompleted = false,
                SessionToken = sessionToken,
                ErrorMessage = "Session expired or invalid"
            };
        }

        // Mark as completed
        session.IsCompleted = true;
        session.CompletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var completedFactors = JsonSerializer.Deserialize<List<string>>(session.CompletedFactors) ?? new List<string>();

        _logger.LogInformation("Step-up session completed successfully");

        return new StepUpCompletionResultDto
        {
            IsCompleted = true,
            SessionToken = sessionToken,
            CompletedFactors = completedFactors,
            ExpiresAt = session.ExpiresAt,
            ResourcePath = session.ResourcePath
        };
    }

    public async Task<bool> HasValidStepUpSessionAsync(Guid userId, string? resourcePath = null)
    {
        var now = DateTime.UtcNow;

        var query = _context.StepUpSessions
            .Where(s => s.UserId == userId &&
                       s.IsValid &&
                       s.IsCompleted &&
                       s.ExpiresAt > now);

        if (resourcePath != null)
        {
            query = query.Where(s => s.ResourcePath == resourcePath);
        }

        return await query.AnyAsync();
    }

    public async Task RecordAuthenticationEventAsync(
        Guid userId,
        string eventType,
        int riskScore,
        string outcome,
        List<string>? factorsUsed = null,
        Guid? policyId = null,
        Dictionary<string, string>? metadata = null)
    {
        var authEvent = new AuthenticationEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventType = eventType,
            RiskScore = riskScore,
            RiskLevel = DetermineRiskLevel(riskScore),
            FactorsUsed = JsonSerializer.Serialize(factorsUsed ?? new List<string>()),
            Outcome = outcome,
            PolicyId = policyId,
            EventTime = DateTime.UtcNow,
            Metadata = metadata != null ? JsonSerializer.Serialize(metadata) : null
        };

        // Extract metadata fields if provided
        if (metadata != null)
        {
            if (metadata.ContainsKey("ip_address"))
                authEvent.IpAddress = metadata["ip_address"];
            if (metadata.ContainsKey("user_agent"))
                authEvent.UserAgent = metadata["user_agent"];
            if (metadata.ContainsKey("location"))
                authEvent.Location = metadata["location"];
            if (metadata.ContainsKey("device_fingerprint"))
                authEvent.DeviceFingerprint = metadata["device_fingerprint"];
            if (metadata.ContainsKey("resource_path"))
                authEvent.ResourcePath = metadata["resource_path"];
            if (metadata.ContainsKey("failure_reason"))
                authEvent.FailureReason = metadata["failure_reason"];
            if (metadata.ContainsKey("policy_action"))
                authEvent.PolicyAction = metadata["policy_action"];
        }

        _context.AuthenticationEvents.Add(authEvent);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Recorded authentication event: {EventType} for user {UserId}, outcome: {Outcome}",
            eventType, userId, outcome);
    }

    public async Task<List<AuthenticationEventDto>> GetAuthenticationEventsAsync(
        Guid userId,
        string? eventType = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int limit = 100)
    {
        var query = _context.AuthenticationEvents
            .Where(e => e.UserId == userId);

        if (eventType != null)
        {
            query = query.Where(e => e.EventType == eventType);
        }

        if (startDate.HasValue)
        {
            query = query.Where(e => e.EventTime >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(e => e.EventTime <= endDate.Value);
        }

        var events = await query
            .OrderByDescending(e => e.EventTime)
            .Take(limit)
            .Include(e => e.Policy)
            .ToListAsync();

        return events.Select(e => new AuthenticationEventDto
        {
            EventId = e.Id,
            UserId = e.UserId,
            EventType = e.EventType,
            RiskScore = e.RiskScore,
            RiskLevel = e.RiskLevel,
            FactorsUsed = JsonSerializer.Deserialize<List<string>>(e.FactorsUsed) ?? new List<string>(),
            Outcome = e.Outcome,
            PolicyId = e.PolicyId,
            PolicyName = e.Policy?.Name,
            PolicyAction = e.PolicyAction,
            IpAddress = e.IpAddress,
            UserAgent = e.UserAgent,
            Location = e.Location,
            DeviceFingerprint = e.DeviceFingerprint,
            IsTrustedDevice = e.IsTrustedDevice,
            ResourcePath = e.ResourcePath,
            FailureReason = e.FailureReason,
            EventTime = e.EventTime,
            Metadata = e.Metadata != null
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(e.Metadata)
                : null
        }).ToList();
    }

    public async Task<AdaptiveAuthPolicyDto> CreateOrUpdatePolicyAsync(CreateAdaptiveAuthPolicyDto request)
    {
        AdaptiveAuthPolicy policy;

        if (request.PolicyId.HasValue)
        {
            // Update existing
            policy = await _context.AdaptiveAuthPolicies.FindAsync(request.PolicyId.Value)
                ?? throw new InvalidOperationException($"Policy {request.PolicyId} not found");

            policy.Name = request.Name;
            policy.Description = request.Description;
            policy.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            // Create new
            policy = new AdaptiveAuthPolicy
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Description = request.Description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.AdaptiveAuthPolicies.Add(policy);
        }

        // Set properties
        policy.MinRiskScore = request.MinRiskScore;
        policy.MaxRiskScore = request.MaxRiskScore;
        policy.RequiredFactors = JsonSerializer.Serialize(request.RequiredFactors);
        policy.RequiredFactorCount = request.RequiredFactorCount;
        policy.StepUpValidityMinutes = request.StepUpValidityMinutes;
        policy.TriggerConditions = request.TriggerConditions != null
            ? JsonSerializer.Serialize(request.TriggerConditions)
            : null;
        policy.ResourcePatterns = request.ResourcePatterns != null
            ? JsonSerializer.Serialize(request.ResourcePatterns)
            : null;
        policy.IsActive = request.IsActive;
        policy.Priority = request.Priority;
        policy.Action = request.Action;

        await _context.SaveChangesAsync();

        return await MapToPolicyDto(policy);
    }

    public async Task<AdaptiveAuthPolicyDto> GetPolicyAsync(Guid policyId)
    {
        var policy = await _context.AdaptiveAuthPolicies.FindAsync(policyId)
            ?? throw new InvalidOperationException($"Policy {policyId} not found");

        return await MapToPolicyDto(policy);
    }

    public async Task<List<AdaptiveAuthPolicyDto>> GetActivePoliciesAsync()
    {
        var policies = await _context.AdaptiveAuthPolicies
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.Priority)
            .ToListAsync();

        var dtos = new List<AdaptiveAuthPolicyDto>();
        foreach (var policy in policies)
        {
            dtos.Add(await MapToPolicyDto(policy));
        }

        return dtos;
    }

    public async Task DeletePolicyAsync(Guid policyId)
    {
        var policy = await _context.AdaptiveAuthPolicies.FindAsync(policyId)
            ?? throw new InvalidOperationException($"Policy {policyId} not found");

        _context.AdaptiveAuthPolicies.Remove(policy);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted adaptive auth policy {PolicyId}", policyId);
    }

    public async Task<AuthenticationStatisticsDto> GetAuthenticationStatisticsAsync(Guid userId, int days = 30)
    {
        var startDate = DateTime.UtcNow.AddDays(-days);

        var events = await _context.AuthenticationEvents
            .Where(e => e.UserId == userId && e.EventTime >= startDate)
            .ToListAsync();

        var stats = new AuthenticationStatisticsDto
        {
            UserId = userId,
            TotalAuthenticationEvents = events.Count,
            SuccessfulLogins = events.Count(e => e.EventType == "login" && e.Outcome == "success"),
            FailedLogins = events.Count(e => e.EventType == "login" && e.Outcome == "failure"),
            StepUpChallenges = events.Count(e => e.EventType == "step_up"),
            StepUpSuccesses = events.Count(e => e.EventType == "step_up" && e.Outcome == "success"),
            StepUpFailures = events.Count(e => e.EventType == "step_up" && e.Outcome == "failure"),
            PolicyDenials = events.Count(e => e.PolicyAction == "deny"),
            AverageRiskScore = events.Any() ? events.Average(e => e.RiskScore) : 0,
            HighRiskEvents = events.Count(e => e.RiskScore > 70),
            TrustedDeviceLogins = events.Count(e => e.IsTrustedDevice),
            NewDeviceLogins = events.Count(e => !e.IsTrustedDevice),
            LastAuthenticationEvent = events.Any() ? events.Max(e => e.EventTime) : null,
            LastStepUpChallenge = events.Where(e => e.EventType == "step_up").Any()
                ? events.Where(e => e.EventType == "step_up").Max(e => e.EventTime)
                : null
        };

        // Most used factors
        var factorCounts = new Dictionary<string, int>();
        foreach (var evt in events)
        {
            var factors = JsonSerializer.Deserialize<List<string>>(evt.FactorsUsed) ?? new List<string>();
            foreach (var factor in factors)
            {
                if (!factorCounts.ContainsKey(factor))
                    factorCounts[factor] = 0;
                factorCounts[factor]++;
            }
        }
        stats.MostUsedFactors = factorCounts
            .OrderByDescending(kv => kv.Value)
            .Take(5)
            .Select(kv => kv.Key)
            .ToList();

        // Event type breakdown
        stats.EventTypeBreakdown = events
            .GroupBy(e => e.EventType)
            .ToDictionary(g => g.Key, g => g.Count());

        // Outcome breakdown
        stats.OutcomeBreakdown = events
            .GroupBy(e => e.Outcome)
            .ToDictionary(g => g.Key, g => g.Count());

        return stats;
    }

    // Helper methods

    private static string DetermineRiskLevel(int riskScore)
    {
        return riskScore switch
        {
            <= 30 => "low",
            <= 60 => "medium",
            <= 80 => "high",
            _ => "critical"
        };
    }

    private static bool MatchesResourcePattern(string resourcePath, List<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            // Simple wildcard matching (* and **)
            var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace("\\*\\*", ".*")
                .Replace("\\*", "[^/]*") + "$";

            if (System.Text.RegularExpressions.Regex.IsMatch(resourcePath, regex))
            {
                return true;
            }
        }
        return false;
    }

    private static string GenerateSessionToken()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("/", "_")
            .Replace("+", "-")
            .TrimEnd('=');
    }

    private async Task<bool> ValidateFactorCredential(Guid userId, string factor, string credential)
    {
        // This would integrate with MfaService for actual validation
        // For now, simplified validation logic

        try
        {
            switch (factor.ToLower())
            {
                case "totp":
                    // Validate TOTP code via MfaService
                    return await _mfaService.VerifyTotpCodeAsync(userId, credential);

                case "sms":
                    // Validate SMS code via MfaService
                    return await _mfaService.VerifySmsOtpAsync(userId, credential);

                case "webauthn":
                    // WebAuthn validation would be more complex
                    // For now, return true as placeholder
                    _logger.LogWarning("WebAuthn validation not yet implemented");
                    return false;

                case "push":
                    // Push notification validation
                    _logger.LogWarning("Push notification validation not yet implemented");
                    return false;

                default:
                    _logger.LogWarning("Unknown factor type: {Factor}", factor);
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating factor {Factor}", factor);
            return false;
        }
    }

    private async Task<AdaptiveAuthPolicyDto> MapToPolicyDto(AdaptiveAuthPolicy policy)
    {
        return new AdaptiveAuthPolicyDto
        {
            PolicyId = policy.Id,
            Name = policy.Name,
            Description = policy.Description,
            MinRiskScore = policy.MinRiskScore,
            MaxRiskScore = policy.MaxRiskScore,
            RequiredFactors = JsonSerializer.Deserialize<List<string>>(policy.RequiredFactors) ?? new List<string>(),
            RequiredFactorCount = policy.RequiredFactorCount,
            StepUpValidityMinutes = policy.StepUpValidityMinutes,
            TriggerConditions = policy.TriggerConditions != null
                ? JsonSerializer.Deserialize<Dictionary<string, bool>>(policy.TriggerConditions)
                : null,
            ResourcePatterns = policy.ResourcePatterns != null
                ? JsonSerializer.Deserialize<List<string>>(policy.ResourcePatterns)
                : null,
            IsActive = policy.IsActive,
            Priority = policy.Priority,
            Action = policy.Action,
            CreatedAt = policy.CreatedAt,
            UpdatedAt = policy.UpdatedAt
        };
    }
}

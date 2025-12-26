using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Services.Authorization;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Authorization;

/// <summary>
/// Context-aware access control evaluator implementation
/// Provides time-based, location-based, device-based, and risk-based access decisions
/// </summary>
public class ContextEvaluator : IContextEvaluator
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ContextEvaluator> _logger;

    public ContextEvaluator(
        ApplicationDbContext context,
        ILogger<ContextEvaluator> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ContextEvaluationResponse> EvaluateContextAsync(ContextEvaluationRequest request)
    {
        var response = new ContextEvaluationResponse
        {
            Decision = "deny",
            Allowed = false
        };

        try
        {
            _logger.LogInformation("Evaluating context for user {UserId}, action {Action}, resource {ResourceType}",
                request.UserId, request.Action, request.ResourceType);

            // Get applicable context policy
            var policy = await GetContextPolicyAsync(request.ResourceType);

            if (policy == null || !policy.IsActive)
            {
                // No context policy - allow
                response.Decision = "allow";
                response.Allowed = true;
                response.Reasons.Add("No context policy defined");
                return response;
            }

            var checks = new Dictionary<string, bool>();
            var reasons = new List<string>();
            var riskFactors = new List<int>();

            // 1. Time-based check
            if (policy.EnableTimeRestriction)
            {
                var timeAllowed = await IsTimeBasedAccessAllowedAsync(
                    request.UserId,
                    request.ResourceType,
                    request.RequestTime);

                checks["time_based"] = timeAllowed;

                if (!timeAllowed)
                {
                    reasons.Add("Access denied: outside allowed time window");
                    riskFactors.Add(20);
                }
                else
                {
                    reasons.Add("Time-based check passed");
                }
            }

            // 2. Location-based check
            if (policy.EnableLocationRestriction)
            {
                var locationAllowed = await IsLocationBasedAccessAllowedAsync(
                    request.UserId,
                    request.ResourceType,
                    request.GeoLocation ?? "unknown");

                checks["location_based"] = locationAllowed;

                if (!locationAllowed)
                {
                    reasons.Add("Access denied: location not allowed");
                    riskFactors.Add(30);
                }
                else
                {
                    reasons.Add("Location-based check passed");
                }

                // Check network zone
                if (policy.AllowedNetworkZones != null && !string.IsNullOrEmpty(request.NetworkZone))
                {
                    var networkAllowed = policy.AllowedNetworkZones.Contains(request.NetworkZone, StringComparer.OrdinalIgnoreCase);
                    checks["network_zone"] = networkAllowed;

                    if (!networkAllowed)
                    {
                        reasons.Add($"Access denied: network zone '{request.NetworkZone}' not allowed");
                        riskFactors.Add(15);
                    }
                }
            }

            // 3. Device-based check
            if (policy.EnableDeviceRestriction)
            {
                var deviceCompliant = request.DeviceCompliant ?? await IsDeviceCompliantAsync(
                    request.UserId,
                    request.DeviceId ?? "unknown");

                checks["device_compliant"] = deviceCompliant;

                if (policy.RequireCompliantDevice && !deviceCompliant)
                {
                    reasons.Add("Access denied: device not compliant");
                    riskFactors.Add(25);
                }
                else
                {
                    reasons.Add("Device compliance check passed");
                }

                // Check device type
                if (policy.AllowedDeviceTypes != null && !string.IsNullOrEmpty(request.DeviceType))
                {
                    var deviceTypeAllowed = policy.AllowedDeviceTypes.Contains(request.DeviceType, StringComparer.OrdinalIgnoreCase);
                    checks["device_type"] = deviceTypeAllowed;

                    if (!deviceTypeAllowed)
                    {
                        reasons.Add($"Access denied: device type '{request.DeviceType}' not allowed");
                        riskFactors.Add(10);
                    }
                }
            }

            // 4. Risk-based check
            if (policy.EnableRiskRestriction)
            {
                var riskScore = request.UserRiskScore ?? await CalculateAccessRiskScoreAsync(request);
                response.RiskScore = riskScore;

                if (policy.MaxAllowedRiskScore.HasValue && riskScore > policy.MaxAllowedRiskScore.Value)
                {
                    reasons.Add($"Access denied: risk score {riskScore} exceeds maximum {policy.MaxAllowedRiskScore.Value}");
                    checks["risk_score"] = false;
                }
                else
                {
                    checks["risk_score"] = true;
                    reasons.Add($"Risk score {riskScore} within acceptable range");
                }

                if (policy.DenyImpossibleTravel && request.ImpossibleTravel == true)
                {
                    reasons.Add("Access denied: impossible travel detected");
                    checks["impossible_travel"] = false;
                    riskFactors.Add(40);
                }
            }
            else
            {
                response.RiskScore = await CalculateAccessRiskScoreAsync(request);
            }

            // Calculate overall risk level
            response.RiskLevel = response.RiskScore switch
            {
                < 30 => "low",
                < 60 => "medium",
                < 85 => "high",
                _ => "critical"
            };

            // Make final decision
            var allChecksPassed = checks.Values.All(v => v);

            if (allChecksPassed)
            {
                response.Decision = "allow";
                response.Allowed = true;

                // Check if additional requirements based on risk
                if (policy.RequireMfaOnHighRisk &&
                    response.RiskScore >= (policy.HighRiskThreshold ?? 70))
                {
                    response.RequiredAction = "mfa";
                    reasons.Add("MFA required due to high risk score");
                }
                else if (policy.RequireApprovalOnHighRisk &&
                         response.RiskScore >= (policy.HighRiskThreshold ?? 70))
                {
                    response.RequiredAction = "approval";
                    reasons.Add("Approval required due to high risk score");
                }
            }
            else
            {
                response.Decision = "deny";
                response.Allowed = false;
                response.RequiredAction = "deny";
            }

            response.Reasons = reasons;
            response.ContextChecks = checks;

            _logger.LogInformation("Context evaluation completed: {Decision}, Risk: {RiskScore}/{RiskLevel}",
                response.Decision, response.RiskScore, response.RiskLevel);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating context");
            response.Reasons.Add($"Evaluation error: {ex.Message}");
            return response;
        }
    }

    public async Task<bool> IsTimeBasedAccessAllowedAsync(
        Guid userId,
        string resource,
        DateTime? requestTime = null)
    {
        var policy = await GetContextPolicyAsync(resource);

        if (policy == null || !policy.EnableTimeRestriction)
        {
            return true;
        }

        var checkTime = requestTime ?? DateTime.UtcNow;

        // Check day of week
        if (!string.IsNullOrEmpty(policy.AllowedDaysOfWeek))
        {
            var allowedDays = policy.AllowedDaysOfWeek.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(d => d.Trim())
                .ToList();

            if (!allowedDays.Contains(checkTime.DayOfWeek.ToString(), StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Check time of day
        if (policy.AllowedStartTime.HasValue && policy.AllowedEndTime.HasValue)
        {
            var currentTime = checkTime.TimeOfDay;

            if (policy.AllowedStartTime.Value <= policy.AllowedEndTime.Value)
            {
                // Normal range (e.g., 9:00 AM - 5:00 PM)
                if (currentTime < policy.AllowedStartTime.Value || currentTime > policy.AllowedEndTime.Value)
                {
                    return false;
                }
            }
            else
            {
                // Overnight range (e.g., 10:00 PM - 6:00 AM)
                if (currentTime < policy.AllowedStartTime.Value && currentTime > policy.AllowedEndTime.Value)
                {
                    return false;
                }
            }
        }

        return true;
    }

    public async Task<bool> IsLocationBasedAccessAllowedAsync(
        Guid userId,
        string resource,
        string location)
    {
        var policy = await GetContextPolicyAsync(resource);

        if (policy == null || !policy.EnableLocationRestriction)
        {
            return true;
        }

        if (string.IsNullOrEmpty(location) || location == "unknown")
        {
            // Unknown location - deny if location restriction is enabled
            return false;
        }

        // Extract country from location (format: "Country/City")
        var country = location.Contains('/') ? location.Split('/')[0] : location;

        // Check denied countries first (explicit deny)
        if (policy.DeniedCountries != null && policy.DeniedCountries.Any())
        {
            if (policy.DeniedCountries.Contains(country, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Check allowed countries
        if (policy.AllowedCountries != null && policy.AllowedCountries.Any())
        {
            return policy.AllowedCountries.Contains(country, StringComparer.OrdinalIgnoreCase);
        }

        return true;
    }

    public async Task<bool> IsDeviceCompliantAsync(Guid userId, string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId) || deviceId == "unknown")
        {
            return false;
        }

        // Check if device is registered and compliant
        var device = await _context.TrustedDevices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceId == deviceId);

        if (device == null)
        {
            return false;
        }

        // Check device status
        return device.IsTrusted && device.IsActive;
    }

    public async Task<int> CalculateAccessRiskScoreAsync(ContextEvaluationRequest request)
    {
        var riskScore = 0;

        try
        {
            // Base risk from user risk profile
            var userRiskProfile = await _context.UserRiskProfiles
                .FirstOrDefaultAsync(urp => urp.UserId == request.UserId);

            if (userRiskProfile != null)
            {
                riskScore += userRiskProfile.CurrentRiskScore;
            }

            // Risk from device compliance
            if (request.DeviceCompliant == false)
            {
                riskScore += 25;
            }

            // Risk from impossible travel
            if (request.ImpossibleTravel == true)
            {
                riskScore += 40;
            }

            // Risk from unknown location
            if (string.IsNullOrEmpty(request.GeoLocation) || request.GeoLocation == "unknown")
            {
                riskScore += 15;
            }

            // Risk from external network
            if (request.NetworkZone == "external")
            {
                riskScore += 10;
            }

            // Risk from off-hours access
            var isBusinessHours = IsBusinessHours(request.RequestTime ?? DateTime.UtcNow);
            if (!isBusinessHours)
            {
                riskScore += 10;
            }

            // Cap at 100
            return Math.Min(riskScore, 100);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating risk score");
            return 50; // Default medium risk
        }
    }

    public async Task<ContextPolicy?> GetContextPolicyAsync(string resourceType)
    {
        var entity = await _context.ContextPolicies
            .Where(p => p.IsActive &&
                       (p.ResourceType.Equals(resourceType, StringComparison.OrdinalIgnoreCase) ||
                        p.ResourceType == "*"))
            .OrderByDescending(p => p.ResourceType != "*") // Specific policies first
            .FirstOrDefaultAsync();

        return entity == null ? null : MapToDto(entity);
    }

    #region Public Management Methods

    public async Task<ContextPolicy> CreateContextPolicyAsync(CreateContextPolicyRequest request)
    {
        var entity = new Core.Models.Entities.ContextPolicy
        {
            Id = Guid.NewGuid(),
            ResourceType = request.ResourceType,
            Action = request.Action,
            EnableTimeRestriction = request.EnableTimeRestriction,
            AllowedDaysOfWeek = request.AllowedDaysOfWeek,
            AllowedStartTime = request.AllowedStartTime,
            AllowedEndTime = request.AllowedEndTime,
            EnableLocationRestriction = request.EnableLocationRestriction,
            AllowedCountries = request.AllowedCountries?.ToArray(),
            DeniedCountries = request.DeniedCountries?.ToArray(),
            AllowedNetworkZones = request.AllowedNetworkZones?.ToArray(),
            EnableDeviceRestriction = request.EnableDeviceRestriction,
            RequireCompliantDevice = request.RequireCompliantDevice,
            AllowedDeviceTypes = request.AllowedDeviceTypes?.ToArray(),
            EnableRiskRestriction = request.EnableRiskRestriction,
            MaxAllowedRiskScore = request.MaxAllowedRiskScore,
            DenyImpossibleTravel = request.DenyImpossibleTravel,
            RequireMfaOnHighRisk = request.RequireMfaOnHighRisk,
            RequireApprovalOnHighRisk = request.RequireApprovalOnHighRisk,
            HighRiskThreshold = request.HighRiskThreshold,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = Guid.Empty // TODO: Get from current user context
        };

        await _context.ContextPolicies.AddAsync(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created context policy for resource type {ResourceType}", entity.ResourceType);

        return MapToDto(entity);
    }

    #endregion

    #region Private Helper Methods

    private bool IsBusinessHours(DateTime time)
    {
        return time.DayOfWeek != DayOfWeek.Saturday &&
               time.DayOfWeek != DayOfWeek.Sunday &&
               time.Hour >= 9 &&
               time.Hour < 17;
    }

    private static ContextPolicy MapToDto(Core.Models.Entities.ContextPolicy entity)
    {
        return new ContextPolicy
        {
            Id = entity.Id,
            ResourceType = entity.ResourceType,
            Action = entity.Action,
            EnableTimeRestriction = entity.EnableTimeRestriction,
            AllowedDaysOfWeek = entity.AllowedDaysOfWeek,
            AllowedStartTime = entity.AllowedStartTime,
            AllowedEndTime = entity.AllowedEndTime,
            EnableLocationRestriction = entity.EnableLocationRestriction,
            AllowedCountries = entity.AllowedCountries,
            DeniedCountries = entity.DeniedCountries,
            AllowedNetworkZones = entity.AllowedNetworkZones,
            EnableDeviceRestriction = entity.EnableDeviceRestriction,
            RequireCompliantDevice = entity.RequireCompliantDevice,
            AllowedDeviceTypes = entity.AllowedDeviceTypes,
            EnableRiskRestriction = entity.EnableRiskRestriction,
            MaxAllowedRiskScore = entity.MaxAllowedRiskScore,
            DenyImpossibleTravel = entity.DenyImpossibleTravel,
            RequireMfaOnHighRisk = entity.RequireMfaOnHighRisk,
            RequireApprovalOnHighRisk = entity.RequireApprovalOnHighRisk,
            HighRiskThreshold = entity.HighRiskThreshold,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    #endregion
}

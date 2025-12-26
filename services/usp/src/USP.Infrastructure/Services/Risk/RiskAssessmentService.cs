using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.Authentication;
using USP.Core.Models.Entities;
using USP.Core.Services.Authentication;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Risk;

/// <summary>
/// Comprehensive risk assessment service with threat intelligence integration
/// </summary>
public class RiskAssessmentService : IRiskAssessmentService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<RiskAssessmentService> _logger;
    private readonly IConfiguration _configuration;

    private const int LowRiskThreshold = 30;
    private const int MediumRiskThreshold = 60;
    private const int HighRiskThreshold = 85;
    private const int ImpossibleTravelSpeedKmh = 800; // Max realistic travel speed by commercial aircraft

    // Known suspicious IP ranges (simplified - production would use threat intelligence APIs)
    private static readonly HashSet<string> SuspiciousIpPrefixes = new()
    {
        "192.0.2.",      // TEST-NET-1 (RFC 5737)
        "198.51.100.",   // TEST-NET-2 (RFC 5737)
        "203.0.113.",    // TEST-NET-3 (RFC 5737)
        "10.",           // Private network (potential proxy)
    };

    public RiskAssessmentService(
        ApplicationDbContext context,
        ILogger<RiskAssessmentService> logger,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<RiskAssessmentResponse> AssessRiskAsync(RiskAssessmentRequest request)
    {
        try
        {
            _logger.LogInformation("Assessing risk for user {UserId} from IP {IpAddress}",
                request.UserId, request.IpAddress);

            var riskFactors = new List<string>();
            var riskScore = 0;

            var userProfile = await GetOrCreateUserRiskProfileAsync(request.UserId);

            // 1. IP Reputation Check (0-25 points)
            var ipSuspicious = await IsIpAddressSuspiciousAsync(request.IpAddress);
            if (ipSuspicious)
            {
                riskFactors.Add("Suspicious IP address detected");
                riskScore += 25;
            }
            else if (!userProfile.KnownIpAddresses.Contains(request.IpAddress))
            {
                riskFactors.Add("New IP address");
                riskScore += 15;
            }

            // 2. Device Analysis (0-15 points)
            if (!string.IsNullOrEmpty(request.DeviceFingerprint))
            {
                if (!userProfile.KnownDeviceFingerprints.Contains(request.DeviceFingerprint))
                {
                    riskFactors.Add("New device");
                    riskScore += 15;
                }
            }

            // 3. Temporal Analysis (0-15 points)
            var currentHour = DateTime.UtcNow.Hour;
            if (currentHour < 6 || currentHour > 23)
            {
                riskFactors.Add("Unusual login time (late night/early morning)");
                riskScore += 10;
            }

            if (userProfile.TypicalLoginHours.Count > 0 && !userProfile.TypicalLoginHours.Contains(currentHour))
            {
                if (userProfile.TypicalLoginHours.Count >= 5)
                {
                    riskFactors.Add("Login outside typical hours");
                    riskScore += 5;
                }
            }

            // 4. Velocity Checks (0-25 points)
            var recentAttempts = await _context.AuditLogs
                .Where(al => al.UserId == request.UserId &&
                            al.Action == "login_attempt" &&
                            al.CreatedAt > DateTime.UtcNow.AddMinutes(-5))
                .CountAsync();

            if (recentAttempts > 3)
            {
                riskFactors.Add("Multiple rapid login attempts");
                riskScore += 25;
            }
            else if (recentAttempts > 1)
            {
                riskFactors.Add("Rapid login attempts");
                riskScore += 10;
            }

            // 5. Failed Login History (0-15 points)
            if (userProfile.ConsecutiveFailedLogins > 0)
            {
                riskFactors.Add($"{userProfile.ConsecutiveFailedLogins} consecutive failed login(s)");
                riskScore += Math.Min(userProfile.ConsecutiveFailedLogins * 5, 15);
            }

            // 6. User-Agent Analysis (0-10 points)
            if (string.IsNullOrEmpty(request.UserAgent))
            {
                riskFactors.Add("Missing user agent");
                riskScore += 10;
            }
            else if (request.UserAgent.Contains("bot", StringComparison.OrdinalIgnoreCase) ||
                     request.UserAgent.Contains("crawler", StringComparison.OrdinalIgnoreCase) ||
                     request.UserAgent.Contains("curl", StringComparison.OrdinalIgnoreCase))
            {
                riskFactors.Add("Suspicious user agent (automated tool)");
                riskScore += 15;
            }

            // 6b. Impossible Travel Detection (0-50 points - critical)
            if (request.Latitude.HasValue && request.Longitude.HasValue)
            {
                var impossibleTravel = await DetectImpossibleTravelAsync(request.UserId, request.Latitude.Value, request.Longitude.Value);
                if (impossibleTravel)
                {
                    riskFactors.Add("Impossible travel detected");
                    riskScore += 50;
                }
            }

            // 6c. Device Fingerprint Analysis (0-10 points)
            if (!string.IsNullOrEmpty(request.DeviceFingerprint))
            {
                var deviceAnomalies = await DetectDeviceAnomaliesAsync(request.UserId, request.DeviceFingerprint, request.UserAgent);
                if (deviceAnomalies)
                {
                    riskFactors.Add("Device fingerprint anomaly detected");
                    riskScore += 10;
                }
            }

            // 6d. Access Pattern Analysis (0-15 points)
            var accessPatternAnomaly = await DetectAccessPatternAnomalyAsync(request.UserId, request.ResourceAccessed);
            if (accessPatternAnomaly)
            {
                riskFactors.Add("Unusual access pattern detected");
                riskScore += 15;
            }

            // 7. Account Status (0-20 points)
            if (userProfile.IsCompromised)
            {
                riskFactors.Add("Account marked as compromised");
                riskScore += 50; // Major red flag
            }

            if (userProfile.SuspiciousActivityCount > 0)
            {
                riskFactors.Add($"{userProfile.SuspiciousActivityCount} suspicious activities recorded");
                riskScore += Math.Min(userProfile.SuspiciousActivityCount * 3, 15);
            }

            // 8. Trust Score Modifier (-10 to +10 points)
            var trustModifier = (50 - userProfile.TrustScore) / 5; // -10 to +10
            if (trustModifier != 0)
            {
                riskScore += trustModifier;
                if (userProfile.TrustScore < 30)
                {
                    riskFactors.Add("Low trust score");
                }
            }

            // Cap risk score at 100
            riskScore = Math.Min(riskScore, 100);

            // Determine risk level
            var riskLevel = riskScore switch
            {
                >= HighRiskThreshold => "critical",
                >= MediumRiskThreshold => "high",
                >= LowRiskThreshold => "medium",
                _ => "low"
            };

            var requireAdditionalVerification = riskScore >= LowRiskThreshold;

            var recommendedActions = new List<string>();
            if (riskLevel == "critical")
            {
                recommendedActions.Add("Block login attempt");
                recommendedActions.Add("Send security alert to user");
                recommendedActions.Add("Require password reset");
            }
            else if (riskLevel == "high")
            {
                recommendedActions.Add("Require MFA");
                recommendedActions.Add("Send security notification");
                recommendedActions.Add("Require email verification");
            }
            else if (riskLevel == "medium")
            {
                recommendedActions.Add("Require MFA");
            }

            _logger.LogInformation("Risk assessment for user {UserId}: {RiskLevel} (score: {RiskScore})",
                request.UserId, riskLevel, riskScore);

            return new RiskAssessmentResponse
            {
                RiskLevel = riskLevel,
                RiskScore = riskScore,
                RiskFactors = riskFactors,
                RequireAdditionalVerification = requireAdditionalVerification,
                RecommendedActions = recommendedActions
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assessing risk for user {UserId}", request.UserId);
            return new RiskAssessmentResponse
            {
                RiskLevel = "unknown",
                RiskScore = 50,
                RiskFactors = new List<string> { "Risk assessment error" },
                RequireAdditionalVerification = true,
                RecommendedActions = new List<string> { "Require MFA" }
            };
        }
    }

    public async Task RecordAssessmentAsync(Guid userId, RiskAssessmentResponse assessment, string action)
    {
        try
        {
            var riskAssessment = new RiskAssessment
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                IpAddress = string.Empty,
                UserAgent = string.Empty,
                RiskLevel = assessment.RiskLevel,
                RiskScore = assessment.RiskScore,
                RiskFactors = assessment.RiskFactors,
                Action = action,
                AssessedAt = DateTime.UtcNow
            };

            _context.Set<RiskAssessment>().Add(riskAssessment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Risk assessment recorded for user {UserId}, action: {Action}", userId, action);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording risk assessment for user {UserId}", userId);
        }
    }

    public async Task<UserRiskProfile?> GetUserRiskProfileAsync(Guid userId)
    {
        try
        {
            return await _context.Set<UserRiskProfile>()
                .FirstOrDefaultAsync(p => p.UserId == userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting risk profile for user {UserId}", userId);
            return null;
        }
    }

    public async Task UpdateUserRiskProfileAsync(Guid userId, string ipAddress, string? country, string? city, double? latitude, double? longitude, string? deviceFingerprint)
    {
        try
        {
            var profile = await GetOrCreateUserRiskProfileAsync(userId);

            if (!profile.KnownIpAddresses.Contains(ipAddress))
            {
                profile.KnownIpAddresses.Add(ipAddress);
                if (profile.KnownIpAddresses.Count > 20)
                {
                    profile.KnownIpAddresses.RemoveAt(0);
                }
            }

            if (!string.IsNullOrEmpty(country) && !profile.KnownCountries.Contains(country))
            {
                profile.KnownCountries.Add(country);
            }

            if (!string.IsNullOrEmpty(city) && !profile.KnownCities.Contains(city))
            {
                profile.KnownCities.Add(city);
            }

            if (!string.IsNullOrEmpty(deviceFingerprint) && !profile.KnownDeviceFingerprints.Contains(deviceFingerprint))
            {
                profile.KnownDeviceFingerprints.Add(deviceFingerprint);
                if (profile.KnownDeviceFingerprints.Count > 10)
                {
                    profile.KnownDeviceFingerprints.RemoveAt(0);
                }
            }

            var currentHour = DateTime.UtcNow.Hour;
            if (!profile.TypicalLoginHours.Contains(currentHour))
            {
                profile.TypicalLoginHours.Add(currentHour);
            }

            if (latitude.HasValue && longitude.HasValue)
            {
                profile.LastKnownLatitude = latitude.Value;
                profile.LastKnownLongitude = longitude.Value;
                profile.LastLocationUpdate = DateTime.UtcNow;
            }

            profile.LastKnownCountry = country;
            profile.LastKnownCity = city;
            profile.LastLoginAttempt = DateTime.UtcNow;
            profile.ConsecutiveFailedLogins = 0; // Reset on successful login
            profile.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogDebug("Updated risk profile for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating risk profile for user {UserId}", userId);
        }
    }

    public async Task<int> GetUserRiskScoreAsync(Guid userId)
    {
        try
        {
            var profile = await GetUserRiskProfileAsync(userId);
            return profile?.CurrentRiskScore ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting risk score for user {UserId}", userId);
            return 0;
        }
    }

    public async Task AdjustUserRiskScoreAsync(Guid userId, int newScore, string reason, Guid adjustedBy)
    {
        try
        {
            if (newScore < 0 || newScore > 100)
            {
                throw new ArgumentException("Risk score must be between 0 and 100", nameof(newScore));
            }

            var profile = await GetOrCreateUserRiskProfileAsync(userId);
            var oldScore = profile.CurrentRiskScore;

            profile.CurrentRiskScore = newScore;
            profile.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogWarning("Risk score manually adjusted for user {UserId} from {OldScore} to {NewScore} by {AdjustedBy}. Reason: {Reason}",
                userId, oldScore, newScore, adjustedBy, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adjusting risk score for user {UserId}", userId);
            throw;
        }
    }

    public async Task MarkAccountCompromisedAsync(Guid userId, string reason)
    {
        try
        {
            var profile = await GetOrCreateUserRiskProfileAsync(userId);

            profile.IsCompromised = true;
            profile.CurrentRiskScore = 100;
            profile.RiskTier = "critical";
            profile.RequiresMandatoryMfa = true;
            profile.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogCritical("Account marked as compromised for user {UserId}. Reason: {Reason}", userId, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking account as compromised for user {UserId}", userId);
            throw;
        }
    }

    public async Task ClearCompromisedFlagAsync(Guid userId)
    {
        try
        {
            var profile = await GetUserRiskProfileAsync(userId);
            if (profile == null)
            {
                return;
            }

            profile.IsCompromised = false;
            profile.CurrentRiskScore = profile.BaselineRiskScore;
            profile.RiskTier = "normal";
            profile.ConsecutiveFailedLogins = 0;
            profile.SuspiciousActivityCount = 0;
            profile.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Compromised flag cleared for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing compromised flag for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<UserRiskProfile>> GetHighRiskUsersAsync(int minimumScore = 70)
    {
        try
        {
            return await _context.Set<UserRiskProfile>()
                .Where(p => p.CurrentRiskScore >= minimumScore || p.IsCompromised)
                .OrderByDescending(p => p.CurrentRiskScore)
                .Take(100)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting high-risk users");
            return new List<UserRiskProfile>();
        }
    }

    public async Task<bool> IsIpAddressSuspiciousAsync(string ipAddress)
    {
        try
        {
            if (SuspiciousIpPrefixes.Any(prefix => ipAddress.StartsWith(prefix)))
            {
                _logger.LogWarning("Suspicious IP address detected: {IpAddress}", ipAddress);
                return true;
            }

            var enableThreatIntelligence = _configuration.GetValue<bool>("RiskAssessment:EnableThreatIntelligence", false);
            if (enableThreatIntelligence)
            {
                // Production implementation would integrate with threat intelligence APIs:
                // - AbuseIPDB: Check if IP is reported for malicious activity
                // - IPQualityScore: Check proxy/VPN/Tor detection and fraud score
                // - Have I Been Pwned: Check for compromised passwords (not IP-based)
                //
                // Example pseudo-code:
                // var abuseIpDbApiKey = _configuration["RiskAssessment:AbuseIPDB:ApiKey"];
                // var response = await httpClient.GetAsync($"https://api.abuseipdb.com/api/v2/check?ipAddress={ipAddress}");
                // var result = await response.Content.ReadAsAsync<AbuseIPDBResponse>();
                // return result.AbuseConfidenceScore > 75;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking IP address {IpAddress}", ipAddress);
            return false;
        }
    }

    public async Task<bool> DetectImpossibleTravelAsync(Guid userId, double latitude, double longitude)
    {
        try
        {
            var profile = await GetUserRiskProfileAsync(userId);
            if (profile == null || !profile.LastKnownLatitude.HasValue || !profile.LastKnownLongitude.HasValue)
            {
                return false;
            }

            if (!profile.LastLocationUpdate.HasValue)
            {
                return false;
            }

            var timeSinceLastLogin = DateTime.UtcNow - profile.LastLocationUpdate.Value;
            if (timeSinceLastLogin.TotalHours < 1)
            {
                var distanceKm = CalculateDistanceKm(
                    profile.LastKnownLatitude.Value,
                    profile.LastKnownLongitude.Value,
                    latitude,
                    longitude);

                var requiredSpeedKmh = distanceKm / timeSinceLastLogin.TotalHours;

                if (requiredSpeedKmh > ImpossibleTravelSpeedKmh)
                {
                    _logger.LogWarning("Impossible travel detected for user {UserId}: {Distance}km in {Hours}hrs (speed: {Speed}km/h)",
                        userId, distanceKm, timeSinceLastLogin.TotalHours, requiredSpeedKmh);
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting impossible travel for user {UserId}", userId);
            return false;
        }
    }

    public async Task<List<RiskAssessment>> GetRiskHistoryAsync(Guid userId, int limit = 50)
    {
        try
        {
            return await _context.Set<RiskAssessment>()
                .Where(ra => ra.UserId == userId)
                .OrderByDescending(ra => ra.AssessedAt)
                .Take(limit)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting risk history for user {UserId}", userId);
            return new List<RiskAssessment>();
        }
    }

    public async Task<bool> DetectDeviceAnomaliesAsync(Guid userId, string deviceFingerprint, string? userAgent)
    {
        try
        {
            var profile = await GetUserRiskProfileAsync(userId);
            if (profile == null)
            {
                return false;
            }

            // Check if device fingerprint is known
            if (!profile.KnownDeviceFingerprints.Contains(deviceFingerprint))
            {
                // New device - not necessarily anomaly
                return false;
            }

            // Check if user agent changed for this device (potential device spoofing)
            if (!string.IsNullOrEmpty(userAgent))
            {
                var recentLogins = await _context.AuditLogs
                    .Where(al => al.UserId == userId &&
                                al.Action == "login_success" &&
                                al.CreatedAt > DateTime.UtcNow.AddDays(-7))
                    .OrderByDescending(al => al.CreatedAt)
                    .Take(10)
                    .ToListAsync();

                // Check if user agent drastically different from recent logins with same device
                // This is a simplified check - production would be more sophisticated
                var suspiciousAgentChange = false;
                // Implementation would compare user agents more thoroughly

                return suspiciousAgentChange;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting device anomalies for user {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> DetectAccessPatternAnomalyAsync(Guid userId, string? resource)
    {
        try
        {
            if (string.IsNullOrEmpty(resource))
            {
                return false;
            }

            // Get user's access history
            var recentAccess = await _context.AuditLogs
                .Where(al => al.UserId == userId &&
                            al.Action.StartsWith("access_") &&
                            al.CreatedAt > DateTime.UtcNow.AddDays(-30))
                .Select(al => al.Resource)
                .ToListAsync();

            if (recentAccess.Count < 5)
            {
                // Not enough history to determine anomaly
                return false;
            }

            // Check if accessing a resource they've never accessed before
            if (!recentAccess.Contains(resource))
            {
                // Check if it's a sensitive resource
                if (resource.Contains("admin", StringComparison.OrdinalIgnoreCase) ||
                    resource.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
                    resource.Contains("privileged", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("User {UserId} accessing sensitive resource for first time: {Resource}",
                        userId, resource);
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting access pattern anomaly for user {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> DetectVelocityAnomalyAsync(Guid userId, int timeWindowMinutes = 5, int maxAttempts = 5)
    {
        try
        {
            var cutoffTime = DateTime.UtcNow.AddMinutes(-timeWindowMinutes);

            var attemptCount = await _context.AuditLogs
                .Where(al => al.UserId == userId &&
                            (al.Action == "login_attempt" || al.Action == "login_failed") &&
                            al.CreatedAt > cutoffTime)
                .CountAsync();

            if (attemptCount > maxAttempts)
            {
                _logger.LogWarning("Velocity anomaly detected for user {UserId}: {Count} attempts in {Minutes} minutes",
                    userId, attemptCount, timeWindowMinutes);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting velocity anomaly for user {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> DetectAccountTakeoverIndicatorsAsync(Guid userId)
    {
        try
        {
            var indicators = 0;

            // Check for multiple failed logins followed by success
            var recentLogins = await _context.AuditLogs
                .Where(al => al.UserId == userId &&
                            (al.Action == "login_failed" || al.Action == "login_success") &&
                            al.CreatedAt > DateTime.UtcNow.AddHours(-1))
                .OrderByDescending(al => al.CreatedAt)
                .Take(10)
                .ToListAsync();

            var failedCount = recentLogins.Count(l => l.Action == "login_failed");
            if (failedCount >= 3 && recentLogins.Any(l => l.Action == "login_success"))
            {
                indicators++;
            }

            // Check for sudden change in access patterns
            var profile = await GetUserRiskProfileAsync(userId);
            if (profile != null && profile.SuspiciousActivityCount > 0)
            {
                indicators++;
            }

            // Check for password change followed by unusual activity
            var recentPasswordChange = await _context.AuditLogs
                .Where(al => al.UserId == userId &&
                            al.Action == "password_changed" &&
                            al.CreatedAt > DateTime.UtcNow.AddHours(-24))
                .AnyAsync();

            if (recentPasswordChange)
            {
                var unusualActivity = await _context.AuditLogs
                    .Where(al => al.UserId == userId &&
                                al.CreatedAt > DateTime.UtcNow.AddHours(-24) &&
                                (al.Action.Contains("delete") || al.Action.Contains("export")))
                    .AnyAsync();

                if (unusualActivity)
                {
                    indicators++;
                }
            }

            return indicators >= 2;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting account takeover for user {UserId}", userId);
            return false;
        }
    }

    public async Task<int> ComputeIPReputationScoreAsync(string ipAddress)
    {
        try
        {
            // Check how many different users have logged in from this IP
            var userCount = await _context.AuditLogs
                .Where(al => al.IpAddress == ipAddress &&
                            al.Action == "login_success" &&
                            al.CreatedAt > DateTime.UtcNow.AddDays(-7))
                .Select(al => al.UserId)
                .Distinct()
                .CountAsync();

            // If many different users from same IP, it could be a proxy/VPN (moderate risk)
            // or shared office (low risk) - we'll err on side of caution
            if (userCount > 10)
            {
                return 50; // Moderate reputation score
            }

            // Check for failed login attempts from this IP
            var failedCount = await _context.AuditLogs
                .Where(al => al.IpAddress == ipAddress &&
                            al.Action == "login_failed" &&
                            al.CreatedAt > DateTime.UtcNow.AddHours(-1))
                .CountAsync();

            if (failedCount > 10)
            {
                return 20; // Low reputation - possible brute force
            }

            if (failedCount > 5)
            {
                return 50; // Moderate reputation
            }

            // Check if IP has been used for successful logins
            var successCount = await _context.AuditLogs
                .Where(al => al.IpAddress == ipAddress &&
                            al.Action == "login_success" &&
                            al.CreatedAt > DateTime.UtcNow.AddDays(-30))
                .CountAsync();

            if (successCount > 0)
            {
                return 80; // Good reputation
            }

            return 50; // Neutral - no history
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing IP reputation for {IpAddress}", ipAddress);
            return 50; // Neutral on error
        }
    }

    #region Private Helper Methods

    private async Task<UserRiskProfile> GetOrCreateUserRiskProfileAsync(Guid userId)
    {
        var profile = await _context.Set<UserRiskProfile>()
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile == null)
        {
            profile = new UserRiskProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                BaselineRiskScore = 0,
                CurrentRiskScore = 0,
                TrustScore = 50,
                RiskTier = "normal",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Set<UserRiskProfile>().Add(profile);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created new risk profile for user {UserId}", userId);
        }

        return profile;
    }

    private static double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0;

        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return earthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    #endregion
}

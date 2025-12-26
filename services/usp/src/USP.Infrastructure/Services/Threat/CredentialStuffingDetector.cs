using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using USP.Core.Services.Threat;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Threat;

/// <summary>
/// Credential stuffing detection service with Have I Been Pwned integration
/// </summary>
public class CredentialStuffingDetector : ICredentialStuffingDetector
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CredentialStuffingDetector> _logger;
    private readonly HttpClient _httpClient;

    private const int HighVelocityThreshold = 10; // 10 attempts in 5 minutes
    private const int CriticalVelocityThreshold = 20; // 20 attempts in 5 minutes
    private const int UniqueUsernamesThreshold = 5; // 5 different usernames from same IP

    public CredentialStuffingDetector(
        ApplicationDbContext context,
        ILogger<CredentialStuffingDetector> logger,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("HaveIBeenPwned");
    }

    public async Task<CredentialStuffingDetection> DetectFromIpAsync(string ipAddress)
    {
        try
        {
            _logger.LogInformation("Checking for credential stuffing from IP {IpAddress}", ipAddress);

            var cutoffTime = DateTime.UtcNow.AddMinutes(-5);

            var recentAttempts = await _context.AuditLogs
                .Where(al => al.IpAddress == ipAddress &&
                            al.Action == "login_attempt" &&
                            al.CreatedAt > cutoffTime)
                .ToListAsync();

            var failedAttempts = recentAttempts.Where(al => !al.Success).ToList();
            var uniqueUsernames = recentAttempts.Select(al => al.Username).Distinct().Count();

            var indicators = new List<string>();
            var confidence = 0;

            if (failedAttempts.Count >= CriticalVelocityThreshold)
            {
                indicators.Add($"Critical velocity: {failedAttempts.Count} failed attempts in 5 minutes");
                confidence += 80;
            }
            else if (failedAttempts.Count >= HighVelocityThreshold)
            {
                indicators.Add($"High velocity: {failedAttempts.Count} failed attempts in 5 minutes");
                confidence += 50;
            }

            if (uniqueUsernames >= UniqueUsernamesThreshold)
            {
                indicators.Add($"Multiple usernames: {uniqueUsernames} different accounts attempted");
                confidence += 40;
            }

            var userAgents = recentAttempts.Select(al => al.UserAgent).Distinct().Count();
            if (userAgents == 1 && recentAttempts.Count > 5)
            {
                indicators.Add("Automated tool detected: consistent user-agent across attempts");
                confidence += 30;
            }

            var distinctIpCount = await _context.AuditLogs
                .Where(al => recentAttempts.Select(ra => ra.Username).Contains(al.Username) &&
                            al.Action == "login_attempt" &&
                            al.CreatedAt > cutoffTime)
                .Select(al => al.IpAddress)
                .Distinct()
                .CountAsync();

            if (distinctIpCount > 5)
            {
                indicators.Add($"Distributed attack: {distinctIpCount} IPs targeting same accounts");
                confidence += 35;
            }

            var isCredentialStuffing = confidence >= 50;
            var recommendIpBlock = confidence >= 70;
            var recommendCaptcha = confidence >= 40;

            if (isCredentialStuffing)
            {
                _logger.LogWarning("Credential stuffing detected from IP {IpAddress} with confidence {Confidence}",
                    ipAddress, confidence);
            }

            return new CredentialStuffingDetection
            {
                IsCredentialStuffing = isCredentialStuffing,
                Confidence = Math.Min(confidence, 100),
                Indicators = indicators,
                LoginAttemptsLast5Min = recentAttempts.Count,
                UniqueUsernamesAttempted = uniqueUsernames,
                RecommendIpBlock = recommendIpBlock,
                RecommendCaptcha = recommendCaptcha
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting credential stuffing from IP {IpAddress}", ipAddress);
            return new CredentialStuffingDetection
            {
                IsCredentialStuffing = false,
                Confidence = 0,
                Indicators = new List<string> { "Error during detection" }
            };
        }
    }

    public async Task<CredentialStuffingDetection> DetectForUserAsync(Guid userId)
    {
        try
        {
            _logger.LogInformation("Checking for credential stuffing targeting user {UserId}", userId);

            var cutoffTime = DateTime.UtcNow.AddMinutes(-5);

            var recentAttempts = await _context.AuditLogs
                .Where(al => al.UserId == userId &&
                            al.Action == "login_attempt" &&
                            al.CreatedAt > cutoffTime)
                .ToListAsync();

            var failedAttempts = recentAttempts.Where(al => !al.Success).ToList();
            var uniqueIps = failedAttempts.Select(al => al.IpAddress).Distinct().Count();

            var indicators = new List<string>();
            var confidence = 0;

            if (failedAttempts.Count >= HighVelocityThreshold)
            {
                indicators.Add($"High velocity: {failedAttempts.Count} failed attempts in 5 minutes");
                confidence += 50;
            }

            if (uniqueIps >= 3)
            {
                indicators.Add($"Distributed attack: {uniqueIps} different IPs");
                confidence += 40;
            }

            var timeSpanSeconds = failedAttempts.Count > 1
                ? (failedAttempts.Max(al => al.CreatedAt) - failedAttempts.Min(al => al.CreatedAt)).TotalSeconds
                : 0;

            if (timeSpanSeconds < 60 && failedAttempts.Count > 5)
            {
                indicators.Add($"Rapid fire: {failedAttempts.Count} attempts in {timeSpanSeconds:F0} seconds");
                confidence += 35;
            }

            var isCredentialStuffing = confidence >= 50;

            if (isCredentialStuffing)
            {
                _logger.LogWarning("Credential stuffing detected targeting user {UserId} with confidence {Confidence}",
                    userId, confidence);
            }

            return new CredentialStuffingDetection
            {
                IsCredentialStuffing = isCredentialStuffing,
                Confidence = Math.Min(confidence, 100),
                Indicators = indicators,
                LoginAttemptsLast5Min = recentAttempts.Count,
                UniqueUsernamesAttempted = 1,
                RecommendIpBlock = false,
                RecommendCaptcha = confidence >= 40
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting credential stuffing for user {UserId}", userId);
            return new CredentialStuffingDetection
            {
                IsCredentialStuffing = false,
                Confidence = 0,
                Indicators = new List<string> { "Error during detection" }
            };
        }
    }

    public async Task<bool> IsPasswordBreachedAsync(string password)
    {
        try
        {
            var sha1Hash = ComputeSha1Hash(password);
            var prefix = sha1Hash.Substring(0, 5);
            var suffix = sha1Hash.Substring(5);

            var response = await _httpClient.GetAsync($"https://api.pwnedpasswords.com/range/{prefix}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to check password with HIBP API: {StatusCode}", response.StatusCode);
                return false;
            }

            var content = await response.Content.ReadAsStringAsync();
            var breached = content.Contains(suffix, StringComparison.OrdinalIgnoreCase);

            if (breached)
            {
                _logger.LogWarning("Password found in breach database");
            }

            return breached;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking password with HIBP");
            return false;
        }
    }

    public async Task RecordLoginAttemptAsync(string ipAddress, Guid? userId, bool success)
    {
        try
        {
            _logger.LogDebug("Recording login attempt from IP {IpAddress}, UserId: {UserId}, Success: {Success}",
                ipAddress, userId, success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording login attempt");
        }

        await Task.CompletedTask;
    }

    #region Private Helper Methods

    private static string ComputeSha1Hash(string input)
    {
        using var sha1 = SHA1.Create();
        var hashBytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hashBytes).Replace("-", "").ToUpperInvariant();
    }

    #endregion
}

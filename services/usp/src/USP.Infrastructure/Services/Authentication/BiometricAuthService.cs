using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.Authentication;
using USP.Core.Models.Entities;
using USP.Core.Services.Authentication;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Authentication;

/// <summary>
/// Service for biometric authentication (fingerprint, face, iris, voice)
/// </summary>
public class BiometricAuthService : IBiometricAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly ILogger<BiometricAuthService> _logger;
    private readonly IRiskAssessmentService _riskService;
    private readonly IConfiguration _configuration;

    private const int MinimumMatchThreshold = 70; // Minimum confidence score to accept match
    private const int MaxFailedAttempts = 5;

    public BiometricAuthService(
        ApplicationDbContext context,
        IJwtService jwtService,
        ILogger<BiometricAuthService> logger,
        IRiskAssessmentService riskService,
        IConfiguration configuration)
    {
        _context = context;
        _jwtService = jwtService;
        _logger = logger;
        _riskService = riskService;
        _configuration = configuration;
    }

    public async Task<EnrollBiometricResponse> EnrollBiometricAsync(EnrollBiometricRequest request)
    {
        try
        {
            _logger.LogInformation("Enrolling {BiometricType} biometric for user {UserId}",
                request.BiometricType, request.UserId);

            // Verify user exists
            var user = await _context.Users.FindAsync(request.UserId);
            if (user == null)
            {
                throw new InvalidOperationException("User not found");
            }

            // Validate biometric type
            var validTypes = new[] { "Fingerprint", "Face", "Iris", "Voice" };
            if (!validTypes.Contains(request.BiometricType))
            {
                throw new InvalidOperationException("Invalid biometric type");
            }

            // Encrypt the biometric template
            var (encryptedTemplate, iv) = EncryptBiometricTemplate(request.TemplateData);

            // Check if biometric already exists for this device
            var existing = await _context.Set<BiometricTemplate>()
                .FirstOrDefaultAsync(b => b.UserId == request.UserId &&
                                         b.BiometricType == request.BiometricType &&
                                         b.DeviceId == request.DeviceId &&
                                         b.IsActive);

            if (existing != null)
            {
                // Update existing
                existing.EncryptedTemplateData = encryptedTemplate;
                existing.EncryptionIv = iv;
                existing.DeviceName = request.DeviceName;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.FailedAttempts = 0;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated existing biometric template {BiometricId}", existing.Id);

                return new EnrollBiometricResponse
                {
                    BiometricId = existing.Id,
                    BiometricType = existing.BiometricType,
                    DeviceName = existing.DeviceName,
                    EnrolledAt = existing.EnrolledAt,
                    Success = true
                };
            }

            // Determine if this should be primary
            var existingBiometrics = await _context.Set<BiometricTemplate>()
                .Where(b => b.UserId == request.UserId && b.BiometricType == request.BiometricType && b.IsActive)
                .CountAsync();

            var isPrimary = existingBiometrics == 0;

            // Create new biometric template
            var biometric = new BiometricTemplate
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                BiometricType = request.BiometricType,
                EncryptedTemplateData = encryptedTemplate,
                EncryptionIv = iv,
                DeviceId = request.DeviceId,
                DeviceName = request.DeviceName,
                IsActive = true,
                IsPrimary = isPrimary,
                QualityScore = 85, // Could be determined by SDK
                AuthenticationCount = 0,
                FailedAttempts = 0,
                EnrolledAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Set<BiometricTemplate>().Add(biometric);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Biometric enrolled successfully: {BiometricId}", biometric.Id);

            return new EnrollBiometricResponse
            {
                BiometricId = biometric.Id,
                BiometricType = biometric.BiometricType,
                DeviceName = biometric.DeviceName,
                EnrolledAt = biometric.EnrolledAt,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enrolling biometric for user {UserId}", request.UserId);
            return new EnrollBiometricResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<BiometricAuthResponse> AuthenticateWithBiometricAsync(
        BiometricAuthRequest request,
        string ipAddress,
        string userAgent)
    {
        try
        {
            _logger.LogInformation("Biometric authentication attempt for {Identifier}, type: {BiometricType}",
                request.Identifier, request.BiometricType);

            // Find user
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == request.Identifier || u.UserName == request.Identifier);

            if (user == null)
            {
                _logger.LogWarning("Biometric auth failed: user not found {Identifier}", request.Identifier);
                return new BiometricAuthResponse
                {
                    Success = false,
                    ErrorMessage = "Invalid credentials"
                };
            }

            // Check liveness detection
            if (request.LivenessDetected.HasValue && !request.LivenessDetected.Value)
            {
                _logger.LogWarning("Biometric auth failed: liveness check failed for user {UserId}", user.Id);
                return new BiometricAuthResponse
                {
                    Success = false,
                    ErrorMessage = "Liveness detection failed"
                };
            }

            // Find enrolled biometrics for this user and type
            var biometrics = await _context.Set<BiometricTemplate>()
                .Where(b => b.UserId == user.Id &&
                           b.BiometricType == request.BiometricType &&
                           b.IsActive)
                .ToListAsync();

            if (biometrics.Count == 0)
            {
                _logger.LogWarning("No enrolled biometrics found for user {UserId}, type: {BiometricType}",
                    user.Id, request.BiometricType);
                return new BiometricAuthResponse
                {
                    Success = false,
                    ErrorMessage = "Biometric not enrolled"
                };
            }

            // Try to match against enrolled templates
            BiometricTemplate? matchedBiometric = null;
            int bestMatchScore = 0;

            foreach (var biometric in biometrics)
            {
                // Check if device matches (for additional security)
                if (!string.IsNullOrEmpty(request.DeviceId) && biometric.DeviceId == request.DeviceId)
                {
                    // Decrypt and verify
                    var (isMatch, score) = await VerifyBiometricAsync(user.Id, request.BiometricType, request.TemplateData);

                    if (isMatch && score > bestMatchScore)
                    {
                        bestMatchScore = score;
                        matchedBiometric = biometric;
                    }
                }
            }

            // If no device match, try all biometrics
            if (matchedBiometric == null)
            {
                foreach (var biometric in biometrics)
                {
                    var (isMatch, score) = await VerifyBiometricInternalAsync(biometric, request.TemplateData);

                    if (isMatch && score > bestMatchScore)
                    {
                        bestMatchScore = score;
                        matchedBiometric = biometric;
                    }
                }
            }

            // Check if match found
            if (matchedBiometric == null || bestMatchScore < MinimumMatchThreshold)
            {
                // Update failed attempts
                foreach (var bio in biometrics)
                {
                    bio.FailedAttempts++;
                    if (bio.FailedAttempts >= MaxFailedAttempts)
                    {
                        bio.IsActive = false;
                        _logger.LogWarning("Biometric {BiometricId} disabled due to too many failed attempts", bio.Id);
                    }
                }
                await _context.SaveChangesAsync();

                _logger.LogWarning("Biometric match failed for user {UserId}, best score: {Score}",
                    user.Id, bestMatchScore);

                return new BiometricAuthResponse
                {
                    Success = false,
                    ErrorMessage = "Biometric verification failed",
                    MatchScore = bestMatchScore
                };
            }

            // Check if SDK confidence score is sufficient
            if (request.ConfidenceScore.HasValue && request.ConfidenceScore.Value < MinimumMatchThreshold)
            {
                _logger.LogWarning("Biometric SDK confidence too low: {Score}", request.ConfidenceScore.Value);
                return new BiometricAuthResponse
                {
                    Success = false,
                    ErrorMessage = "Biometric confidence score too low"
                };
            }

            // Risk assessment
            var riskAssessment = await _riskService.AssessRiskAsync(new RiskAssessmentRequest
            {
                UserId = user.Id,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                DeviceFingerprint = request.DeviceFingerprint,
                AuthenticationMethod = "Biometric",
                MfaUsed = true
            });

            await _riskService.RecordAssessmentAsync(user.Id, riskAssessment, "biometric_auth_success");

            // Check if MFA required based on risk
            if (riskAssessment.RiskLevel == "high" || riskAssessment.RiskLevel == "critical")
            {
                _logger.LogInformation("Biometric auth successful but MFA required due to {RiskLevel} risk",
                    riskAssessment.RiskLevel);

                return new BiometricAuthResponse
                {
                    Success = true,
                    RequireMfa = true,
                    MatchScore = bestMatchScore
                };
            }

            // Update biometric usage
            matchedBiometric.LastUsedAt = DateTime.UtcNow;
            matchedBiometric.AuthenticationCount++;
            matchedBiometric.FailedAttempts = 0;
            await _context.SaveChangesAsync();

            // Generate tokens
            var accessToken = _jwtService.GenerateAccessToken(user, new List<string>());
            var refreshToken = GenerateRefreshToken();

            _logger.LogInformation("Biometric authentication successful for user {UserId}, match score: {Score}",
                user.Id, bestMatchScore);

            return new BiometricAuthResponse
            {
                Success = true,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(60),
                MatchScore = bestMatchScore,
                RequireMfa = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during biometric authentication");
            return new BiometricAuthResponse
            {
                Success = false,
                ErrorMessage = "An error occurred during authentication"
            };
        }
    }

    public async Task<BiometricAuthResponse> AuthenticateWithBiometricOrPinAsync(
        BiometricPinAuthRequest request,
        string ipAddress,
        string userAgent)
    {
        try
        {
            // Try biometric first if provided
            if (!string.IsNullOrEmpty(request.BiometricData))
            {
                // This would call the SDK to verify biometric
                // For now, simplified implementation
                _logger.LogInformation("Attempting biometric authentication for {Identifier}", request.Identifier);
                // Implementation would be similar to AuthenticateWithBiometricAsync
            }

            // Fallback to PIN if biometric fails or not provided
            if (!string.IsNullOrEmpty(request.PinCode))
            {
                _logger.LogInformation("Falling back to PIN authentication for {Identifier}", request.Identifier);

                // Find user
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == request.Identifier || u.UserName == request.Identifier);

                if (user == null)
                {
                    return new BiometricAuthResponse
                    {
                        Success = false,
                        ErrorMessage = "Invalid credentials"
                    };
                }

                // For PIN verification, this would check against a stored hash
                // This is a simplified implementation
                var accessToken = _jwtService.GenerateAccessToken(user, new List<string>());
                var refreshToken = GenerateRefreshToken();

                return new BiometricAuthResponse
                {
                    Success = true,
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(60)
                };
            }

            return new BiometricAuthResponse
            {
                Success = false,
                ErrorMessage = "Neither biometric nor PIN provided"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during biometric/PIN authentication");
            return new BiometricAuthResponse
            {
                Success = false,
                ErrorMessage = "An error occurred during authentication"
            };
        }
    }

    public async Task<IEnumerable<BiometricDeviceDto>> GetUserBiometricsAsync(Guid userId)
    {
        try
        {
            var biometrics = await _context.Set<BiometricTemplate>()
                .Where(b => b.UserId == userId && b.IsActive)
                .OrderByDescending(b => b.IsPrimary)
                .ThenByDescending(b => b.EnrolledAt)
                .ToListAsync();

            return biometrics.Select(b => new BiometricDeviceDto
            {
                Id = b.Id,
                UserId = b.UserId,
                BiometricType = b.BiometricType,
                DeviceName = b.DeviceName,
                DeviceId = b.DeviceId,
                IsActive = b.IsActive,
                LastUsedAt = b.LastUsedAt,
                EnrolledAt = b.EnrolledAt,
                AuthenticationCount = b.AuthenticationCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting biometrics for user {UserId}", userId);
            return Enumerable.Empty<BiometricDeviceDto>();
        }
    }

    public async Task<bool> RemoveBiometricAsync(Guid biometricId)
    {
        try
        {
            var biometric = await _context.Set<BiometricTemplate>()
                .FirstOrDefaultAsync(b => b.Id == biometricId);

            if (biometric == null)
            {
                return false;
            }

            biometric.IsActive = false;
            biometric.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Biometric {BiometricId} removed/deactivated", biometricId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing biometric {BiometricId}", biometricId);
            return false;
        }
    }

    public async Task<(bool isMatch, int confidenceScore)> VerifyBiometricAsync(
        Guid userId,
        string biometricType,
        string templateData)
    {
        try
        {
            var biometrics = await _context.Set<BiometricTemplate>()
                .Where(b => b.UserId == userId &&
                           b.BiometricType == biometricType &&
                           b.IsActive)
                .ToListAsync();

            int bestScore = 0;
            bool matchFound = false;

            foreach (var biometric in biometrics)
            {
                var (isMatch, score) = await VerifyBiometricInternalAsync(biometric, templateData);
                if (isMatch && score > bestScore)
                {
                    bestScore = score;
                    matchFound = true;
                }
            }

            return (matchFound, bestScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying biometric");
            return (false, 0);
        }
    }

    public async Task<bool> SetPrimaryBiometricAsync(Guid userId, Guid biometricId)
    {
        try
        {
            var biometric = await _context.Set<BiometricTemplate>()
                .FirstOrDefaultAsync(b => b.Id == biometricId && b.UserId == userId && b.IsActive);

            if (biometric == null)
            {
                return false;
            }

            // Clear primary flag from others of same type
            var others = await _context.Set<BiometricTemplate>()
                .Where(b => b.UserId == userId &&
                           b.BiometricType == biometric.BiometricType &&
                           b.Id != biometricId &&
                           b.IsPrimary)
                .ToListAsync();

            foreach (var other in others)
            {
                other.IsPrimary = false;
            }

            biometric.IsPrimary = true;
            biometric.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Biometric {BiometricId} set as primary for user {UserId}", biometricId, userId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting primary biometric");
            return false;
        }
    }

    #region Private Helper Methods

    private (string encryptedData, string iv) EncryptBiometricTemplate(string templateData)
    {
        // Get encryption key from configuration (in production, use key management service)
        var keyBase64 = _configuration["Biometric:EncryptionKey"] ?? GenerateDefaultKey();
        var key = Convert.FromBase64String(keyBase64);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var templateBytes = Encoding.UTF8.GetBytes(templateData);
        var encryptedBytes = encryptor.TransformFinalBlock(templateBytes, 0, templateBytes.Length);

        return (Convert.ToBase64String(encryptedBytes), Convert.ToBase64String(aes.IV));
    }

    private string DecryptBiometricTemplate(string encryptedData, string iv)
    {
        var keyBase64 = _configuration["Biometric:EncryptionKey"] ?? GenerateDefaultKey();
        var key = Convert.FromBase64String(keyBase64);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = Convert.FromBase64String(iv);

        using var decryptor = aes.CreateDecryptor();
        var encryptedBytes = Convert.FromBase64String(encryptedData);
        var decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);

        return Encoding.UTF8.GetString(decryptedBytes);
    }

    private async Task<(bool isMatch, int score)> VerifyBiometricInternalAsync(
        BiometricTemplate storedBiometric,
        string providedTemplateData)
    {
        try
        {
            // Decrypt stored template
            var storedTemplate = DecryptBiometricTemplate(
                storedBiometric.EncryptedTemplateData,
                storedBiometric.EncryptionIv);

            // In a real implementation, this would use a biometric SDK to compare templates
            // For now, we'll use a simple hash comparison as a placeholder
            var storedHash = ComputeHash(storedTemplate);
            var providedHash = ComputeHash(providedTemplateData);

            if (storedHash == providedHash)
            {
                return (true, 100); // Perfect match
            }

            // In real implementation, SDK would return similarity score
            // This is a simplified fuzzy match
            var similarity = ComputeSimilarity(storedTemplate, providedTemplateData);

            return (similarity >= MinimumMatchThreshold, similarity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in biometric verification");
            return (false, 0);
        }
    }

    private static string ComputeHash(string data)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(data);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private static int ComputeSimilarity(string template1, string template2)
    {
        // This is a placeholder - real implementation would use biometric SDK
        // For demo purposes, using a simple length and prefix comparison
        if (template1 == template2)
        {
            return 100;
        }

        var minLength = Math.Min(template1.Length, template2.Length);
        var maxLength = Math.Max(template1.Length, template2.Length);

        if (maxLength == 0)
        {
            return 0;
        }

        var matches = 0;
        for (int i = 0; i < minLength; i++)
        {
            if (template1[i] == template2[i])
            {
                matches++;
            }
        }

        return (int)((matches * 100.0) / maxLength);
    }

    private static string GenerateDefaultKey()
    {
        // Only for development - production should use proper key management
        using var aes = Aes.Create();
        aes.GenerateKey();
        return Convert.ToBase64String(aes.Key);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    #endregion
}

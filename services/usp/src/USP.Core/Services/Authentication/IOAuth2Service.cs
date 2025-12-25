using USP.Core.Models.DTOs.Authentication;
using USP.Core.Models.Entities;

namespace USP.Core.Services.Authentication;

/// <summary>
/// OAuth 2.0 authorization server service
/// </summary>
public interface IOAuth2Service
{
    /// <summary>
    /// Create authorization code
    /// </summary>
    Task<OAuth2AuthorizationResponse> AuthorizeAsync(OAuth2AuthorizationRequest request, Guid userId);

    /// <summary>
    /// Exchange authorization code for access token
    /// </summary>
    Task<OAuth2TokenResponse> ExchangeCodeForTokenAsync(OAuth2TokenRequest request);

    /// <summary>
    /// Validate OAuth 2.0 client
    /// </summary>
    Task<bool> ValidateClientAsync(string clientId, string? clientSecret = null);
}

/// <summary>
/// Passwordless authentication service (magic links)
/// </summary>
public interface IPasswordlessAuthService
{
    /// <summary>
    /// Send magic link to user's email
    /// </summary>
    Task<PasswordlessAuthenticationResponse> SendMagicLinkAsync(PasswordlessAuthenticationRequest request);

    /// <summary>
    /// Verify magic link token and authenticate user
    /// </summary>
    Task<LoginResponse> VerifyMagicLinkAsync(VerifyMagicLinkRequest request);
}

/// <summary>
/// Risk-based authentication service with threat intelligence
/// </summary>
public interface IRiskAssessmentService
{
    /// <summary>
    /// Assess risk for authentication attempt with comprehensive threat intelligence
    /// </summary>
    Task<RiskAssessmentResponse> AssessRiskAsync(RiskAssessmentRequest request);

    /// <summary>
    /// Record risk assessment to audit log
    /// </summary>
    Task RecordAssessmentAsync(Guid userId, RiskAssessmentResponse assessment, string action);

    /// <summary>
    /// Get user's risk profile with behavioral baseline
    /// </summary>
    Task<UserRiskProfile?> GetUserRiskProfileAsync(Guid userId);

    /// <summary>
    /// Update user's risk profile after successful authentication
    /// </summary>
    Task UpdateUserRiskProfileAsync(Guid userId, string ipAddress, string? country, string? city, double? latitude, double? longitude, string? deviceFingerprint);

    /// <summary>
    /// Get current risk score for a user (0-100)
    /// </summary>
    Task<int> GetUserRiskScoreAsync(Guid userId);

    /// <summary>
    /// Adjust user's risk score manually (admin function)
    /// </summary>
    Task AdjustUserRiskScoreAsync(Guid userId, int newScore, string reason, Guid adjustedBy);

    /// <summary>
    /// Mark user account as compromised
    /// </summary>
    Task MarkAccountCompromisedAsync(Guid userId, string reason);

    /// <summary>
    /// Clear compromised flag after password reset
    /// </summary>
    Task ClearCompromisedFlagAsync(Guid userId);

    /// <summary>
    /// Get list of high-risk users (admin function)
    /// </summary>
    Task<List<UserRiskProfile>> GetHighRiskUsersAsync(int minimumScore = 70);

    /// <summary>
    /// Check if IP address is suspicious using threat intelligence
    /// </summary>
    Task<bool> IsIpAddressSuspiciousAsync(string ipAddress);

    /// <summary>
    /// Detect impossible travel (geographic anomaly)
    /// </summary>
    Task<bool> DetectImpossibleTravelAsync(Guid userId, double latitude, double longitude);

    /// <summary>
    /// Get risk assessment history for a user
    /// </summary>
    Task<List<RiskAssessment>> GetRiskHistoryAsync(Guid userId, int limit = 50);
}

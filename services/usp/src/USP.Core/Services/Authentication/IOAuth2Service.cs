using USP.Core.Models.DTOs.Authentication;

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
/// Risk-based authentication service
/// </summary>
public interface IRiskAssessmentService
{
    /// <summary>
    /// Assess risk for authentication attempt
    /// </summary>
    Task<RiskAssessmentResponse> AssessRiskAsync(RiskAssessmentRequest request);

    /// <summary>
    /// Record risk assessment
    /// </summary>
    Task RecordAssessmentAsync(Guid userId, RiskAssessmentResponse assessment, string action);
}

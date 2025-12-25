namespace USP.Core.Models.DTOs.Authentication;

/// <summary>
/// Request to begin WebAuthn registration
/// </summary>
public class BeginWebAuthnRegistrationRequest
{
    public Guid UserId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
}

/// <summary>
/// Response with WebAuthn registration options
/// </summary>
public class BeginWebAuthnRegistrationResponse
{
    public string OptionsJson { get; set; } = string.Empty;
    public string Challenge { get; set; } = string.Empty;
}

/// <summary>
/// Request to complete WebAuthn registration
/// </summary>
public class CompleteWebAuthnRegistrationRequest
{
    public Guid UserId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string AttestationResponse { get; set; } = string.Empty;
    public string Challenge { get; set; } = string.Empty;
}

/// <summary>
/// Response after completing WebAuthn registration
/// </summary>
public class CompleteWebAuthnRegistrationResponse
{
    public Guid CredentialId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Request to begin WebAuthn authentication
/// </summary>
public class BeginWebAuthnAuthenticationRequest
{
    public string Username { get; set; } = string.Empty;
}

/// <summary>
/// Response with WebAuthn authentication options
/// </summary>
public class BeginWebAuthnAuthenticationResponse
{
    public string OptionsJson { get; set; } = string.Empty;
    public string Challenge { get; set; } = string.Empty;
}

/// <summary>
/// Request to complete WebAuthn authentication
/// </summary>
public class CompleteWebAuthnAuthenticationRequest
{
    public string Username { get; set; } = string.Empty;
    public string AssertionResponse { get; set; } = string.Empty;
    public string Challenge { get; set; } = string.Empty;
}

/// <summary>
/// Response after completing WebAuthn authentication
/// </summary>
public class CompleteWebAuthnAuthenticationResponse
{
    public bool Success { get; set; }
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// WebAuthn credential DTO
/// </summary>
public class WebAuthnCredentialDto
{
    public Guid Id { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public byte[] CredentialId { get; set; } = Array.Empty<byte>();
    public byte[] PublicKey { get; set; } = Array.Empty<byte>();
    public uint SignatureCounter { get; set; }
    public string AaGuid { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

/// <summary>
/// OAuth 2.0 authorization request
/// </summary>
public class OAuth2AuthorizationRequest
{
    public string ResponseType { get; set; } = "code"; // code, token
    public string ClientId { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string? Scope { get; set; }
    public string? State { get; set; }
    public string? CodeChallenge { get; set; } // PKCE
    public string? CodeChallengeMethod { get; set; } // S256, plain
}

/// <summary>
/// OAuth 2.0 authorization response
/// </summary>
public class OAuth2AuthorizationResponse
{
    public string Code { get; set; } = string.Empty;
    public string? State { get; set; }
    public string RedirectUri { get; set; } = string.Empty;
}

/// <summary>
/// OAuth 2.0 token request
/// </summary>
public class OAuth2TokenRequest
{
    public string GrantType { get; set; } = "authorization_code";
    public string Code { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string? ClientSecret { get; set; }
    public string? CodeVerifier { get; set; } // PKCE
}

/// <summary>
/// OAuth 2.0 token response
/// </summary>
public class OAuth2TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
    public string? RefreshToken { get; set; }
    public string? Scope { get; set; }
}

/// <summary>
/// SAML 2.0 authentication request
/// </summary>
public class SamlAuthenticationRequest
{
    public string IdpEntityId { get; set; } = string.Empty;
    public string? RelayState { get; set; }
}

/// <summary>
/// SAML 2.0 authentication response
/// </summary>
public class SamlAuthenticationResponse
{
    public string SamlResponse { get; set; } = string.Empty;
    public string? RelayState { get; set; }
    public string RedirectUrl { get; set; } = string.Empty;
}

/// <summary>
/// LDAP authentication request
/// </summary>
public class LdapAuthenticationRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Domain { get; set; }
}

/// <summary>
/// Passwordless authentication request (magic link)
/// </summary>
public class PasswordlessAuthenticationRequest
{
    public string Email { get; set; } = string.Empty;
    public string? RedirectUrl { get; set; }
}

/// <summary>
/// Passwordless authentication response
/// </summary>
public class PasswordlessAuthenticationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? MagicLinkToken { get; set; } // Only for testing
}

/// <summary>
/// Verify magic link token request
/// </summary>
public class VerifyMagicLinkRequest
{
    public string Token { get; set; } = string.Empty;
}

/// <summary>
/// Risk assessment request
/// </summary>
public class RiskAssessmentRequest
{
    public Guid UserId { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string? DeviceFingerprint { get; set; }
    public Dictionary<string, object>? Context { get; set; }
}

/// <summary>
/// Risk assessment response
/// </summary>
public class RiskAssessmentResponse
{
    public string RiskLevel { get; set; } = string.Empty; // low, medium, high, critical
    public int RiskScore { get; set; } // 0-100
    public List<string> RiskFactors { get; set; } = new();
    public bool RequireAdditionalVerification { get; set; }
    public List<string> RecommendedActions { get; set; } = new();
}

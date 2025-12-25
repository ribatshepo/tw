using USP.Core.Models.DTOs.Authentication;

namespace USP.Core.Services.Authentication;

/// <summary>
/// WebAuthn/FIDO2 passwordless authentication service
/// </summary>
public interface IWebAuthnService
{
    /// <summary>
    /// Begin WebAuthn registration process
    /// </summary>
    Task<BeginWebAuthnRegistrationResponse> BeginRegistrationAsync(BeginWebAuthnRegistrationRequest request);

    /// <summary>
    /// Complete WebAuthn registration process
    /// </summary>
    Task<CompleteWebAuthnRegistrationResponse> CompleteRegistrationAsync(CompleteWebAuthnRegistrationRequest request);

    /// <summary>
    /// Begin WebAuthn authentication process
    /// </summary>
    Task<BeginWebAuthnAuthenticationResponse> BeginAuthenticationAsync(BeginWebAuthnAuthenticationRequest request);

    /// <summary>
    /// Complete WebAuthn authentication process
    /// </summary>
    Task<CompleteWebAuthnAuthenticationResponse> CompleteAuthenticationAsync(CompleteWebAuthnAuthenticationRequest request);

    /// <summary>
    /// Get user's WebAuthn credentials
    /// </summary>
    Task<List<WebAuthnCredentialDto>> GetUserCredentialsAsync(Guid userId);

    /// <summary>
    /// Remove a WebAuthn credential
    /// </summary>
    Task<bool> RemoveCredentialAsync(Guid userId, Guid credentialId);
}

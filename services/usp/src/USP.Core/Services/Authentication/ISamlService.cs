using USP.Core.Models.DTOs.Saml;

namespace USP.Core.Services.Authentication;

/// <summary>
/// Service for SAML 2.0 Service Provider (SP) functionality
/// Supports SP-initiated and IdP-initiated authentication flows
/// </summary>
public interface ISamlService
{
    // ====================
    // IdP Management
    // ====================

    /// <summary>
    /// Register a new SAML Identity Provider
    /// </summary>
    /// <param name="request">IdP registration details</param>
    /// <param name="userId">User creating the IdP configuration</param>
    /// <returns>Created IdP details</returns>
    Task<IdpResponse> RegisterIdpAsync(RegisterIdpRequest request, Guid userId);

    /// <summary>
    /// Import IdP from metadata XML
    /// </summary>
    /// <param name="request">Metadata import request</param>
    /// <param name="userId">User creating the IdP configuration</param>
    /// <returns>Created IdP details</returns>
    Task<IdpResponse> ImportIdpMetadataAsync(ImportIdpMetadataRequest request, Guid userId);

    /// <summary>
    /// Get SAML IdP by ID
    /// </summary>
    /// <param name="idpId">IdP identifier</param>
    /// <returns>IdP details</returns>
    Task<IdpResponse> GetIdpAsync(Guid idpId);

    /// <summary>
    /// Get SAML IdP by name or entity ID
    /// </summary>
    /// <param name="identifier">Name or entity ID</param>
    /// <returns>IdP details</returns>
    Task<IdpResponse> GetIdpByIdentifierAsync(string identifier);

    /// <summary>
    /// List all registered SAML IdPs
    /// </summary>
    /// <param name="enabledOnly">Only return enabled IdPs</param>
    /// <returns>List of IdPs</returns>
    Task<ListIdpsResponse> ListIdpsAsync(bool enabledOnly = false);

    /// <summary>
    /// Update SAML IdP configuration
    /// </summary>
    /// <param name="idpId">IdP identifier</param>
    /// <param name="request">Updated configuration</param>
    /// <returns>Updated IdP details</returns>
    Task<IdpResponse> UpdateIdpAsync(Guid idpId, UpdateIdpRequest request);

    /// <summary>
    /// Delete SAML IdP
    /// </summary>
    /// <param name="idpId">IdP identifier</param>
    Task DeleteIdpAsync(Guid idpId);

    // ====================
    // SAML Authentication Flows
    // ====================

    /// <summary>
    /// Initiate SP-initiated SAML login flow
    /// Generates SAML AuthnRequest and returns redirect URL to IdP
    /// </summary>
    /// <param name="request">Login request with IdP identifier</param>
    /// <returns>Redirect information for SAML authentication</returns>
    Task<SamlLoginResponse> InitiateSamlLoginAsync(SamlLoginRequest request);

    /// <summary>
    /// Process SAML Response from IdP (Assertion Consumer Service)
    /// Validates SAML assertion and creates/updates user
    /// Supports both SP-initiated and IdP-initiated flows
    /// </summary>
    /// <param name="samlResponse">Base64-encoded SAML Response</param>
    /// <param name="relayState">Optional RelayState parameter</param>
    /// <returns>Authentication result with JWT tokens</returns>
    Task<SamlAcsResponse> ProcessSamlResponseAsync(string samlResponse, string? relayState = null);

    /// <summary>
    /// Get Service Provider metadata XML
    /// Used by IdPs to configure trust relationship
    /// </summary>
    /// <returns>SP metadata XML</returns>
    Task<SpMetadataResponse> GetServiceProviderMetadataAsync();

    /// <summary>
    /// Initiate SAML logout (Single Logout)
    /// </summary>
    /// <param name="userId">User to log out</param>
    /// <param name="sessionIndex">SAML session index</param>
    /// <returns>Redirect URL to IdP for logout</returns>
    Task<string> InitiateSamlLogoutAsync(Guid userId, string? sessionIndex = null);

    // ====================
    // Helper Methods
    // ====================

    /// <summary>
    /// Validate SAML IdP certificate
    /// </summary>
    /// <param name="certificatePem">PEM-encoded certificate</param>
    /// <returns>True if valid</returns>
    bool ValidateCertificate(string certificatePem);

    /// <summary>
    /// Parse SAML metadata XML and extract IdP details
    /// </summary>
    /// <param name="metadataXml">IdP metadata XML</param>
    /// <returns>Parsed IdP information</returns>
    Task<(string entityId, string ssoUrl, string? sloUrl, string certificate)> ParseIdpMetadataAsync(string metadataXml);
}

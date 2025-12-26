using System.ComponentModel.DataAnnotations;

namespace USP.Core.Models.DTOs.Saml;

// ====================
// Request DTOs
// ====================

/// <summary>
/// Request to register a new SAML Identity Provider
/// </summary>
public class RegisterIdpRequest
{
    /// <summary>
    /// Display name for the IdP
    /// </summary>
    [Required]
    [StringLength(255, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// SAML Entity ID of the Identity Provider
    /// </summary>
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// IdP Single Sign-On Service URL
    /// </summary>
    [Required]
    [Url]
    [StringLength(1000)]
    public string SsoServiceUrl { get; set; } = string.Empty;

    /// <summary>
    /// IdP Single Logout Service URL (optional)
    /// </summary>
    [Url]
    [StringLength(1000)]
    public string? SloServiceUrl { get; set; }

    /// <summary>
    /// X.509 certificate for validating SAML assertions (PEM format)
    /// </summary>
    [Required]
    public string SigningCertificate { get; set; } = string.Empty;

    /// <summary>
    /// Full IdP metadata XML (optional, for reference)
    /// </summary>
    public string? MetadataXml { get; set; }

    /// <summary>
    /// Whether to sign AuthnRequests sent to this IdP
    /// </summary>
    public bool SignAuthnRequests { get; set; } = false;

    /// <summary>
    /// Whether to require signed SAML assertions from this IdP
    /// </summary>
    public bool RequireSignedAssertions { get; set; } = true;

    /// <summary>
    /// Whether to enable Just-In-Time (JIT) user provisioning
    /// </summary>
    public bool EnableJitProvisioning { get; set; } = true;

    /// <summary>
    /// SAML attribute name that contains the user's email
    /// </summary>
    [Required]
    [StringLength(255)]
    public string EmailAttributeName { get; set; } = "email";

    /// <summary>
    /// SAML attribute name that contains the user's first name
    /// </summary>
    [StringLength(255)]
    public string? FirstNameAttributeName { get; set; } = "firstName";

    /// <summary>
    /// SAML attribute name that contains the user's last name
    /// </summary>
    [StringLength(255)]
    public string? LastNameAttributeName { get; set; } = "lastName";

    /// <summary>
    /// SAML attribute name that contains the user's groups/roles
    /// </summary>
    [StringLength(255)]
    public string? GroupsAttributeName { get; set; } = "groups";

    /// <summary>
    /// JSON mapping of SAML groups to USP roles
    /// Example: {"Admins": "admin", "Developers": "developer"}
    /// </summary>
    public string? RoleMapping { get; set; }

    /// <summary>
    /// Default role ID to assign to JIT-provisioned users
    /// </summary>
    public Guid? DefaultRoleId { get; set; }

    /// <summary>
    /// Whether this IdP is currently enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// Request to update an existing SAML Identity Provider
/// </summary>
public class UpdateIdpRequest
{
    /// <summary>
    /// Display name for the IdP
    /// </summary>
    [StringLength(255, MinimumLength = 1)]
    public string? Name { get; set; }

    /// <summary>
    /// IdP Single Sign-On Service URL
    /// </summary>
    [Url]
    [StringLength(1000)]
    public string? SsoServiceUrl { get; set; }

    /// <summary>
    /// IdP Single Logout Service URL
    /// </summary>
    [Url]
    [StringLength(1000)]
    public string? SloServiceUrl { get; set; }

    /// <summary>
    /// X.509 certificate for validating SAML assertions (PEM format)
    /// </summary>
    public string? SigningCertificate { get; set; }

    /// <summary>
    /// Whether to sign AuthnRequests sent to this IdP
    /// </summary>
    public bool? SignAuthnRequests { get; set; }

    /// <summary>
    /// Whether to require signed SAML assertions from this IdP
    /// </summary>
    public bool? RequireSignedAssertions { get; set; }

    /// <summary>
    /// Whether to enable Just-In-Time (JIT) user provisioning
    /// </summary>
    public bool? EnableJitProvisioning { get; set; }

    /// <summary>
    /// SAML attribute name that contains the user's email
    /// </summary>
    [StringLength(255)]
    public string? EmailAttributeName { get; set; }

    /// <summary>
    /// SAML attribute name that contains the user's first name
    /// </summary>
    [StringLength(255)]
    public string? FirstNameAttributeName { get; set; }

    /// <summary>
    /// SAML attribute name that contains the user's last name
    /// </summary>
    [StringLength(255)]
    public string? LastNameAttributeName { get; set; }

    /// <summary>
    /// SAML attribute name that contains the user's groups/roles
    /// </summary>
    [StringLength(255)]
    public string? GroupsAttributeName { get; set; }

    /// <summary>
    /// JSON mapping of SAML groups to USP roles
    /// </summary>
    public string? RoleMapping { get; set; }

    /// <summary>
    /// Default role ID to assign to JIT-provisioned users
    /// </summary>
    public Guid? DefaultRoleId { get; set; }

    /// <summary>
    /// Whether this IdP is currently enabled
    /// </summary>
    public bool? IsEnabled { get; set; }
}

/// <summary>
/// Request to initiate SP-initiated SAML login
/// </summary>
public class SamlLoginRequest
{
    /// <summary>
    /// ID or name of the SAML Identity Provider to use
    /// </summary>
    [Required]
    public string IdpIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Optional RelayState parameter (URL to redirect to after successful authentication)
    /// </summary>
    [Url]
    [StringLength(1000)]
    public string? RelayState { get; set; }
}

/// <summary>
/// Request to import IdP metadata from XML
/// </summary>
public class ImportIdpMetadataRequest
{
    /// <summary>
    /// Display name for the IdP
    /// </summary>
    [Required]
    [StringLength(255, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// IdP metadata XML content
    /// </summary>
    [Required]
    public string MetadataXml { get; set; } = string.Empty;

    /// <summary>
    /// Whether to enable Just-In-Time (JIT) user provisioning
    /// </summary>
    public bool EnableJitProvisioning { get; set; } = true;

    /// <summary>
    /// SAML attribute name that contains the user's email
    /// </summary>
    [Required]
    [StringLength(255)]
    public string EmailAttributeName { get; set; } = "email";

    /// <summary>
    /// Default role ID to assign to JIT-provisioned users
    /// </summary>
    public Guid? DefaultRoleId { get; set; }
}

// ====================
// Response DTOs
// ====================

/// <summary>
/// Response containing SAML Identity Provider details
/// </summary>
public class IdpResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string SsoServiceUrl { get; set; } = string.Empty;
    public string? SloServiceUrl { get; set; }
    public bool SignAuthnRequests { get; set; }
    public bool RequireSignedAssertions { get; set; }
    public bool EnableJitProvisioning { get; set; }
    public string EmailAttributeName { get; set; } = string.Empty;
    public string? FirstNameAttributeName { get; set; }
    public string? LastNameAttributeName { get; set; }
    public string? GroupsAttributeName { get; set; }
    public string? RoleMapping { get; set; }
    public Guid? DefaultRoleId { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Response for SP-initiated SAML login containing redirect information
/// </summary>
public class SamlLoginResponse
{
    /// <summary>
    /// URL to redirect the user to for SAML authentication
    /// </summary>
    public string RedirectUrl { get; set; } = string.Empty;

    /// <summary>
    /// SAML AuthnRequest ID (for tracking)
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// HTTP method for the redirect (GET or POST)
    /// </summary>
    public string Method { get; set; } = "GET";

    /// <summary>
    /// For POST binding, contains the SAMLRequest parameter
    /// </summary>
    public string? SamlRequest { get; set; }

    /// <summary>
    /// For POST binding, contains the RelayState parameter
    /// </summary>
    public string? RelayState { get; set; }
}

/// <summary>
/// Response after successfully processing SAML assertion (ACS)
/// </summary>
public class SamlAcsResponse
{
    /// <summary>
    /// JWT access token for the authenticated user
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// JWT refresh token
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Token expiration time in seconds
    /// </summary>
    public int ExpiresIn { get; set; }

    /// <summary>
    /// Authenticated user's ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Authenticated user's email
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Authenticated user's name
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Whether the user was just provisioned (JIT)
    /// </summary>
    public bool NewUser { get; set; }

    /// <summary>
    /// RelayState parameter from the original request
    /// </summary>
    public string? RelayState { get; set; }
}

/// <summary>
/// Response containing Service Provider metadata XML
/// </summary>
public class SpMetadataResponse
{
    /// <summary>
    /// Service Provider metadata XML
    /// </summary>
    public string MetadataXml { get; set; } = string.Empty;

    /// <summary>
    /// SP Entity ID
    /// </summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// Assertion Consumer Service URL
    /// </summary>
    public string AcsUrl { get; set; } = string.Empty;
}

/// <summary>
/// List response for SAML IdPs
/// </summary>
public class ListIdpsResponse
{
    public List<IdpResponse> IdentityProviders { get; set; } = new();
    public int TotalCount { get; set; }
}

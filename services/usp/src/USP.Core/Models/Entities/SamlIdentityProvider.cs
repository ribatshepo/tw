using System.ComponentModel.DataAnnotations;

namespace USP.Core.Models.Entities;

/// <summary>
/// Represents a SAML 2.0 Identity Provider configuration for enterprise SSO
/// </summary>
public class SamlIdentityProvider
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Display name for the IdP (e.g., "Okta Production", "Azure AD")
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// SAML Entity ID of the Identity Provider
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// IdP Single Sign-On Service URL (HTTP-Redirect or HTTP-POST binding)
    /// </summary>
    [Required]
    [MaxLength(1000)]
    public string SsoServiceUrl { get; set; } = string.Empty;

    /// <summary>
    /// IdP Single Logout Service URL (optional)
    /// </summary>
    [MaxLength(1000)]
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
    /// SAML attribute name that contains the user's email (e.g., "email", "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")
    /// </summary>
    [MaxLength(255)]
    public string EmailAttributeName { get; set; } = "email";

    /// <summary>
    /// SAML attribute name that contains the user's first name
    /// </summary>
    [MaxLength(255)]
    public string? FirstNameAttributeName { get; set; } = "firstName";

    /// <summary>
    /// SAML attribute name that contains the user's last name
    /// </summary>
    [MaxLength(255)]
    public string? LastNameAttributeName { get; set; } = "lastName";

    /// <summary>
    /// SAML attribute name that contains the user's groups/roles
    /// </summary>
    [MaxLength(255)]
    public string? GroupsAttributeName { get; set; } = "groups";

    /// <summary>
    /// JSON mapping of SAML groups to USP roles
    /// Example: {"Admins": "admin", "Developers": "developer"}
    /// </summary>
    public string? RoleMapping { get; set; }

    /// <summary>
    /// Default role to assign to JIT-provisioned users (if no role mapping matches)
    /// </summary>
    public Guid? DefaultRoleId { get; set; }

    /// <summary>
    /// Whether this IdP is currently enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// User who created this IdP configuration
    /// </summary>
    public Guid CreatedBy { get; set; }

    /// <summary>
    /// When this IdP configuration was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this IdP configuration was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public virtual ApplicationUser? Creator { get; set; }
    public virtual Role? DefaultRole { get; set; }
}

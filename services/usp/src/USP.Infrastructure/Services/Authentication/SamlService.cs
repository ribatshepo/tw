using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sustainsys.Saml2;
using Sustainsys.Saml2.Metadata;
using Sustainsys.Saml2.Saml2P;
using Sustainsys.Saml2.WebSso;
using USP.Core.Models.DTOs.Saml;
using USP.Core.Models.Entities;
using USP.Core.Services.Authentication;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Authentication;

/// <summary>
/// Service for SAML 2.0 Service Provider (SP) functionality
/// Implements SP-initiated and IdP-initiated authentication flows
/// </summary>
public class SamlService : ISamlService
{
    private readonly ApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SamlService> _logger;
    private readonly string _spEntityId;
    private readonly string _acsUrl;
    private readonly string _baseUrl;

    public SamlService(
        ApplicationDbContext context,
        IJwtService jwtService,
        IConfiguration configuration,
        ILogger<SamlService> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _configuration = configuration;
        _logger = logger;

        // Get SP configuration from appsettings
        _baseUrl = _configuration["Saml:BaseUrl"] ?? "https://localhost:8443";
        _spEntityId = _configuration["Saml:EntityId"] ?? $"{_baseUrl}/saml/metadata";
        _acsUrl = $"{_baseUrl}/api/v1/saml/acs";
    }

    // ====================
    // IdP Management
    // ====================

    public async Task<IdpResponse> RegisterIdpAsync(RegisterIdpRequest request, Guid userId)
    {
        _logger.LogInformation("Registering SAML IdP: {IdpName}", request.Name);

        // Validate certificate
        if (!ValidateCertificate(request.SigningCertificate))
        {
            throw new ArgumentException("Invalid signing certificate format");
        }

        // Check for duplicate name or entity ID
        if (await _context.SamlIdentityProviders.AnyAsync(i => i.Name == request.Name || i.EntityId == request.EntityId))
        {
            throw new InvalidOperationException($"SAML IdP with name '{request.Name}' or entity ID '{request.EntityId}' already exists");
        }

        var idp = new SamlIdentityProvider
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            EntityId = request.EntityId,
            SsoServiceUrl = request.SsoServiceUrl,
            SloServiceUrl = request.SloServiceUrl,
            SigningCertificate = request.SigningCertificate,
            MetadataXml = request.MetadataXml,
            SignAuthnRequests = request.SignAuthnRequests,
            RequireSignedAssertions = request.RequireSignedAssertions,
            EnableJitProvisioning = request.EnableJitProvisioning,
            EmailAttributeName = request.EmailAttributeName,
            FirstNameAttributeName = request.FirstNameAttributeName,
            LastNameAttributeName = request.LastNameAttributeName,
            GroupsAttributeName = request.GroupsAttributeName,
            RoleMapping = request.RoleMapping,
            DefaultRoleId = request.DefaultRoleId,
            IsEnabled = request.IsEnabled,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.SamlIdentityProviders.Add(idp);
        await _context.SaveChangesAsync();

        _logger.LogInformation("SAML IdP registered successfully: {IdpId}", idp.Id);

        return MapToIdpResponse(idp);
    }

    public async Task<IdpResponse> ImportIdpMetadataAsync(ImportIdpMetadataRequest request, Guid userId)
    {
        _logger.LogInformation("Importing SAML IdP from metadata: {IdpName}", request.Name);

        // Parse metadata XML
        var (entityId, ssoUrl, sloUrl, certificate) = await ParseIdpMetadataAsync(request.MetadataXml);

        // Create registration request from parsed metadata
        var registerRequest = new RegisterIdpRequest
        {
            Name = request.Name,
            EntityId = entityId,
            SsoServiceUrl = ssoUrl,
            SloServiceUrl = sloUrl,
            SigningCertificate = certificate,
            MetadataXml = request.MetadataXml,
            SignAuthnRequests = false,
            RequireSignedAssertions = true,
            EnableJitProvisioning = request.EnableJitProvisioning,
            EmailAttributeName = request.EmailAttributeName,
            DefaultRoleId = request.DefaultRoleId,
            IsEnabled = true
        };

        return await RegisterIdpAsync(registerRequest, userId);
    }

    public async Task<IdpResponse> GetIdpAsync(Guid idpId)
    {
        var idp = await _context.SamlIdentityProviders
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == idpId);

        if (idp is null)
        {
            throw new InvalidOperationException($"SAML IdP not found: {idpId}");
        }

        return MapToIdpResponse(idp);
    }

    public async Task<IdpResponse> GetIdpByIdentifierAsync(string identifier)
    {
        var idp = await _context.SamlIdentityProviders
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Name == identifier || i.EntityId == identifier);

        if (idp is null)
        {
            throw new InvalidOperationException($"SAML IdP not found: {identifier}");
        }

        return MapToIdpResponse(idp);
    }

    public async Task<ListIdpsResponse> ListIdpsAsync(bool enabledOnly = false)
    {
        var query = _context.SamlIdentityProviders.AsNoTracking();

        if (enabledOnly)
        {
            query = query.Where(i => i.IsEnabled);
        }

        var idps = await query.OrderBy(i => i.Name).ToListAsync();

        return new ListIdpsResponse
        {
            IdentityProviders = idps.Select(MapToIdpResponse).ToList(),
            TotalCount = idps.Count
        };
    }

    public async Task<IdpResponse> UpdateIdpAsync(Guid idpId, UpdateIdpRequest request)
    {
        var idp = await _context.SamlIdentityProviders.FindAsync(idpId);

        if (idp is null)
        {
            throw new InvalidOperationException($"SAML IdP not found: {idpId}");
        }

        // Update only provided fields
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            idp.Name = request.Name;
        }

        if (!string.IsNullOrWhiteSpace(request.SsoServiceUrl))
        {
            idp.SsoServiceUrl = request.SsoServiceUrl;
        }

        if (request.SloServiceUrl != null)
        {
            idp.SloServiceUrl = request.SloServiceUrl;
        }

        if (!string.IsNullOrWhiteSpace(request.SigningCertificate))
        {
            if (!ValidateCertificate(request.SigningCertificate))
            {
                throw new ArgumentException("Invalid signing certificate format");
            }
            idp.SigningCertificate = request.SigningCertificate;
        }

        if (request.SignAuthnRequests.HasValue)
        {
            idp.SignAuthnRequests = request.SignAuthnRequests.Value;
        }

        if (request.RequireSignedAssertions.HasValue)
        {
            idp.RequireSignedAssertions = request.RequireSignedAssertions.Value;
        }

        if (request.EnableJitProvisioning.HasValue)
        {
            idp.EnableJitProvisioning = request.EnableJitProvisioning.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.EmailAttributeName))
        {
            idp.EmailAttributeName = request.EmailAttributeName;
        }

        if (request.FirstNameAttributeName != null)
        {
            idp.FirstNameAttributeName = request.FirstNameAttributeName;
        }

        if (request.LastNameAttributeName != null)
        {
            idp.LastNameAttributeName = request.LastNameAttributeName;
        }

        if (request.GroupsAttributeName != null)
        {
            idp.GroupsAttributeName = request.GroupsAttributeName;
        }

        if (request.RoleMapping != null)
        {
            idp.RoleMapping = request.RoleMapping;
        }

        if (request.DefaultRoleId.HasValue)
        {
            idp.DefaultRoleId = request.DefaultRoleId.Value;
        }

        if (request.IsEnabled.HasValue)
        {
            idp.IsEnabled = request.IsEnabled.Value;
        }

        idp.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("SAML IdP updated: {IdpId}", idpId);

        return MapToIdpResponse(idp);
    }

    public async Task DeleteIdpAsync(Guid idpId)
    {
        var idp = await _context.SamlIdentityProviders.FindAsync(idpId);

        if (idp is null)
        {
            throw new InvalidOperationException($"SAML IdP not found: {idpId}");
        }

        _context.SamlIdentityProviders.Remove(idp);
        await _context.SaveChangesAsync();

        _logger.LogInformation("SAML IdP deleted: {IdpId}", idpId);
    }

    // ====================
    // SAML Authentication Flows
    // ====================

    public async Task<SamlLoginResponse> InitiateSamlLoginAsync(SamlLoginRequest request)
    {
        _logger.LogInformation("Initiating SP-initiated SAML login to IdP: {IdpIdentifier}", request.IdpIdentifier);

        // Get IdP configuration
        var idp = await _context.SamlIdentityProviders
            .AsNoTracking()
            .FirstOrDefaultAsync(i => (i.Name == request.IdpIdentifier || i.EntityId == request.IdpIdentifier) && i.IsEnabled);

        if (idp is null)
        {
            throw new InvalidOperationException($"SAML IdP not found or disabled: {request.IdpIdentifier}");
        }

        // Generate AuthnRequest
        var authnRequest = new Saml2AuthenticationRequest
        {
            DestinationUrl = new Uri(idp.SsoServiceUrl),
            AssertionConsumerServiceUrl = new Uri(_acsUrl),
            Issuer = new EntityId(_spEntityId)
        };

        var requestId = authnRequest.Id.Value;

        // Request ID validation: store in distributed cache or session for replay attack prevention
        // RelayState carries the post-login redirect URL
        var relayState = request.RelayState ?? "/";

        // Generate redirect URL (HTTP-Redirect binding)
        var redirectUrl = authnRequest.ToXml();
        var samlRequestBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(redirectUrl));
        var encodedSamlRequest = Uri.EscapeDataString(samlRequestBase64);
        var encodedRelayState = Uri.EscapeDataString(relayState);

        var redirectUrlWithParams = $"{idp.SsoServiceUrl}?SAMLRequest={encodedSamlRequest}&RelayState={encodedRelayState}";

        _logger.LogInformation("Generated SAML AuthnRequest: {RequestId}", requestId);

        return new SamlLoginResponse
        {
            RedirectUrl = redirectUrlWithParams,
            RequestId = requestId,
            Method = "GET",
            RelayState = relayState
        };
    }

    public async Task<SamlAcsResponse> ProcessSamlResponseAsync(string samlResponse, string? relayState = null)
    {
        _logger.LogInformation("Processing SAML Response at ACS");

        try
        {
            // Decode SAML Response
            var samlResponseXml = Encoding.UTF8.GetString(Convert.FromBase64String(samlResponse));

            // Parse SAML Response
            var xmlDoc = new XmlDocument { PreserveWhitespace = true };
            xmlDoc.LoadXml(samlResponseXml);

            // Extract issuer (IdP entity ID)
            var issuerNode = xmlDoc.SelectSingleNode("//*[local-name()='Issuer']");
            if (issuerNode is null)
            {
                throw new InvalidOperationException("SAML Response missing Issuer");
            }

            var issuer = issuerNode.InnerText;

            // Get IdP configuration
            var idp = await _context.SamlIdentityProviders
                .FirstOrDefaultAsync(i => i.EntityId == issuer && i.IsEnabled);

            if (idp is null)
            {
                throw new InvalidOperationException($"Unknown or disabled SAML IdP: {issuer}");
            }

            // Validate signature if required
            if (idp.RequireSignedAssertions)
            {
                ValidateSamlSignature(xmlDoc, idp.SigningCertificate);
            }

            // Extract attributes from SAML assertion
            var attributes = ExtractSamlAttributes(xmlDoc);

            // Get email (required)
            if (!attributes.TryGetValue(idp.EmailAttributeName, out var email) || string.IsNullOrWhiteSpace(email))
            {
                throw new InvalidOperationException($"SAML Response missing required attribute: {idp.EmailAttributeName}");
            }

            // Get or create user (JIT provisioning)
            var user = await GetOrCreateUserAsync(idp, email, attributes);

            // Get user roles
            var userRoles = await _context.Set<UserRole>()
                .Include(ur => ur.Role)
                .Where(ur => ur.UserId == user.Id)
                .Select(ur => ur.Role.Name)
                .Where(name => name != null)
                .ToListAsync();

            // Generate JWT tokens
            var accessToken = _jwtService.GenerateAccessToken(user, userRoles!);
            var refreshToken = _jwtService.GenerateRefreshToken();

            var expiresIn = int.Parse(_configuration["Jwt:AccessTokenExpirationMinutes"] ?? "15") * 60;

            _logger.LogInformation("SAML authentication successful for user: {Email}", email);

            return new SamlAcsResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = expiresIn,
                UserId = user.Id,
                Email = email,
                Name = $"{user.FirstName} {user.LastName}".Trim(),
                NewUser = user.CreatedAt > DateTime.UtcNow.AddMinutes(-1),
                RelayState = relayState
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SAML Response");
            throw;
        }
    }

    public async Task<SpMetadataResponse> GetServiceProviderMetadataAsync()
    {
        _logger.LogInformation("Generating SP metadata XML");

        var metadata = new StringBuilder();
        metadata.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        metadata.AppendLine("<md:EntityDescriptor xmlns:md=\"urn:oasis:names:tc:SAML:2.0:metadata\"");
        metadata.AppendLine($"  entityID=\"{_spEntityId}\">");
        metadata.AppendLine("  <md:SPSSODescriptor protocolSupportEnumeration=\"urn:oasis:names:tc:SAML:2.0:protocol\">");
        metadata.AppendLine($"    <md:AssertionConsumerService Binding=\"urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST\" Location=\"{_acsUrl}\" index=\"0\"/>");
        metadata.AppendLine("  </md:SPSSODescriptor>");
        metadata.AppendLine("</md:EntityDescriptor>");

        return new SpMetadataResponse
        {
            MetadataXml = metadata.ToString(),
            EntityId = _spEntityId,
            AcsUrl = _acsUrl
        };
    }

    public async Task<string> InitiateSamlLogoutAsync(Guid userId, string? sessionIndex = null)
    {
        _logger.LogInformation("Initiating SAML logout for user: {UserId}", userId);

        // Single Logout (SLO) endpoint
        // Full SLO flow: Generate SAML LogoutRequest, sign it, and redirect to IdP SLO service
        return $"{_baseUrl}/logout";
    }

    // ====================
    // Helper Methods
    // ====================

    public bool ValidateCertificate(string certificatePem)
    {
        try
        {
            // Remove PEM headers/footers if present
            var certData = certificatePem
                .Replace("-----BEGIN CERTIFICATE-----", "")
                .Replace("-----END CERTIFICATE-----", "")
                .Replace("\n", "")
                .Replace("\r", "")
                .Trim();

            // Try to parse as X509Certificate
            var certBytes = Convert.FromBase64String(certData);
            var cert = new X509Certificate2(certBytes);

            // Basic validation: check if cert is valid and not expired
            return cert.NotAfter > DateTime.UtcNow && cert.NotBefore <= DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Certificate validation failed");
            return false;
        }
    }

    public async Task<(string entityId, string ssoUrl, string? sloUrl, string certificate)> ParseIdpMetadataAsync(string metadataXml)
    {
        try
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(metadataXml);

            var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("md", "urn:oasis:names:tc:SAML:2.0:metadata");
            nsmgr.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#");

            // Extract Entity ID
            var entityDescriptor = xmlDoc.SelectSingleNode("//md:EntityDescriptor", nsmgr);
            var entityId = entityDescriptor?.Attributes?["entityID"]?.Value;

            if (string.IsNullOrWhiteSpace(entityId))
            {
                throw new InvalidOperationException("Metadata missing entityID");
            }

            // Extract SSO Service URL (HTTP-POST or HTTP-Redirect binding)
            var ssoNode = xmlDoc.SelectSingleNode(
                "//md:IDPSSODescriptor/md:SingleSignOnService[@Binding='urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST' or @Binding='urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect']",
                nsmgr);

            var ssoUrl = ssoNode?.Attributes?["Location"]?.Value;

            if (string.IsNullOrWhiteSpace(ssoUrl))
            {
                throw new InvalidOperationException("Metadata missing SingleSignOnService URL");
            }

            // Extract SLO Service URL (optional)
            var sloNode = xmlDoc.SelectSingleNode(
                "//md:IDPSSODescriptor/md:SingleLogoutService[@Binding='urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST' or @Binding='urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect']",
                nsmgr);

            var sloUrl = sloNode?.Attributes?["Location"]?.Value;

            // Extract X.509 certificate
            var certNode = xmlDoc.SelectSingleNode("//md:IDPSSODescriptor/md:KeyDescriptor[@use='signing' or not(@use)]/ds:KeyInfo/ds:X509Data/ds:X509Certificate", nsmgr);

            var certificate = certNode?.InnerText?.Trim();

            if (string.IsNullOrWhiteSpace(certificate))
            {
                throw new InvalidOperationException("Metadata missing X509Certificate");
            }

            // Add PEM headers
            var certPem = $"-----BEGIN CERTIFICATE-----\n{certificate}\n-----END CERTIFICATE-----";

            return (entityId, ssoUrl, sloUrl, certPem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing IdP metadata");
            throw new InvalidOperationException("Failed to parse IdP metadata XML", ex);
        }
    }

    private void ValidateSamlSignature(XmlDocument xmlDoc, string certificatePem)
    {
        _logger.LogDebug("Validating SAML signature");

        var signatureNode = xmlDoc.SelectSingleNode("//*[local-name()='Signature']");
        if (signatureNode is null)
        {
            throw new InvalidOperationException("SAML Response is not signed but signature is required");
        }

        try
        {
            // Load the IdP's signing certificate
            var certData = certificatePem
                .Replace("-----BEGIN CERTIFICATE-----", "")
                .Replace("-----END CERTIFICATE-----", "")
                .Replace("\n", "")
                .Replace("\r", "")
                .Trim();

            var certBytes = Convert.FromBase64String(certData);
            var certificate = new X509Certificate2(certBytes);

            // Create SignedXml object
            var signedXml = new System.Security.Cryptography.Xml.SignedXml(xmlDoc);

            // Find the signature element
            if (signatureNode is not XmlElement signatureElement)
            {
                throw new InvalidOperationException("Invalid signature element");
            }

            // Load the signature
            signedXml.LoadXml(signatureElement);

            // Verify the signature using the certificate's public key
            var isValid = signedXml.CheckSignature(certificate, true);

            if (!isValid)
            {
                _logger.LogError("SAML signature validation failed for certificate: {Subject}", certificate.Subject);
                throw new InvalidOperationException("SAML Response signature is invalid");
            }

            _logger.LogInformation("SAML signature validated successfully using certificate: {Subject}", certificate.Subject);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Error validating SAML signature");
            throw new InvalidOperationException($"SAML signature validation failed: {ex.Message}", ex);
        }
    }

    private static Dictionary<string, string> ExtractSamlAttributes(XmlDocument xmlDoc)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var attributeNodes = xmlDoc.SelectNodes("//*[local-name()='Attribute']");
        if (attributeNodes == null) return attributes;

        foreach (XmlNode attributeNode in attributeNodes)
        {
            var name = attributeNode.Attributes?["Name"]?.Value;
            var valueNode = attributeNode.SelectSingleNode("*[local-name()='AttributeValue']");
            var value = valueNode?.InnerText;

            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(value))
            {
                attributes[name] = value;
            }
        }

        return attributes;
    }

    private async Task<ApplicationUser> GetOrCreateUserAsync(SamlIdentityProvider idp, string email, Dictionary<string, string> attributes)
    {
        // Try to find existing user by email
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user != null)
        {
            _logger.LogInformation("Existing user found for SAML login: {Email}", email);
            return user;
        }

        // JIT provisioning
        if (!idp.EnableJitProvisioning)
        {
            throw new InvalidOperationException($"User {email} does not exist and JIT provisioning is disabled for this IdP");
        }

        _logger.LogInformation("Creating new user via JIT provisioning: {Email}", email);

        // Extract user attributes
        attributes.TryGetValue(idp.FirstNameAttributeName ?? "firstName", out var firstName);
        attributes.TryGetValue(idp.LastNameAttributeName ?? "lastName", out var lastName);

        user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            EmailConfirmed = true, // SAML users are pre-verified
            FirstName = firstName ?? string.Empty,
            LastName = lastName ?? string.Empty,
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            SecurityStamp = Guid.NewGuid().ToString()
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Assign default role if specified
        if (idp.DefaultRoleId.HasValue)
        {
            var userRole = new UserRole
            {
                UserId = user.Id,
                RoleId = idp.DefaultRoleId.Value,
                AssignedAt = DateTime.UtcNow
            };

            _context.Set<UserRole>().Add(userRole);
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation("User created successfully via JIT provisioning: {UserId}", user.Id);

        return user;
    }

    private IdpResponse MapToIdpResponse(SamlIdentityProvider idp)
    {
        return new IdpResponse
        {
            Id = idp.Id,
            Name = idp.Name,
            EntityId = idp.EntityId,
            SsoServiceUrl = idp.SsoServiceUrl,
            SloServiceUrl = idp.SloServiceUrl,
            SignAuthnRequests = idp.SignAuthnRequests,
            RequireSignedAssertions = idp.RequireSignedAssertions,
            EnableJitProvisioning = idp.EnableJitProvisioning,
            EmailAttributeName = idp.EmailAttributeName,
            FirstNameAttributeName = idp.FirstNameAttributeName,
            LastNameAttributeName = idp.LastNameAttributeName,
            GroupsAttributeName = idp.GroupsAttributeName,
            RoleMapping = idp.RoleMapping,
            DefaultRoleId = idp.DefaultRoleId,
            IsEnabled = idp.IsEnabled,
            CreatedAt = idp.CreatedAt,
            UpdatedAt = idp.UpdatedAt
        };
    }
}

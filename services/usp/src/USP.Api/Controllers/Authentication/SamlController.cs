using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USP.Core.Models.DTOs.Saml;
using USP.Core.Services.Authentication;

namespace USP.Api.Controllers.Authentication;

/// <summary>
/// SAML 2.0 Service Provider endpoints for enterprise SSO
/// </summary>
[ApiController]
[Route("api/v1/saml")]
public class SamlController : ControllerBase
{
    private readonly ISamlService _samlService;
    private readonly IJwtService _jwtService;
    private readonly ILogger<SamlController> _logger;

    public SamlController(
        ISamlService samlService,
        IJwtService jwtService,
        ILogger<SamlController> logger)
    {
        _samlService = samlService;
        _jwtService = jwtService;
        _logger = logger;
    }

    // ====================
    // IdP Management Endpoints
    // ====================

    /// <summary>
    /// Register a new SAML Identity Provider
    /// </summary>
    [HttpPost("idp")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(IdpResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IdpResponse>> RegisterIdp([FromBody] RegisterIdpRequest request)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            var result = await _samlService.RegisterIdpAsync(request, userId);
            return CreatedAtAction(nameof(GetIdp), new { idpId = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering SAML IdP");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Import SAML IdP from metadata XML
    /// </summary>
    [HttpPost("idp/import")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(IdpResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IdpResponse>> ImportIdpMetadata([FromBody] ImportIdpMetadataRequest request)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            var result = await _samlService.ImportIdpMetadataAsync(request, userId);
            return CreatedAtAction(nameof(GetIdp), new { idpId = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing SAML IdP metadata");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get SAML IdP by ID
    /// </summary>
    [HttpGet("idp/{idpId:guid}")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(IdpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IdpResponse>> GetIdp(Guid idpId)
    {
        try
        {
            var result = await _samlService.GetIdpAsync(idpId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving SAML IdP");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// List all registered SAML IdPs
    /// </summary>
    [HttpGet("idp")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(ListIdpsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ListIdpsResponse>> ListIdps([FromQuery] bool enabledOnly = false)
    {
        try
        {
            var result = await _samlService.ListIdpsAsync(enabledOnly);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing SAML IdPs");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Update SAML IdP configuration
    /// </summary>
    [HttpPut("idp/{idpId:guid}")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(IdpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IdpResponse>> UpdateIdp(Guid idpId, [FromBody] UpdateIdpRequest request)
    {
        try
        {
            var result = await _samlService.UpdateIdpAsync(idpId, request);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating SAML IdP");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Delete SAML IdP
    /// </summary>
    [HttpDelete("idp/{idpId:guid}")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> DeleteIdp(Guid idpId)
    {
        try
        {
            await _samlService.DeleteIdpAsync(idpId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting SAML IdP");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    // ====================
    // SAML Authentication Endpoints
    // ====================

    /// <summary>
    /// Initiate SP-initiated SAML login
    /// Returns redirect URL to IdP for authentication
    /// </summary>
    [HttpGet("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SamlLoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SamlLoginResponse>> Login([FromQuery] string idp, [FromQuery] string? relayState = null)
    {
        try
        {
            var request = new SamlLoginRequest
            {
                IdpIdentifier = idp,
                RelayState = relayState
            };

            var result = await _samlService.InitiateSamlLoginAsync(request);

            // Return 302 redirect
            return Redirect(result.RedirectUrl);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating SAML login");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Assertion Consumer Service (ACS) - Process SAML Response from IdP
    /// Supports both SP-initiated and IdP-initiated flows
    /// </summary>
    [HttpPost("acs")]
    [AllowAnonymous]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType(typeof(SamlAcsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SamlAcsResponse>> AssertionConsumerService([FromForm] string SAMLResponse, [FromForm] string? RelayState = null)
    {
        try
        {
            var result = await _samlService.ProcessSamlResponseAsync(SAMLResponse, RelayState);

            // If RelayState is provided, redirect to that URL with tokens as query params
            if (!string.IsNullOrWhiteSpace(result.RelayState) && result.RelayState != "/")
            {
                var redirectUrl = $"{result.RelayState}?access_token={result.AccessToken}&refresh_token={result.RefreshToken}";
                return Redirect(redirectUrl);
            }

            // Otherwise return JSON response with tokens
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SAML Response");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get Service Provider metadata XML
    /// Used by IdPs to configure trust relationship
    /// </summary>
    [HttpGet("metadata")]
    [AllowAnonymous]
    [Produces("application/xml")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetMetadata()
    {
        try
        {
            var result = await _samlService.GetServiceProviderMetadataAsync();
            return Content(result.MetadataXml, "application/xml");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating SP metadata");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Initiate SAML logout (Single Logout)
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> Logout([FromQuery] string? sessionIndex = null)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            var logoutUrl = await _samlService.InitiateSamlLogoutAsync(userId, sessionIndex);
            return Redirect(logoutUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating SAML logout");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    // ====================
    // Helper Methods
    // ====================

    private Guid GetAuthenticatedUserId()
    {
        var userId = _jwtService.GetUserIdFromClaims(User);
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            throw new UnauthorizedAccessException("User not authenticated");
        }
        return userId.Value;
    }
}

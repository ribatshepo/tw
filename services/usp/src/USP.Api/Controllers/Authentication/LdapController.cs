using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USP.Core.Models.DTOs.Ldap;
using USP.Core.Services.Authentication;

namespace USP.Api.Controllers.Authentication;

/// <summary>
/// LDAP/Active Directory integration endpoints
/// </summary>
[ApiController]
[Route("api/v1/ldap")]
public class LdapController : ControllerBase
{
    private readonly ILdapService _ldapService;
    private readonly IJwtService _jwtService;
    private readonly ILogger<LdapController> _logger;

    public LdapController(
        ILdapService ldapService,
        IJwtService jwtService,
        ILogger<LdapController> logger)
    {
        _ldapService = ldapService;
        _jwtService = jwtService;
        _logger = logger;
    }

    // ====================
    // Configuration Management Endpoints
    // ====================

    /// <summary>
    /// Configure LDAP server
    /// </summary>
    [HttpPost("configure")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(LdapConfigResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LdapConfigResponse>> ConfigureLdap([FromBody] ConfigureLdapRequest request)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            var result = await _ldapService.ConfigureLdapAsync(request, userId);
            return CreatedAtAction(nameof(GetConfiguration), new { configId = result.Id }, result);
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
            _logger.LogError(ex, "Error configuring LDAP");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get LDAP configuration by ID
    /// </summary>
    [HttpGet("{configId:guid}")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(LdapConfigResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LdapConfigResponse>> GetConfiguration(Guid configId)
    {
        try
        {
            var result = await _ldapService.GetConfigurationAsync(configId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving LDAP configuration");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// List all LDAP configurations
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(ListLdapConfigsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ListLdapConfigsResponse>> ListConfigurations([FromQuery] bool activeOnly = true)
    {
        try
        {
            var result = await _ldapService.ListConfigurationsAsync(activeOnly);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing LDAP configurations");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Update LDAP configuration
    /// </summary>
    [HttpPut("{configId:guid}")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(LdapConfigResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LdapConfigResponse>> UpdateConfiguration(
        Guid configId,
        [FromBody] UpdateLdapConfigRequest request)
    {
        try
        {
            var result = await _ldapService.UpdateConfigurationAsync(configId, request);
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
            _logger.LogError(ex, "Error updating LDAP configuration");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Delete LDAP configuration
    /// </summary>
    [HttpDelete("{configId:guid}")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> DeleteConfiguration(Guid configId)
    {
        try
        {
            await _ldapService.DeleteConfigurationAsync(configId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting LDAP configuration");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    // ====================
    // Connection Testing
    // ====================

    /// <summary>
    /// Test LDAP connection
    /// </summary>
    [HttpPost("test")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(TestLdapConnectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TestLdapConnectionResponse>> TestConnection(
        [FromBody] TestLdapConnectionRequest request)
    {
        try
        {
            var result = await _ldapService.TestConnectionAsync(request);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing LDAP connection");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    // ====================
    // Authentication Endpoints
    // ====================

    /// <summary>
    /// Authenticate user against LDAP directory
    /// </summary>
    [HttpPost("authenticate")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LdapAuthenticationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LdapAuthenticationResponse>> Authenticate(
        [FromBody] LdapAuthenticationRequest request)
    {
        try
        {
            var result = await _ldapService.AuthenticateAsync(request);

            if (!result.Success)
                return Unauthorized(result);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during LDAP authentication");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    // ====================
    // User Search Endpoints
    // ====================

    /// <summary>
    /// Search for users in LDAP directory
    /// </summary>
    [HttpPost("users/search")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(SearchLdapUsersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SearchLdapUsersResponse>> SearchUsers([FromBody] SearchLdapUsersRequest request)
    {
        try
        {
            var result = await _ldapService.SearchUsersAsync(request);
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
            _logger.LogError(ex, "Error searching LDAP users");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get user details from LDAP
    /// </summary>
    [HttpGet("users/{configId:guid}/{username}")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(LdapUserEntry), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LdapUserEntry>> GetUserDetails(Guid configId, string username)
    {
        try
        {
            var result = await _ldapService.GetUserDetailsAsync(configId, username);

            if (result == null)
                return NotFound(new { error = "User not found in LDAP" });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving LDAP user details");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    // ====================
    // Group Sync Endpoints
    // ====================

    /// <summary>
    /// Trigger LDAP group synchronization
    /// </summary>
    [HttpPost("sync")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(SyncLdapGroupsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SyncLdapGroupsResponse>> SyncGroups([FromBody] SyncLdapGroupsRequest request)
    {
        try
        {
            var result = await _ldapService.SyncGroupsAsync(request);
            return Ok(result);
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
            _logger.LogError(ex, "Error syncing LDAP groups");
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

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USP.Core.Models.DTOs.Secrets;
using USP.Core.Services.Secrets;

namespace USP.Api.Controllers.Secrets;

/// <summary>
/// Manages time-bound leases for secret access
/// Provides lease creation, renewal, revocation, and monitoring capabilities
/// </summary>
[ApiController]
[Route("api/v1/secrets/leases")]
[Authorize]
public class LeaseController : ControllerBase
{
    private readonly ILeaseManagementService _leaseService;
    private readonly ILogger<LeaseController> _logger;

    public LeaseController(
        ILeaseManagementService leaseService,
        ILogger<LeaseController> logger)
    {
        _leaseService = leaseService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new lease for a secret
    /// </summary>
    /// <param name="request">Lease creation request</param>
    /// <returns>Created lease information</returns>
    [HttpPost]
    [ProducesResponseType(typeof(LeaseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaseDto>> CreateLease([FromBody] CreateLeaseDto request)
    {
        try
        {
            var userId = GetUserId();

            var lease = await _leaseService.CreateLeaseAsync(
                request.SecretId,
                userId,
                request.LeaseDurationSeconds,
                request.AutoRenewalEnabled,
                request.MaxRenewals);

            return CreatedAtAction(
                nameof(GetLease),
                new { leaseId = lease.LeaseId },
                lease);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to create lease");
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid lease parameters");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating lease");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Renew an existing lease
    /// </summary>
    /// <param name="request">Lease renewal request</param>
    /// <returns>Updated lease information</returns>
    [HttpPost("renew")]
    [ProducesResponseType(typeof(LeaseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaseDto>> RenewLease([FromBody] RenewLeaseDto request)
    {
        try
        {
            var userId = GetUserId();

            var lease = await _leaseService.RenewLeaseAsync(
                request.LeaseId,
                userId,
                request.IncrementSeconds);

            return Ok(lease);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to renew lease {LeaseId}", request.LeaseId);
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized lease renewal attempt for {LeaseId}", request.LeaseId);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error renewing lease {LeaseId}", request.LeaseId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Revoke a lease immediately
    /// </summary>
    /// <param name="request">Lease revocation request</param>
    [HttpPost("revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeLease([FromBody] RevokeLeaseDto request)
    {
        try
        {
            var userId = GetUserId();

            await _leaseService.RevokeLeaseAsync(
                request.LeaseId,
                userId,
                request.Reason);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to revoke lease {LeaseId}", request.LeaseId);
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized lease revocation attempt for {LeaseId}", request.LeaseId);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking lease {LeaseId}", request.LeaseId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get lease information by ID
    /// </summary>
    /// <param name="leaseId">Lease ID</param>
    /// <returns>Lease information</returns>
    [HttpGet("{leaseId:guid}")]
    [ProducesResponseType(typeof(LeaseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaseDto>> GetLease(Guid leaseId)
    {
        try
        {
            var userId = GetUserId();

            var lease = await _leaseService.GetLeaseAsync(leaseId, userId);

            return Ok(lease);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Lease {LeaseId} not found", leaseId);
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to lease {LeaseId}", leaseId);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving lease {LeaseId}", leaseId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get all leases for the current user
    /// </summary>
    /// <param name="includeExpired">Include expired leases in results</param>
    /// <returns>List of user's leases</returns>
    [HttpGet("my")]
    [ProducesResponseType(typeof(List<LeaseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<LeaseDto>>> GetMyLeases([FromQuery] bool includeExpired = false)
    {
        try
        {
            var userId = GetUserId();

            var leases = await _leaseService.GetUserLeasesAsync(userId, includeExpired);

            return Ok(leases);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving leases for user");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get all leases for a specific secret
    /// </summary>
    /// <param name="secretId">Secret ID</param>
    /// <param name="includeExpired">Include expired leases in results</param>
    /// <returns>List of leases for the secret</returns>
    [HttpGet("secret/{secretId:guid}")]
    [ProducesResponseType(typeof(List<LeaseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<LeaseDto>>> GetSecretLeases(
        Guid secretId,
        [FromQuery] bool includeExpired = false)
    {
        try
        {
            var userId = GetUserId();

            var leases = await _leaseService.GetSecretLeasesAsync(secretId, userId, includeExpired);

            return Ok(leases);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Secret {SecretId} not found", secretId);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving leases for secret {SecretId}", secretId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get renewal history for a lease
    /// </summary>
    /// <param name="leaseId">Lease ID</param>
    /// <returns>List of renewal history entries</returns>
    [HttpGet("{leaseId:guid}/history")]
    [ProducesResponseType(typeof(List<LeaseRenewalHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<LeaseRenewalHistoryDto>>> GetLeaseHistory(Guid leaseId)
    {
        try
        {
            var userId = GetUserId();

            var history = await _leaseService.GetLeaseRenewalHistoryAsync(leaseId, userId);

            return Ok(history);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Lease {LeaseId} not found", leaseId);
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to lease history {LeaseId}", leaseId);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving lease history for {LeaseId}", leaseId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get lease statistics for the current user
    /// </summary>
    /// <returns>Lease statistics</returns>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(LeaseStatisticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LeaseStatisticsDto>> GetStatistics()
    {
        try
        {
            var userId = GetUserId();

            var statistics = await _leaseService.GetLeaseStatisticsAsync(userId);

            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving lease statistics");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Revoke all active leases for a secret (admin operation)
    /// </summary>
    /// <param name="secretId">Secret ID</param>
    /// <param name="reason">Reason for mass revocation</param>
    [HttpPost("secret/{secretId:guid}/revoke-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeAllSecretLeases(
        Guid secretId,
        [FromQuery] string? reason = null)
    {
        try
        {
            var userId = GetUserId();

            await _leaseService.RevokeAllSecretLeasesAsync(secretId, userId, reason);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to revoke leases for secret {SecretId}", secretId);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking leases for secret {SecretId}", secretId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}

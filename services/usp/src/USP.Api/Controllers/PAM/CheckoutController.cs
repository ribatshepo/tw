using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USP.Core.Models.DTOs.PAM;
using USP.Core.Services.PAM;

namespace USP.Api.Controllers.PAM;

/// <summary>
/// Controller for privileged account checkout/checkin operations
/// </summary>
[ApiController]
[Route("api/v1/pam/checkout")]
[Authorize]
public class CheckoutController : ControllerBase
{
    private readonly ICheckoutService _checkoutService;
    private readonly ILogger<CheckoutController> _logger;

    public CheckoutController(
        ICheckoutService checkoutService,
        ILogger<CheckoutController> logger)
    {
        _checkoutService = checkoutService;
        _logger = logger;
    }

    /// <summary>
    /// Request checkout of a privileged account
    /// </summary>
    [HttpPost("{accountId:guid}")]
    [ProducesResponseType(typeof(CheckoutResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CheckoutResponseDto>> RequestCheckout(
        Guid accountId,
        [FromBody] CheckoutRequestDto request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var response = await _checkoutService.RequestCheckoutAsync(accountId, userId, request);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting checkout for account {AccountId}", accountId);
            return StatusCode(500, new { error = "Failed to checkout account" });
        }
    }

    /// <summary>
    /// Checkin a privileged account
    /// </summary>
    [HttpPost("{checkoutId:guid}/checkin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> Checkin(
        Guid checkoutId,
        [FromBody] CheckinRequestDto? request = null)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var success = await _checkoutService.CheckinAccountAsync(checkoutId, userId, request);

            if (!success)
                return NotFound(new { error = "Checkout not found" });

            return Ok(new { message = "Account checked in successfully" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking in checkout {CheckoutId}", checkoutId);
            return StatusCode(500, new { error = "Failed to checkin account" });
        }
    }

    /// <summary>
    /// Get active checkouts for current user
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(List<AccountCheckoutDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AccountCheckoutDto>>> GetActiveCheckouts()
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var checkouts = await _checkoutService.GetActiveCheckoutsAsync(userId);

            return Ok(checkouts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active checkouts");
            return StatusCode(500, new { error = "Failed to retrieve active checkouts" });
        }
    }

    /// <summary>
    /// Get checkout history for current user
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(List<AccountCheckoutDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AccountCheckoutDto>>> GetCheckoutHistory([FromQuery] int? limit = 50)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var checkouts = await _checkoutService.GetCheckoutHistoryAsync(userId, limit);

            return Ok(checkouts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving checkout history");
            return StatusCode(500, new { error = "Failed to retrieve checkout history" });
        }
    }

    /// <summary>
    /// Get checkout by ID
    /// </summary>
    [HttpGet("{checkoutId:guid}")]
    [ProducesResponseType(typeof(AccountCheckoutDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountCheckoutDto>> GetCheckoutById(Guid checkoutId)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var checkout = await _checkoutService.GetCheckoutByIdAsync(checkoutId, userId);

            if (checkout == null)
                return NotFound(new { error = "Checkout not found or access denied" });

            return Ok(checkout);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving checkout {CheckoutId}", checkoutId);
            return StatusCode(500, new { error = "Failed to retrieve checkout" });
        }
    }

    /// <summary>
    /// Extend checkout duration
    /// </summary>
    [HttpPost("{checkoutId:guid}/extend")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> ExtendCheckout(
        Guid checkoutId,
        [FromBody] ExtendCheckoutRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            if (request.AdditionalMinutes <= 0)
                return BadRequest(new { error = "Additional minutes must be greater than 0" });

            var success = await _checkoutService.ExtendCheckoutAsync(checkoutId, userId, request.AdditionalMinutes);

            if (!success)
                return NotFound(new { error = "Checkout not found" });

            return Ok(new { message = "Checkout extended successfully" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extending checkout {CheckoutId}", checkoutId);
            return StatusCode(500, new { error = "Failed to extend checkout" });
        }
    }

    /// <summary>
    /// Force checkin (admin operation)
    /// </summary>
    [HttpPost("{checkoutId:guid}/force-checkin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> ForceCheckin(
        Guid checkoutId,
        [FromBody] ForceCheckinRequest request)
    {
        try
        {
            var adminUserId = GetUserId();
            if (adminUserId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            if (string.IsNullOrWhiteSpace(request.Reason))
                return BadRequest(new { error = "Reason is required for force checkin" });

            var success = await _checkoutService.ForceCheckinAsync(checkoutId, adminUserId, request.Reason);

            if (!success)
                return NotFound(new { error = "Checkout not found" });

            return Ok(new { message = "Account force checked in successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error force checking in checkout {CheckoutId}", checkoutId);
            return StatusCode(500, new { error = "Failed to force checkin" });
        }
    }

    /// <summary>
    /// Check if account is currently checked out
    /// </summary>
    [HttpGet("account/{accountId:guid}/status")]
    [ProducesResponseType(typeof(AccountCheckoutStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AccountCheckoutStatusDto>> GetAccountCheckoutStatus(Guid accountId)
    {
        try
        {
            var isCheckedOut = await _checkoutService.IsAccountCheckedOutAsync(accountId);
            var activeCheckout = isCheckedOut
                ? await _checkoutService.GetActiveCheckoutForAccountAsync(accountId)
                : null;

            return Ok(new AccountCheckoutStatusDto
            {
                AccountId = accountId,
                IsCheckedOut = isCheckedOut,
                ActiveCheckout = activeCheckout
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking account {AccountId} checkout status", accountId);
            return StatusCode(500, new { error = "Failed to check account status" });
        }
    }

    /// <summary>
    /// Get checkout statistics for current user
    /// </summary>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(CheckoutStatisticsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CheckoutStatisticsDto>> GetStatistics()
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var stats = await _checkoutService.GetCheckoutStatisticsAsync(userId);

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving checkout statistics");
            return StatusCode(500, new { error = "Failed to retrieve statistics" });
        }
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}

// Request DTOs
public class ExtendCheckoutRequest
{
    public int AdditionalMinutes { get; set; }
}

public class ForceCheckinRequest
{
    public string Reason { get; set; } = string.Empty;
}

// Response DTOs
public class AccountCheckoutStatusDto
{
    public Guid AccountId { get; set; }
    public bool IsCheckedOut { get; set; }
    public AccountCheckoutDto? ActiveCheckout { get; set; }
}

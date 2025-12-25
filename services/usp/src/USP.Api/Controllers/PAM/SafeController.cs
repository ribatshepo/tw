using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USP.Core.Models.DTOs.PAM;
using USP.Core.Services.PAM;

namespace USP.Api.Controllers.PAM;

/// <summary>
/// Controller for privileged safe and account management
/// </summary>
[ApiController]
[Route("api/v1/pam/safes")]
[Authorize]
public class SafeController : ControllerBase
{
    private readonly ISafeManagementService _safeService;
    private readonly ILogger<SafeController> _logger;

    public SafeController(
        ISafeManagementService safeService,
        ILogger<SafeController> logger)
    {
        _safeService = safeService;
        _logger = logger;
    }

    // Safe Management Endpoints

    /// <summary>
    /// Create a new privileged safe
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PrivilegedSafeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PrivilegedSafeDto>> CreateSafe([FromBody] CreateSafeRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var safe = await _safeService.CreateSafeAsync(userId, request);

            return CreatedAtAction(nameof(GetSafeById), new { id = safe.Id }, safe);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating safe");
            return StatusCode(500, new { error = "Failed to create safe" });
        }
    }

    /// <summary>
    /// Get all safes accessible by the current user
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(SafesResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SafesResponse>> GetSafes([FromQuery] string? safeType = null)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var safes = await _safeService.GetSafesAsync(userId, safeType);

            var response = new SafesResponse
            {
                Safes = safes,
                TotalCount = safes.Count
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving safes");
            return StatusCode(500, new { error = "Failed to retrieve safes" });
        }
    }

    /// <summary>
    /// Get safe by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PrivilegedSafeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PrivilegedSafeDto>> GetSafeById(Guid id)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var safe = await _safeService.GetSafeByIdAsync(id, userId);

            if (safe == null)
                return NotFound(new { error = "Safe not found or access denied" });

            return Ok(safe);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving safe {SafeId}", id);
            return StatusCode(500, new { error = "Failed to retrieve safe" });
        }
    }

    /// <summary>
    /// Update safe
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> UpdateSafe(Guid id, [FromBody] UpdateSafeRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var success = await _safeService.UpdateSafeAsync(id, userId, request);

            if (!success)
                return NotFound(new { error = "Safe not found or insufficient permissions" });

            return Ok(new { message = "Safe updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating safe {SafeId}", id);
            return StatusCode(500, new { error = "Failed to update safe" });
        }
    }

    /// <summary>
    /// Delete safe (and all accounts in it)
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> DeleteSafe(Guid id)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var success = await _safeService.DeleteSafeAsync(id, userId);

            if (!success)
                return NotFound(new { error = "Safe not found or only owner can delete" });

            return Ok(new { message = "Safe deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting safe {SafeId}", id);
            return StatusCode(500, new { error = "Failed to delete safe" });
        }
    }

    // Account Management Endpoints

    /// <summary>
    /// Add a privileged account to a safe
    /// </summary>
    [HttpPost("{safeId:guid}/accounts")]
    [ProducesResponseType(typeof(PrivilegedAccountDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PrivilegedAccountDto>> AddAccount(
        Guid safeId,
        [FromBody] CreatePrivilegedAccountRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var account = await _safeService.AddAccountAsync(safeId, userId, request);

            return CreatedAtAction(nameof(GetAccountById),
                new { safeId, accountId = account.Id }, account);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding account to safe {SafeId}", safeId);
            return StatusCode(500, new { error = "Failed to add account" });
        }
    }

    /// <summary>
    /// Get all accounts in a safe
    /// </summary>
    [HttpGet("{safeId:guid}/accounts")]
    [ProducesResponseType(typeof(AccountsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AccountsResponse>> GetAccounts(Guid safeId)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var accounts = await _safeService.GetAccountsAsync(safeId, userId);

            var response = new AccountsResponse
            {
                Accounts = accounts,
                TotalCount = accounts.Count
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving accounts for safe {SafeId}", safeId);
            return StatusCode(500, new { error = "Failed to retrieve accounts" });
        }
    }

    /// <summary>
    /// Get account by ID
    /// </summary>
    [HttpGet("{safeId:guid}/accounts/{accountId:guid}")]
    [ProducesResponseType(typeof(PrivilegedAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PrivilegedAccountDto>> GetAccountById(Guid safeId, Guid accountId)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var account = await _safeService.GetAccountByIdAsync(accountId, userId);

            if (account == null)
                return NotFound(new { error = "Account not found or access denied" });

            return Ok(account);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving account {AccountId}", accountId);
            return StatusCode(500, new { error = "Failed to retrieve account" });
        }
    }

    /// <summary>
    /// Update privileged account
    /// </summary>
    [HttpPut("{safeId:guid}/accounts/{accountId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> UpdateAccount(
        Guid safeId,
        Guid accountId,
        [FromBody] UpdatePrivilegedAccountRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var success = await _safeService.UpdateAccountAsync(accountId, userId, request);

            if (!success)
                return NotFound(new { error = "Account not found or insufficient permissions" });

            return Ok(new { message = "Account updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating account {AccountId}", accountId);
            return StatusCode(500, new { error = "Failed to update account" });
        }
    }

    /// <summary>
    /// Delete privileged account
    /// </summary>
    [HttpDelete("{safeId:guid}/accounts/{accountId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> DeleteAccount(Guid safeId, Guid accountId)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var success = await _safeService.DeleteAccountAsync(accountId, userId);

            if (!success)
                return NotFound(new { error = "Account not found or insufficient permissions" });

            return Ok(new { message = "Account deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting account {AccountId}", accountId);
            return StatusCode(500, new { error = "Failed to delete account" });
        }
    }

    /// <summary>
    /// Reveal account password (requires appropriate permissions and logs the action)
    /// </summary>
    [HttpPost("{safeId:guid}/accounts/{accountId:guid}/reveal")]
    [ProducesResponseType(typeof(RevealPasswordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RevealPasswordResponse>> RevealPassword(
        Guid safeId,
        Guid accountId,
        [FromBody] RevealPasswordRequest? request = null)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var response = await _safeService.RevealPasswordAsync(accountId, userId, request?.Reason);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revealing password for account {AccountId}", accountId);
            return StatusCode(500, new { error = "Failed to reveal password" });
        }
    }

    /// <summary>
    /// Search accounts across all accessible safes
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(AccountsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AccountsResponse>> SearchAccounts(
        [FromQuery] string searchTerm,
        [FromQuery] string? platform = null)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            if (string.IsNullOrWhiteSpace(searchTerm))
                return BadRequest(new { error = "Search term is required" });

            var accounts = await _safeService.SearchAccountsAsync(userId, searchTerm, platform);

            var response = new AccountsResponse
            {
                Accounts = accounts,
                TotalCount = accounts.Count
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching accounts");
            return StatusCode(500, new { error = "Failed to search accounts" });
        }
    }

    /// <summary>
    /// Get safe statistics
    /// </summary>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(SafeStatisticsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SafeStatisticsDto>> GetStatistics()
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var stats = await _safeService.GetStatisticsAsync(userId);

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving statistics");
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
public class RevealPasswordRequest
{
    public string? Reason { get; set; }
}

// Response DTOs
public class SafesResponse
{
    public List<PrivilegedSafeDto> Safes { get; set; } = new();
    public int TotalCount { get; set; }
}

public class AccountsResponse
{
    public List<PrivilegedAccountDto> Accounts { get; set; } = new();
    public int TotalCount { get; set; }
}

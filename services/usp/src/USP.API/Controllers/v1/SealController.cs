using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USP.Core.Interfaces.Services.Secrets;

namespace USP.API.Controllers.v1;

/// <summary>
/// Vault seal/unseal API (Vault-compatible endpoints).
/// Manages vault initialization, sealing, and unsealing using Shamir's Secret Sharing.
/// </summary>
[ApiController]
[Route("v1/sys")]
public class SealController : ControllerBase
{
    private readonly ISealService _sealService;
    private readonly ILogger<SealController> _logger;

    public SealController(
        ISealService sealService,
        ILogger<SealController> logger)
    {
        _sealService = sealService ?? throw new ArgumentNullException(nameof(sealService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initialize the vault.
    /// This can only be called once. It generates a master key, splits it using
    /// Shamir's Secret Sharing, and returns the unseal keys and root token.
    ///
    /// CRITICAL: The unseal keys and root token are only returned once.
    /// Store them securely - they cannot be recovered if lost.
    /// </summary>
    /// <param name="request">Initialization request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Unseal keys and root token</returns>
    [HttpPost("init")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(InitializeResult), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Initialize(
        [FromBody] InitializeRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate request
            if (request.SecretShares < 1 || request.SecretShares > 255)
            {
                return BadRequest(new
                {
                    errors = new[] { "secret_shares must be between 1 and 255" }
                });
            }

            if (request.SecretThreshold < 1 || request.SecretThreshold > request.SecretShares)
            {
                return BadRequest(new
                {
                    errors = new[] { "secret_threshold must be between 1 and secret_shares" }
                });
            }

            var result = await _sealService.InitializeAsync(
                request.SecretShares,
                request.SecretThreshold,
                cancellationToken);

            _logger.LogInformation("Vault initialized with {Shares} shares and {Threshold} threshold",
                request.SecretShares, request.SecretThreshold);

            // Return Vault-compatible response
            return Ok(new
            {
                keys = result.UnsealKeys,
                keys_base64 = result.UnsealKeys,
                keys_hex = result.UnsealKeysHex,
                root_token = result.RootToken,
                secret_shares = result.SecretShares,
                secret_threshold = result.SecretThreshold
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Vault initialization failed");
            return BadRequest(new { errors = new[] { ex.Message } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing vault");
            return StatusCode(500, new { errors = new[] { "Internal server error" } });
        }
    }

    /// <summary>
    /// Submit an unseal key to unseal the vault.
    /// Requires threshold number of unique unseal keys to successfully unseal.
    /// </summary>
    /// <param name="request">Unseal request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current seal status</returns>
    [HttpPost("unseal")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SealStatusResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Unseal(
        [FromBody] UnsealRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Key))
            {
                return BadRequest(new { errors = new[] { "key is required" } });
            }

            var status = await _sealService.UnsealAsync(request.Key, cancellationToken);

            _logger.LogInformation("Unseal key processed. Progress: {Progress}/{Threshold}",
                status.Progress, status.Threshold);

            return Ok(MapToVaultResponse(status));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Unseal failed");
            return BadRequest(new { errors = new[] { ex.Message } });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid unseal key");
            return BadRequest(new { errors = new[] { ex.Message } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unsealing vault");
            return StatusCode(500, new { errors = new[] { "Internal server error" } });
        }
    }

    /// <summary>
    /// Seal the vault.
    /// Removes the master key from memory. All cryptographic operations will fail
    /// until the vault is unsealed again.
    ///
    /// Requires authentication (root token or admin privileges).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Seal status</returns>
    [HttpPost("seal")]
    [AllowAnonymous] // TODO: Implement X-Vault-Token authentication for production
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> Seal(CancellationToken cancellationToken = default)
    {
        try
        {
            await _sealService.SealAsync(cancellationToken);

            _logger.LogWarning("Vault sealed by user {User}",
                User.Identity?.Name ?? "Unknown");

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Seal failed");
            return BadRequest(new { errors = new[] { ex.Message } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sealing vault");
            return StatusCode(500, new { errors = new[] { "Internal server error" } });
        }
    }

    /// <summary>
    /// Get the current seal status.
    /// Shows whether the vault is sealed, initialized, and unseal progress.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Seal status</returns>
    [HttpGet("seal-status")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SealStatusResponse), 200)]
    public async Task<IActionResult> GetSealStatus(CancellationToken cancellationToken = default)
    {
        try
        {
            var status = await _sealService.GetSealStatusAsync(cancellationToken);
            return Ok(MapToVaultResponse(status));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting seal status");
            return StatusCode(500, new { errors = new[] { "Internal server error" } });
        }
    }

    /// <summary>
    /// Reset unseal progress.
    /// Clears all submitted unseal keys and starts the unseal process over.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content</returns>
    [HttpPost("unseal-reset")]
    [AllowAnonymous]
    [ProducesResponseType(204)]
    public async Task<IActionResult> ResetUnsealProgress(CancellationToken cancellationToken = default)
    {
        try
        {
            await _sealService.ResetUnsealProgressAsync(cancellationToken);
            _logger.LogInformation("Unseal progress reset");
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting unseal progress");
            return StatusCode(500, new { errors = new[] { "Internal server error" } });
        }
    }

    /// <summary>
    /// Maps SealStatus to Vault-compatible response format.
    /// </summary>
    private static object MapToVaultResponse(SealStatus status)
    {
        return new
        {
            type = "shamir",
            initialized = status.Initialized,
            @sealed = status.Sealed,
            t = status.Threshold,
            n = status.SecretShares,
            progress = status.Progress,
            nonce = "",
            version = status.Version,
            build_date = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            migration = false,
            cluster_name = status.ClusterName,
            cluster_id = status.ClusterId,
            recovery_seal = false,
            storage_type = "postgresql"
        };
    }
}

/// <summary>
/// Request to initialize the vault.
/// </summary>
public class InitializeRequest
{
    /// <summary>
    /// Number of key shares to split the master key into.
    /// </summary>
    public int SecretShares { get; set; } = 5;

    /// <summary>
    /// Number of key shares required to reconstruct the master key.
    /// </summary>
    public int SecretThreshold { get; set; } = 3;
}

/// <summary>
/// Request to unseal the vault with a key.
/// </summary>
public class UnsealRequest
{
    /// <summary>
    /// Unseal key (Base64 or Hex encoded).
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Whether to reset the unseal process before submitting this key.
    /// </summary>
    public bool Reset { get; set; } = false;
}

/// <summary>
/// Seal status response (Vault-compatible format).
/// </summary>
public class SealStatusResponse
{
    public string Type { get; set; } = "shamir";
    public bool Initialized { get; set; }
    public bool Sealed { get; set; }
    public int T { get; set; } // Threshold
    public int N { get; set; } // Total shares
    public int Progress { get; set; }
    public string Nonce { get; set; } = "";
    public string? Version { get; set; }
    public string? ClusterName { get; set; }
    public string? ClusterId { get; set; }
}

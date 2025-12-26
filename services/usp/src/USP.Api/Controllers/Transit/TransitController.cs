using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USP.Core.Models.DTOs.Transit;
using USP.Core.Services.Authentication;
using USP.Core.Services.Secrets;

namespace USP.Api.Controllers.Transit;

/// <summary>
/// Transit Engine API - Encryption as a Service
/// Provides Vault-compatible transit encryption with named keys and versioning
/// </summary>
[ApiController]
[Route("api/v1/transit")]
[Authorize]
[Produces("application/json")]
public class TransitController : ControllerBase
{
    private readonly ITransitEngine _transitEngine;
    private readonly IJwtService _jwtService;
    private readonly ILogger<TransitController> _logger;

    public TransitController(
        ITransitEngine transitEngine,
        IJwtService jwtService,
        ILogger<TransitController> logger)
    {
        _transitEngine = transitEngine;
        _jwtService = jwtService;
        _logger = logger;
    }

    /// <summary>
    /// Get user ID from JWT claims
    /// </summary>
    private Guid GetAuthenticatedUserId()
    {
        var userId = _jwtService.GetUserIdFromClaims(User);
        if (userId == null)
            throw new UnauthorizedAccessException("User ID not found in claims");
        return userId.Value;
    }

    // ============================================
    // Key Management Endpoints
    // ============================================

    /// <summary>
    /// Create a new named encryption key
    /// </summary>
    /// <param name="keyName">Unique name for the key (e.g., "customer-data-key")</param>
    /// <param name="request">Key creation parameters</param>
    /// <returns>Created key metadata</returns>
    [HttpPost("keys/{keyName}")]
    [ProducesResponseType(typeof(CreateKeyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CreateKeyResponse>> CreateKey(
        [FromRoute] string keyName,
        [FromBody] CreateKeyRequest request)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            var response = await _transitEngine.CreateKeyAsync(keyName, request, userId);

            _logger.LogInformation("Transit key '{KeyName}' created by user {UserId}", keyName, userId);
            return CreatedAtAction(nameof(ReadKey), new { keyName }, response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Request",
                Detail = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Key Already Exists",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating transit key '{KeyName}'", keyName);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An error occurred while creating the transit key"
            });
        }
    }

    /// <summary>
    /// Read key metadata and configuration
    /// </summary>
    /// <param name="keyName">Name of the key</param>
    /// <returns>Key metadata including all versions</returns>
    [HttpGet("keys/{keyName}")]
    [ProducesResponseType(typeof(ReadKeyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ReadKeyResponse>> ReadKey([FromRoute] string keyName)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            var response = await _transitEngine.ReadKeyAsync(keyName, userId);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Key Not Found",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading transit key '{KeyName}'", keyName);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An error occurred while reading the transit key"
            });
        }
    }

    /// <summary>
    /// List all transit keys
    /// </summary>
    /// <returns>List of key names</returns>
    [HttpGet("keys")]
    [ProducesResponseType(typeof(ListKeysResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ListKeysResponse>> ListKeys()
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            var response = await _transitEngine.ListKeysAsync(userId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing transit keys");
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An error occurred while listing transit keys"
            });
        }
    }

    /// <summary>
    /// Delete a transit key (only if DeletionAllowed is true)
    /// </summary>
    /// <param name="keyName">Name of the key to delete</param>
    /// <returns>No content on success</returns>
    [HttpDelete("keys/{keyName}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteKey([FromRoute] string keyName)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            await _transitEngine.DeleteKeyAsync(keyName, userId);

            _logger.LogWarning("Transit key '{KeyName}' deleted by user {UserId}", keyName, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Key Not Found",
                Detail = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Deletion Not Allowed",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting transit key '{KeyName}'", keyName);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An error occurred while deleting the transit key"
            });
        }
    }

    /// <summary>
    /// Update key configuration (min versions, deletion policy)
    /// </summary>
    /// <param name="keyName">Name of the key</param>
    /// <param name="request">Configuration updates</param>
    /// <returns>Updated key configuration</returns>
    [HttpPut("keys/{keyName}/config")]
    [ProducesResponseType(typeof(UpdateKeyConfigResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UpdateKeyConfigResponse>> UpdateKeyConfig(
        [FromRoute] string keyName,
        [FromBody] UpdateKeyConfigRequest request)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            var response = await _transitEngine.UpdateKeyConfigAsync(keyName, request, userId);

            _logger.LogInformation("Transit key '{KeyName}' configuration updated by user {UserId}", keyName, userId);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Key Not Found",
                Detail = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Configuration",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating transit key '{KeyName}' configuration", keyName);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An error occurred while updating the key configuration"
            });
        }
    }

    /// <summary>
    /// Rotate a key (create a new version)
    /// </summary>
    /// <param name="keyName">Name of the key to rotate</param>
    /// <returns>New key version number</returns>
    [HttpPost("keys/{keyName}/rotate")]
    [ProducesResponseType(typeof(RotateKeyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RotateKeyResponse>> RotateKey([FromRoute] string keyName)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            var response = await _transitEngine.RotateKeyAsync(keyName, userId);

            _logger.LogInformation("Transit key '{KeyName}' rotated to version {Version} by user {UserId}",
                keyName, response.LatestVersion, userId);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Key Not Found",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rotating transit key '{KeyName}'", keyName);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An error occurred while rotating the transit key"
            });
        }
    }

    // ============================================
    // Encryption Operations
    // ============================================

    /// <summary>
    /// Encrypt plaintext data using the named key
    /// </summary>
    /// <param name="keyName">Name of the encryption key</param>
    /// <param name="request">Encryption parameters (plaintext, context, version)</param>
    /// <returns>Ciphertext in vault format: vault:v{version}:{base64_ciphertext}</returns>
    [HttpPost("encrypt/{keyName}")]
    [ProducesResponseType(typeof(EncryptResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EncryptResponse>> Encrypt(
        [FromRoute] string keyName,
        [FromBody] EncryptRequest request)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            var response = await _transitEngine.EncryptAsync(keyName, request, userId);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Key Not Found",
                Detail = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Request",
                Detail = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Operation Not Allowed",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error encrypting with transit key '{KeyName}'", keyName);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An error occurred during encryption"
            });
        }
    }

    /// <summary>
    /// Decrypt ciphertext using the named key
    /// </summary>
    /// <param name="keyName">Name of the encryption key</param>
    /// <param name="request">Decryption parameters (ciphertext, context)</param>
    /// <returns>Base64-encoded plaintext</returns>
    [HttpPost("decrypt/{keyName}")]
    [ProducesResponseType(typeof(DecryptResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DecryptResponse>> Decrypt(
        [FromRoute] string keyName,
        [FromBody] DecryptRequest request)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            var response = await _transitEngine.DecryptAsync(keyName, request, userId);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Key Not Found",
                Detail = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Request",
                Detail = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Operation Not Allowed",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decrypting with transit key '{KeyName}'", keyName);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An error occurred during decryption"
            });
        }
    }

    /// <summary>
    /// Rewrap ciphertext (re-encrypt with latest key version)
    /// Useful for key rotation without decrypting to plaintext in application
    /// </summary>
    /// <param name="keyName">Name of the encryption key</param>
    /// <param name="request">Rewrap parameters (ciphertext, context)</param>
    /// <returns>Re-encrypted ciphertext with latest version</returns>
    [HttpPost("rewrap/{keyName}")]
    [ProducesResponseType(typeof(RewrapResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RewrapResponse>> Rewrap(
        [FromRoute] string keyName,
        [FromBody] RewrapRequest request)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            var response = await _transitEngine.RewrapAsync(keyName, request, userId);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Key Not Found",
                Detail = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Request",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rewrapping with transit key '{KeyName}'", keyName);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An error occurred during rewrap"
            });
        }
    }

    // ============================================
    // Batch Operations
    // ============================================

    /// <summary>
    /// Encrypt multiple plaintexts in a single operation
    /// </summary>
    /// <param name="keyName">Name of the encryption key</param>
    /// <param name="request">Batch of plaintexts to encrypt (max 1000 items)</param>
    /// <returns>Batch of ciphertexts</returns>
    [HttpPost("encrypt/{keyName}/batch")]
    [ProducesResponseType(typeof(BatchEncryptResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BatchEncryptResponse>> BatchEncrypt(
        [FromRoute] string keyName,
        [FromBody] BatchEncryptRequest request)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            var response = await _transitEngine.BatchEncryptAsync(keyName, request, userId);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Key Not Found",
                Detail = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Request",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error batch encrypting with transit key '{KeyName}'", keyName);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An error occurred during batch encryption"
            });
        }
    }

    /// <summary>
    /// Decrypt multiple ciphertexts in a single operation
    /// </summary>
    /// <param name="keyName">Name of the encryption key</param>
    /// <param name="request">Batch of ciphertexts to decrypt (max 1000 items)</param>
    /// <returns>Batch of plaintexts</returns>
    [HttpPost("decrypt/{keyName}/batch")]
    [ProducesResponseType(typeof(BatchDecryptResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BatchDecryptResponse>> BatchDecrypt(
        [FromRoute] string keyName,
        [FromBody] BatchDecryptRequest request)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            var response = await _transitEngine.BatchDecryptAsync(keyName, request, userId);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Key Not Found",
                Detail = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Request",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error batch decrypting with transit key '{KeyName}'", keyName);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An error occurred during batch decryption"
            });
        }
    }

    // ============================================
    // Data Key Generation (Envelope Encryption)
    // ============================================

    /// <summary>
    /// Generate a high-entropy data encryption key (DEK)
    /// Returns both plaintext key (for immediate use) and encrypted key (for storage)
    /// Used for envelope encryption pattern
    /// </summary>
    /// <param name="keyName">Name of the key-encryption-key (KEK)</param>
    /// <param name="request">Data key generation parameters (bits, context)</param>
    /// <returns>Plaintext and encrypted data key</returns>
    [HttpPost("datakey/plaintext/{keyName}")]
    [ProducesResponseType(typeof(GenerateDataKeyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GenerateDataKeyResponse>> GenerateDataKey(
        [FromRoute] string keyName,
        [FromBody] GenerateDataKeyRequest request)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            var response = await _transitEngine.GenerateDataKeyAsync(keyName, request, userId);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Key Not Found",
                Detail = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Request",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating data key with transit key '{KeyName}'", keyName);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An error occurred while generating the data key"
            });
        }
    }

    // ============================================
    // Signing Operations (Asymmetric Keys)
    // ============================================

    /// <summary>
    /// Sign data using an asymmetric key
    /// Supported for: rsa-2048, rsa-4096, ed25519, ecdsa-p256
    /// </summary>
    /// <param name="keyName">Name of the signing key</param>
    /// <param name="request">Signing parameters (data, hash algorithm)</param>
    /// <returns>Digital signature</returns>
    [HttpPost("sign/{keyName}")]
    [ProducesResponseType(typeof(SignResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SignResponse>> Sign(
        [FromRoute] string keyName,
        [FromBody] SignRequest request)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            var response = await _transitEngine.SignAsync(keyName, request, userId);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Key Not Found",
                Detail = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Operation Not Supported",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error signing with transit key '{KeyName}'", keyName);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An error occurred during signing"
            });
        }
    }

    /// <summary>
    /// Verify a signature using an asymmetric key
    /// Supported for: rsa-2048, rsa-4096, ed25519, ecdsa-p256
    /// </summary>
    /// <param name="keyName">Name of the signing key</param>
    /// <param name="request">Verification parameters (data, signature, hash algorithm)</param>
    /// <returns>True if signature is valid</returns>
    [HttpPost("verify/{keyName}")]
    [ProducesResponseType(typeof(VerifyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<VerifyResponse>> Verify(
        [FromRoute] string keyName,
        [FromBody] VerifyRequest request)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            var response = await _transitEngine.VerifyAsync(keyName, request, userId);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Key Not Found",
                Detail = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Operation Not Supported",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying signature with transit key '{KeyName}'", keyName);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An error occurred during signature verification"
            });
        }
    }
}

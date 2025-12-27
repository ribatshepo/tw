using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using USP.Core.Interfaces.Services.Secrets;

namespace USP.API.Controllers.v1;

/// <summary>
/// Secrets management endpoints (Vault KV v2 compatible)
/// </summary>
[ApiController]
[Route("api/v1/secrets")]
[Produces("application/json")]
[Authorize]
public class SecretsController : ControllerBase
{
    private readonly ISecretService _secretService;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<SecretsController> _logger;

    public SecretsController(
        ISecretService secretService,
        IEncryptionService encryptionService,
        ILogger<SecretsController> logger)
    {
        _secretService = secretService;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    /// <summary>
    /// Write a secret
    /// </summary>
    [HttpPost("data/{*path}")]
    [ProducesResponseType(typeof(WriteSecretResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> WriteSecret(
        string path,
        [FromBody] WriteSecretRequest request)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var version = await _secretService.WriteSecretAsync(
                path,
                request.Data,
                request.Cas,
                userId);

            _logger.LogInformation("Secret written: {Path} (version {Version}) by user {UserId}",
                path, version.Version, userId);

            return Ok(new WriteSecretResponse
            {
                Version = version.Version,
                CreatedAt = version.CreatedAt
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Write secret failed: {Error}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Read the latest version of a secret
    /// </summary>
    [HttpGet("data/{*path}")]
    [ProducesResponseType(typeof(ReadSecretResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReadSecret(string path)
    {
        var data = await _secretService.ReadSecretAsync(path);

        if (data == null)
        {
            return NotFound(new { error = "Secret not found" });
        }

        _logger.LogInformation("Secret read: {Path}", path);

        return Ok(new ReadSecretResponse { Data = data });
    }

    /// <summary>
    /// Read a specific version of a secret
    /// </summary>
    [HttpGet("data/{path}/version/{version:int}")]
    [ProducesResponseType(typeof(ReadSecretResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReadSecretVersion(string path, int version)
    {
        var data = await _secretService.ReadSecretVersionAsync(path, version);

        if (data == null)
        {
            return NotFound(new { error = "Secret version not found" });
        }

        _logger.LogInformation("Secret version read: {Path} (version {Version})", path, version);

        return Ok(new ReadSecretResponse { Data = data });
    }

    /// <summary>
    /// List secrets at a path
    /// </summary>
    [HttpGet("list/{*path}")]
    [ProducesResponseType(typeof(ListSecretsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListSecrets(string path)
    {
        var secrets = await _secretService.ListSecretsAsync(path);

        return Ok(new ListSecretsResponse { Keys = secrets });
    }

    /// <summary>
    /// Delete the latest version of a secret
    /// </summary>
    [HttpDelete("data/{*path}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSecret(string path)
    {
        try
        {
            await _secretService.DeleteSecretAsync(path);

            _logger.LogInformation("Secret deleted: {Path}", path);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Delete secret failed: {Error}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete specific versions of a secret
    /// </summary>
    [HttpPost("delete/{*path}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSecretVersions(
        string path,
        [FromBody] DeleteVersionsRequest request)
    {
        try
        {
            await _secretService.DeleteSecretVersionsAsync(path, request.Versions);

            _logger.LogInformation("Secret versions deleted: {Path} (versions {Versions})",
                path, string.Join(", ", request.Versions));

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Delete secret versions failed: {Error}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Undelete specific versions of a secret
    /// </summary>
    [HttpPost("undelete/{*path}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UndeleteSecretVersions(
        string path,
        [FromBody] DeleteVersionsRequest request)
    {
        try
        {
            await _secretService.UndeleteSecretVersionsAsync(path, request.Versions);

            _logger.LogInformation("Secret versions undeleted: {Path} (versions {Versions})",
                path, string.Join(", ", request.Versions));

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Undelete secret versions failed: {Error}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Permanently destroy specific versions of a secret
    /// </summary>
    [HttpPost("destroy/{*path}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DestroySecretVersions(
        string path,
        [FromBody] DeleteVersionsRequest request)
    {
        try
        {
            await _secretService.DestroySecretVersionsAsync(path, request.Versions);

            _logger.LogWarning("Secret versions permanently destroyed: {Path} (versions {Versions})",
                path, string.Join(", ", request.Versions));

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Destroy secret versions failed: {Error}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get secret metadata
    /// </summary>
    [HttpGet("metadata/{*path}")]
    [ProducesResponseType(typeof(SecretMetadataResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSecretMetadata(string path)
    {
        var secret = await _secretService.GetSecretMetadataAsync(path);

        if (secret == null)
        {
            return NotFound(new { error = "Secret not found" });
        }

        return Ok(new SecretMetadataResponse
        {
            CurrentVersion = secret.CurrentVersion,
            MaxVersions = secret.MaxVersions,
            CasRequired = secret.CasRequired,
            CreatedAt = secret.CreatedAt,
            UpdatedAt = secret.UpdatedAt,
            Versions = secret.Versions.Select(v => new VersionInfo
            {
                Version = v.Version,
                CreatedAt = v.CreatedAt,
                IsDeleted = v.IsDeleted,
                IsDestroyed = v.IsDestroyed
            }).ToList()
        });
    }

    /// <summary>
    /// Update secret metadata
    /// </summary>
    [HttpPost("metadata/{*path}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSecretMetadata(
        string path,
        [FromBody] UpdateMetadataRequest request)
    {
        try
        {
            await _secretService.UpdateSecretMetadataAsync(
                path,
                request.MaxVersions,
                request.CasRequired);

            _logger.LogInformation("Secret metadata updated: {Path}", path);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Update secret metadata failed: {Error}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Encrypt data using transit engine
    /// </summary>
    [HttpPost("transit/encrypt/{keyName}")]
    [ProducesResponseType(typeof(EncryptResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Encrypt(
        string keyName,
        [FromBody] EncryptRequest request)
    {
        try
        {
            var ciphertext = await _encryptionService.EncryptAsync(
                keyName,
                request.Plaintext,
                request.Context);

            _logger.LogInformation("Data encrypted with key: {KeyName}", keyName);

            return Ok(new EncryptResponse { Ciphertext = ciphertext });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Encryption failed: {Error}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Decrypt data using transit engine
    /// </summary>
    [HttpPost("transit/decrypt/{keyName}")]
    [ProducesResponseType(typeof(DecryptResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Decrypt(
        string keyName,
        [FromBody] DecryptRequest request)
    {
        try
        {
            var plaintext = await _encryptionService.DecryptAsync(
                keyName,
                request.Ciphertext,
                request.Context);

            _logger.LogInformation("Data decrypted with key: {KeyName}", keyName);

            return Ok(new DecryptResponse { Plaintext = plaintext });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Decryption failed: {Error}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Create encryption key
    /// </summary>
    [HttpPost("transit/keys/{keyName}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateKey(
        string keyName,
        [FromBody] CreateKeyRequest? request = null)
    {
        try
        {
            await _encryptionService.CreateKeyAsync(
                keyName,
                request?.Algorithm ?? Core.Domain.Enums.EncryptionAlgorithm.AES256GCM,
                request?.Exportable ?? false);

            _logger.LogInformation("Encryption key created: {KeyName}", keyName);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Create key failed: {Error}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Rotate encryption key
    /// </summary>
    [HttpPost("transit/keys/{keyName}/rotate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RotateKey(string keyName)
    {
        try
        {
            await _encryptionService.RotateKeyAsync(keyName);

            _logger.LogInformation("Encryption key rotated: {KeyName}", keyName);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Rotate key failed: {Error}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
    }
}

// Request/Response DTOs

public record WriteSecretRequest(Dictionary<string, string> Data, int? Cas = null);
public record WriteSecretResponse
{
    public required int Version { get; set; }
    public required DateTime CreatedAt { get; set; }
}

public record ReadSecretResponse
{
    public required Dictionary<string, string> Data { get; set; }
}

public record ListSecretsResponse
{
    public required List<string> Keys { get; set; }
}

public record DeleteVersionsRequest(List<int> Versions);

public record SecretMetadataResponse
{
    public required int CurrentVersion { get; set; }
    public required int MaxVersions { get; set; }
    public required bool CasRequired { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required DateTime UpdatedAt { get; set; }
    public required List<VersionInfo> Versions { get; set; }
}

public record VersionInfo
{
    public required int Version { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required bool IsDeleted { get; set; }
    public required bool IsDestroyed { get; set; }
}

public record UpdateMetadataRequest(int? MaxVersions = null, bool? CasRequired = null);

public record EncryptRequest(string Plaintext, string? Context = null);
public record EncryptResponse
{
    public required string Ciphertext { get; set; }
}

public record DecryptRequest(string Ciphertext, string? Context = null);
public record DecryptResponse
{
    public required string Plaintext { get; set; }
}

public record CreateKeyRequest(Core.Domain.Enums.EncryptionAlgorithm? Algorithm = null, bool Exportable = false);

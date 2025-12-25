using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using USP.Core.Models.Configuration;
using USP.Core.Models.DTOs.Authentication;
using USP.Core.Models.Entities;
using USP.Core.Services.Authentication;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Authentication;

/// <summary>
/// WebAuthn/FIDO2 passwordless authentication service
/// </summary>
public class WebAuthnService : IWebAuthnService
{
    private readonly ApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly ILogger<WebAuthnService> _logger;
    private readonly IDistributedCache _cache;
    private readonly IFido2 _fido2;
    private readonly WebAuthnSettings _settings;

    public WebAuthnService(
        ApplicationDbContext context,
        IJwtService jwtService,
        ILogger<WebAuthnService> logger,
        IDistributedCache cache,
        IFido2 fido2,
        IOptions<WebAuthnSettings> settings)
    {
        _context = context;
        _jwtService = jwtService;
        _logger = logger;
        _cache = cache;
        _fido2 = fido2;
        _settings = settings.Value;
    }

    public async Task<BeginWebAuthnRegistrationResponse> BeginRegistrationAsync(BeginWebAuthnRegistrationRequest request)
    {
        try
        {
            _logger.LogInformation("Beginning WebAuthn registration for user {UserId}", request.UserId);

            var user = await _context.Users.FindAsync(request.UserId);
            if (user == null)
            {
                throw new InvalidOperationException("User not found");
            }

            var existingCredentials = await _context.Set<WebAuthnCredential>()
                .Where(c => c.UserId == user.Id && c.IsActive)
                .ToListAsync();

            var existingKeys = existingCredentials
                .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
                .ToList();

            var fidoUser = new Fido2User
            {
                Id = user.Id.ToByteArray(),
                Name = user.UserName ?? user.Email ?? "user",
                DisplayName = request.DisplayName ?? user.Email ?? "User"
            };

            var authenticatorSelection = new AuthenticatorSelection
            {
                RequireResidentKey = false,
                UserVerification = UserVerificationRequirement.Preferred
            };

            var options = _fido2.RequestNewCredential(
                fidoUser,
                existingKeys,
                authenticatorSelection,
                AttestationConveyancePreference.None);

            var cacheKey = $"webauthn:reg:challenge:{user.Id}";
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_settings.ChallengeExpirationMinutes)
            };
            await _cache.SetStringAsync(cacheKey,
                JsonSerializer.Serialize(options),
                cacheOptions);

            var optionsJson = JsonSerializer.Serialize(options);
            var challengeBase64 = Convert.ToBase64String(options.Challenge);

            _logger.LogInformation("WebAuthn registration options created for user {UserId}", request.UserId);

            return new BeginWebAuthnRegistrationResponse
            {
                OptionsJson = optionsJson,
                Challenge = challengeBase64
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error beginning WebAuthn registration");
            throw;
        }
    }

    public async Task<CompleteWebAuthnRegistrationResponse> CompleteRegistrationAsync(CompleteWebAuthnRegistrationRequest request)
    {
        try
        {
            _logger.LogInformation("Completing WebAuthn registration for user {UserId}", request.UserId);

            var user = await _context.Users.FindAsync(request.UserId);
            if (user == null)
            {
                throw new InvalidOperationException("User not found");
            }

            var cacheKey = $"webauthn:reg:challenge:{user.Id}";
            var storedOptionsJson = await _cache.GetStringAsync(cacheKey);

            if (storedOptionsJson == null)
            {
                throw new InvalidOperationException("Challenge expired or not found");
            }

            var options = JsonSerializer.Deserialize<CredentialCreateOptions>(storedOptionsJson);
            if (options == null)
            {
                throw new InvalidOperationException("Invalid stored options");
            }

            var attestationResponse = new AuthenticatorAttestationRawResponse
            {
                Type = PublicKeyCredentialType.PublicKey,
                Id = Convert.FromBase64String(request.Challenge).Take(16).ToArray(),
                RawId = Convert.FromBase64String(request.Challenge).Take(16).ToArray(),
                Response = new AuthenticatorAttestationRawResponse.ResponseData
                {
                    AttestationObject = Convert.FromBase64String(request.AttestationResponse),
                    ClientDataJson = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                    {
                        type = "webauthn.create",
                        challenge = request.Challenge,
                        origin = _settings.Origin
                    }))
                }
            };

            var success = await _fido2.MakeNewCredentialAsync(
                attestationResponse,
                options,
                async (args, cancellationToken) => true);

            if (success?.Result == null)
            {
                throw new InvalidOperationException("Credential verification failed");
            }

            var credential = new WebAuthnCredential
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                DeviceName = request.DeviceName,
                CredentialId = success.Result.CredentialId,
                PublicKey = success.Result.PublicKey,
                SignatureCounter = success.Result.Counter,
                AaGuid = success.Result.Aaguid.ToString(),
                RegisteredAt = DateTime.UtcNow
            };

            _context.Set<WebAuthnCredential>().Add(credential);
            await _context.SaveChangesAsync();

            await _cache.RemoveAsync(cacheKey);

            _logger.LogInformation("WebAuthn credential registered for user {UserId}, device {DeviceName}",
                request.UserId, request.DeviceName);

            return new CompleteWebAuthnRegistrationResponse
            {
                CredentialId = credential.Id,
                DeviceName = credential.DeviceName,
                Success = true,
                Message = "WebAuthn credential registered successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing WebAuthn registration");
            return new CompleteWebAuthnRegistrationResponse
            {
                Success = false,
                Message = $"Registration failed: {ex.Message}"
            };
        }
    }

    public async Task<BeginWebAuthnAuthenticationResponse> BeginAuthenticationAsync(BeginWebAuthnAuthenticationRequest request)
    {
        try
        {
            _logger.LogInformation("Beginning WebAuthn authentication for user {Username}", request.Username);

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserName == request.Username || u.Email == request.Username);

            if (user == null)
            {
                throw new InvalidOperationException("User not found");
            }

            var credentials = await _context.Set<WebAuthnCredential>()
                .Where(c => c.UserId == user.Id && c.IsActive)
                .ToListAsync();

            if (credentials.Count == 0)
            {
                throw new InvalidOperationException("No WebAuthn credentials registered");
            }

            var allowedCredentials = credentials
                .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
                .ToList();

            var options = _fido2.GetAssertionOptions(
                allowedCredentials,
                UserVerificationRequirement.Preferred);

            var cacheKey = $"webauthn:auth:challenge:{user.Id}";
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_settings.ChallengeExpirationMinutes)
            };
            await _cache.SetStringAsync(cacheKey,
                JsonSerializer.Serialize(options),
                cacheOptions);

            var optionsJson = JsonSerializer.Serialize(options);
            var challengeBase64 = Convert.ToBase64String(options.Challenge);

            _logger.LogInformation("WebAuthn authentication options created for user {Username}", request.Username);

            return new BeginWebAuthnAuthenticationResponse
            {
                OptionsJson = optionsJson,
                Challenge = challengeBase64
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error beginning WebAuthn authentication");
            throw;
        }
    }

    public async Task<CompleteWebAuthnAuthenticationResponse> CompleteAuthenticationAsync(CompleteWebAuthnAuthenticationRequest request)
    {
        try
        {
            _logger.LogInformation("Completing WebAuthn authentication for user {Username}", request.Username);

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserName == request.Username || u.Email == request.Username);

            if (user == null)
            {
                throw new InvalidOperationException("User not found");
            }

            var cacheKey = $"webauthn:auth:challenge:{user.Id}";
            var storedOptionsJson = await _cache.GetStringAsync(cacheKey);

            if (storedOptionsJson == null)
            {
                throw new InvalidOperationException("Challenge expired or not found");
            }

            var options = JsonSerializer.Deserialize<AssertionOptions>(storedOptionsJson);
            if (options == null)
            {
                throw new InvalidOperationException("Invalid stored options");
            }

            var assertionResponse = new AuthenticatorAssertionRawResponse
            {
                Type = PublicKeyCredentialType.PublicKey,
                Id = Convert.FromBase64String(request.Challenge).Take(16).ToArray(),
                RawId = Convert.FromBase64String(request.Challenge).Take(16).ToArray(),
                Response = new AuthenticatorAssertionRawResponse.AssertionResponse
                {
                    AuthenticatorData = Convert.FromBase64String(request.AssertionResponse),
                    Signature = new byte[64],
                    ClientDataJson = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                    {
                        type = "webauthn.get",
                        challenge = request.Challenge,
                        origin = _settings.Origin
                    })),
                    UserHandle = user.Id.ToByteArray()
                }
            };

            var credentialIdBytes = assertionResponse.RawId;
            var credential = await _context.Set<WebAuthnCredential>()
                .FirstOrDefaultAsync(c =>
                    c.UserId == user.Id &&
                    c.CredentialId.SequenceEqual(credentialIdBytes) &&
                    c.IsActive);

            if (credential == null)
            {
                throw new InvalidOperationException("Credential not found");
            }

            var storedPublicKey = credential.PublicKey;
            var storedSignatureCounter = credential.SignatureCounter;

            var result = await _fido2.MakeAssertionAsync(
                assertionResponse,
                options,
                storedPublicKey,
                storedSignatureCounter,
                async (args, cancellationToken) => true);

            if (result?.Status != "ok")
            {
                throw new InvalidOperationException("Assertion verification failed");
            }

            credential.SignatureCounter = result.Counter;
            credential.LastUsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _cache.RemoveAsync(cacheKey);

            var accessToken = _jwtService.GenerateAccessToken(user, new List<string>());
            var refreshToken = GenerateRefreshToken();

            _logger.LogInformation("WebAuthn authentication successful for user {Username}", request.Username);

            return new CompleteWebAuthnAuthenticationResponse
            {
                Success = true,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                Message = "Authentication successful"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing WebAuthn authentication");
            return new CompleteWebAuthnAuthenticationResponse
            {
                Success = false,
                Message = $"Authentication failed: {ex.Message}"
            };
        }
    }

    public async Task<List<WebAuthnCredentialDto>> GetUserCredentialsAsync(Guid userId)
    {
        try
        {
            var credentials = await _context.Set<WebAuthnCredential>()
                .Where(c => c.UserId == userId && c.IsActive)
                .ToListAsync();

            return credentials.Select(c => new WebAuthnCredentialDto
            {
                Id = c.Id,
                DeviceName = c.DeviceName,
                CredentialId = c.CredentialId,
                PublicKey = c.PublicKey,
                SignatureCounter = c.SignatureCounter,
                AaGuid = c.AaGuid,
                RegisteredAt = c.RegisteredAt,
                LastUsedAt = c.LastUsedAt
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting WebAuthn credentials for user {UserId}", userId);
            return new List<WebAuthnCredentialDto>();
        }
    }

    public async Task<bool> RemoveCredentialAsync(Guid userId, Guid credentialId)
    {
        try
        {
            var credential = await _context.Set<WebAuthnCredential>()
                .FirstOrDefaultAsync(c => c.UserId == userId && c.Id == credentialId);

            if (credential == null)
            {
                return false;
            }

            credential.IsActive = false;
            await _context.SaveChangesAsync();

            _logger.LogInformation("WebAuthn credential {CredentialId} removed for user {UserId}",
                credentialId, userId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing WebAuthn credential");
            return false;
        }
    }

    #region Private Helper Methods

    private static string GenerateRefreshToken()
    {
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return Convert.ToBase64String(bytes);
    }

    #endregion
}

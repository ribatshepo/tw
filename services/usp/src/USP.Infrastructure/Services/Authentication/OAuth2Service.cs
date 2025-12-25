using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.Authentication;
using USP.Core.Models.Entities;
using USP.Core.Services.Authentication;
using USP.Infrastructure.Data;
using BCrypt.Net;

namespace USP.Infrastructure.Services.Authentication;

/// <summary>
/// OAuth 2.0 authorization server implementation with PKCE support
/// </summary>
public class OAuth2Service : IOAuth2Service
{
    private readonly ApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly ILogger<OAuth2Service> _logger;

    public OAuth2Service(
        ApplicationDbContext context,
        IJwtService jwtService,
        ILogger<OAuth2Service> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _logger = logger;
    }

    public async Task<OAuth2AuthorizationResponse> AuthorizeAsync(OAuth2AuthorizationRequest request, Guid userId)
    {
        try
        {
            _logger.LogInformation("Creating OAuth2 authorization code for user {UserId}, client {ClientId}",
                userId, request.ClientId);

            // Validate client
            var client = await _context.Set<OAuth2Client>()
                .FirstOrDefaultAsync(c => c.ClientId == request.ClientId && c.IsActive);

            if (client == null)
            {
                throw new InvalidOperationException("Invalid client");
            }

            // Validate redirect URI
            if (!client.RedirectUris.Contains(request.RedirectUri))
            {
                throw new InvalidOperationException("Invalid redirect URI");
            }

            // Validate PKCE if required
            if (client.RequirePkce && string.IsNullOrEmpty(request.CodeChallenge))
            {
                throw new InvalidOperationException("PKCE code challenge required");
            }

            // Generate authorization code
            var code = GenerateAuthorizationCode();

            // Store authorization code
            var authCode = new OAuth2AuthorizationCode
            {
                Id = Guid.NewGuid(),
                Code = code,
                ClientId = client.Id,
                UserId = userId,
                RedirectUri = request.RedirectUri,
                Scope = request.Scope ?? "openid profile",
                CodeChallenge = request.CodeChallenge,
                CodeChallengeMethod = request.CodeChallengeMethod ?? "S256",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10) // 10 minute expiry
            };

            _context.Set<OAuth2AuthorizationCode>().Add(authCode);
            await _context.SaveChangesAsync();

            _logger.LogInformation("OAuth2 authorization code created for client {ClientId}", request.ClientId);

            return new OAuth2AuthorizationResponse
            {
                Code = code,
                State = request.State,
                RedirectUri = request.RedirectUri
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating OAuth2 authorization code");
            throw;
        }
    }

    public async Task<OAuth2TokenResponse> ExchangeCodeForTokenAsync(OAuth2TokenRequest request)
    {
        try
        {
            _logger.LogInformation("Exchanging OAuth2 code for token, client {ClientId}", request.ClientId);

            // Find authorization code
            var authCode = await _context.Set<OAuth2AuthorizationCode>()
                .Include(ac => ac.Client)
                .Include(ac => ac.User)
                .FirstOrDefaultAsync(ac => ac.Code == request.Code && !ac.IsUsed);

            if (authCode == null)
            {
                throw new InvalidOperationException("Invalid or expired authorization code");
            }

            // Check expiration
            if (authCode.ExpiresAt < DateTime.UtcNow)
            {
                throw new InvalidOperationException("Authorization code expired");
            }

            // Validate client
            if (authCode.Client.ClientId != request.ClientId)
            {
                throw new InvalidOperationException("Client mismatch");
            }

            // Validate redirect URI
            if (authCode.RedirectUri != request.RedirectUri)
            {
                throw new InvalidOperationException("Redirect URI mismatch");
            }

            // Validate PKCE if present
            if (!string.IsNullOrEmpty(authCode.CodeChallenge))
            {
                if (string.IsNullOrEmpty(request.CodeVerifier))
                {
                    throw new InvalidOperationException("Code verifier required");
                }

                if (!VerifyPkce(request.CodeVerifier, authCode.CodeChallenge, authCode.CodeChallengeMethod ?? "S256"))
                {
                    throw new InvalidOperationException("Invalid code verifier");
                }
            }

            // Mark code as used
            authCode.IsUsed = true;
            await _context.SaveChangesAsync();

            var scopes = authCode.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            var accessToken = _jwtService.GenerateAccessToken(authCode.User, scopes);

            _logger.LogInformation("OAuth2 token issued for user {UserId}", authCode.UserId);

            return new OAuth2TokenResponse
            {
                AccessToken = accessToken,
                TokenType = "Bearer",
                ExpiresIn = 3600,
                Scope = authCode.Scope
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging OAuth2 code for token");
            throw;
        }
    }

    public async Task<bool> ValidateClientAsync(string clientId, string? clientSecret = null)
    {
        try
        {
            var client = await _context.Set<OAuth2Client>()
                .FirstOrDefaultAsync(c => c.ClientId == clientId && c.IsActive);

            if (client == null)
            {
                return false;
            }

            // If client secret provided, verify it
            if (!string.IsNullOrEmpty(clientSecret))
            {
                return BCrypt.Net.BCrypt.Verify(clientSecret, client.ClientSecret);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating OAuth2 client");
            return false;
        }
    }

    #region Private Helper Methods

    private static string GenerateAuthorizationCode()
    {
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    private static bool VerifyPkce(string verifier, string challenge, string method)
    {
        if (method == "plain")
        {
            return verifier == challenge;
        }
        else if (method == "S256")
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(verifier));
            var computedChallenge = Convert.ToBase64String(hash)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
            return computedChallenge == challenge;
        }

        return false;
    }

    #endregion
}

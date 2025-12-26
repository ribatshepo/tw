using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using USP.Core.Interfaces.Services.Authentication;
using USP.Shared.Configuration.Options;

namespace USP.Infrastructure.Services.Authentication;

/// <summary>
/// Provides JWT token generation, validation, and refresh token operations using RS256 signing.
/// </summary>
public class TokenService : ITokenService
{
    private readonly JwtOptions _jwtOptions;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private RSA? _rsaPrivateKey;
    private RSA? _rsaPublicKey;

    public TokenService(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
        _tokenHandler = new JwtSecurityTokenHandler();

        // Validate required configuration
        ValidateConfiguration();
    }

    private void ValidateConfiguration()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(_jwtOptions.PrivateKeyPath))
        {
            errors.Add("JWT PrivateKeyPath is required. Set environment variable JWT_PRIVATE_KEY_PATH or configure in appsettings.json");
        }

        if (string.IsNullOrWhiteSpace(_jwtOptions.PublicKeyPath))
        {
            errors.Add("JWT PublicKeyPath is required. Set environment variable JWT_PUBLIC_KEY_PATH or configure in appsettings.json");
        }

        if (errors.Any())
        {
            throw new InvalidOperationException(
                $"JWT configuration is incomplete:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
        }
    }

    /// <summary>
    /// Generates a JWT access token using RS256 signing.
    /// </summary>
    public async Task<string> GenerateAccessTokenAsync(
        string userId,
        string email,
        IEnumerable<string> roles,
        IDictionary<string, string>? additionalClaims = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        // Add roles
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        // Add additional claims
        if (additionalClaims != null)
        {
            foreach (var claim in additionalClaims)
            {
                claims.Add(new Claim(claim.Key, claim.Value));
            }
        }

        var privateKey = await GetPrivateKeyAsync();
        var credentials = new SigningCredentials(
            new RsaSecurityKey(privateKey),
            SecurityAlgorithms.RsaSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes),
            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,
            SigningCredentials = credentials
        };

        var token = _tokenHandler.CreateToken(tokenDescriptor);
        return _tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// Generates a cryptographically secure refresh token.
    /// </summary>
    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[_jwtOptions.RefreshTokenLength];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    /// <summary>
    /// Validates a JWT token signature, expiration, issuer, and audience.
    /// </summary>
    public async Task<Core.Interfaces.Services.Authentication.TokenValidationResult> ValidateTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new Core.Interfaces.Services.Authentication.TokenValidationResult
            {
                IsValid = false,
                Error = "Token is null or empty"
            };
        }

        try
        {
            var publicKey = await GetPublicKeyAsync();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _jwtOptions.Issuer,
                ValidAudience = _jwtOptions.Audience,
                IssuerSigningKey = new RsaSecurityKey(publicKey),
                ClockSkew = TimeSpan.FromMinutes(5) // Allow 5 minute clock skew
            };

            var principal = _tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

            return new Core.Interfaces.Services.Authentication.TokenValidationResult
            {
                IsValid = true,
                Principal = principal
            };
        }
        catch (SecurityTokenExpiredException)
        {
            return new Core.Interfaces.Services.Authentication.TokenValidationResult
            {
                IsValid = false,
                Error = "Token has expired"
            };
        }
        catch (SecurityTokenInvalidSignatureException)
        {
            return new Core.Interfaces.Services.Authentication.TokenValidationResult
            {
                IsValid = false,
                Error = "Invalid token signature"
            };
        }
        catch (Exception ex)
        {
            return new Core.Interfaces.Services.Authentication.TokenValidationResult
            {
                IsValid = false,
                Error = $"Token validation failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Extracts claims from a JWT token without validating expiration.
    /// </summary>
    public ClaimsPrincipal? ExtractClaimsWithoutValidation(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var jwtToken = _tokenHandler.ReadJwtToken(token);
            var identity = new ClaimsIdentity(jwtToken.Claims, "jwt");
            return new ClaimsPrincipal(identity);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the expiration time for access tokens in seconds.
    /// </summary>
    public int GetAccessTokenExpirationSeconds()
    {
        return _jwtOptions.ExpirationMinutes * 60;
    }

    private async Task<RSA> GetPrivateKeyAsync()
    {
        if (_rsaPrivateKey != null)
        {
            return _rsaPrivateKey;
        }

        if (!File.Exists(_jwtOptions.PrivateKeyPath))
        {
            throw new FileNotFoundException(
                $"JWT private key file not found at: {_jwtOptions.PrivateKeyPath}");
        }

        var pemKey = await File.ReadAllTextAsync(_jwtOptions.PrivateKeyPath);
        _rsaPrivateKey = RSA.Create();
        _rsaPrivateKey.ImportFromPem(pemKey);
        return _rsaPrivateKey;
    }

    private async Task<RSA> GetPublicKeyAsync()
    {
        if (_rsaPublicKey != null)
        {
            return _rsaPublicKey;
        }

        if (!File.Exists(_jwtOptions.PublicKeyPath))
        {
            throw new FileNotFoundException(
                $"JWT public key file not found at: {_jwtOptions.PublicKeyPath}");
        }

        var pemKey = await File.ReadAllTextAsync(_jwtOptions.PublicKeyPath);
        _rsaPublicKey = RSA.Create();
        _rsaPublicKey.ImportFromPem(pemKey);
        return _rsaPublicKey;
    }
}

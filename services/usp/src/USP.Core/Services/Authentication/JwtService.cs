using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using USP.Core.Models.Entities;

namespace USP.Core.Services.Authentication;

/// <summary>
/// JWT service implementation with support for HS256 and RS256 algorithms
/// </summary>
public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly string _algorithm;
    private readonly SymmetricSecurityKey? _symmetricKey;
    private readonly RsaSecurityKey? _rsaKey;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
        _tokenHandler = new JwtSecurityTokenHandler();
        _algorithm = configuration["Jwt:Algorithm"] ?? "HS256";

        if (_algorithm == "HS256")
        {
            var secret = configuration["Jwt:Secret"]
                ?? throw new InvalidOperationException("JWT secret is required for HS256 algorithm");
            _symmetricKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        }
        else if (_algorithm == "RS256")
        {
            var privateKeyPath = configuration["Jwt:PrivateKeyPath"]
                ?? throw new InvalidOperationException("Private key path is required for RS256 algorithm");

            var privateKeyPem = File.ReadAllText(privateKeyPath);
            var rsa = RSA.Create();
            rsa.ImportFromPem(privateKeyPem);
            _rsaKey = new RsaSecurityKey(rsa);
        }
        else
        {
            throw new NotSupportedException($"Algorithm {_algorithm} is not supported");
        }
    }

    public string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        if (!string.IsNullOrEmpty(user.FirstName))
        {
            claims.Add(new Claim(ClaimTypes.GivenName, user.FirstName));
        }

        if (!string.IsNullOrEmpty(user.LastName))
        {
            claims.Add(new Claim(ClaimTypes.Surname, user.LastName));
        }

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var credentials = _algorithm == "HS256"
            ? new SigningCredentials(_symmetricKey, SecurityAlgorithms.HmacSha256)
            : new SigningCredentials(_rsaKey, SecurityAlgorithms.RsaSha256);

        var expirationMinutes = int.Parse(_configuration["Jwt:AccessTokenExpirationMinutes"] ?? "15");
        var expires = DateTime.UtcNow.AddMinutes(expirationMinutes);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: credentials
        );

        return _tokenHandler.WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidAudience = _configuration["Jwt:Audience"],
                IssuerSigningKey = _algorithm == "HS256" ? _symmetricKey : _rsaKey,
                ClockSkew = TimeSpan.Zero
            };

            var principal = _tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

            if (validatedToken is not JwtSecurityToken jwtToken ||
                !IsValidAlgorithm(jwtToken))
            {
                return null;
            }

            return principal;
        }
        catch (SecurityTokenException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public Guid? GetUserIdFromClaims(ClaimsPrincipal principal)
    {
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)
            ?? principal.FindFirst(JwtRegisteredClaimNames.Sub);

        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return null;
        }

        return userId;
    }

    public string HashToken(string token)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hashBytes);
    }

    private bool IsValidAlgorithm(JwtSecurityToken token)
    {
        return _algorithm switch
        {
            "HS256" => token.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.Ordinal),
            "RS256" => token.Header.Alg.Equals(SecurityAlgorithms.RsaSha256, StringComparison.Ordinal),
            _ => false
        };
    }
}

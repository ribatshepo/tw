using Grpc.Core;
using Microsoft.AspNetCore.Identity;
using USP.Core.Models.Entities;
using USP.Core.Services.Authentication;
using USP.Core.Services.Mfa;
using USP.Grpc.Authentication;
using Microsoft.EntityFrameworkCore;
using USP.Infrastructure.Data;

namespace USP.Api.Grpc;

/// <summary>
/// gRPC Authentication Service implementation
/// </summary>
public class GrpcAuthenticationService : AuthenticationService.AuthenticationServiceBase
{
    private readonly IJwtService _jwtService;
    private readonly IMfaService _mfaService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<GrpcAuthenticationService> _logger;

    public GrpcAuthenticationService(
        IJwtService jwtService,
        IMfaService mfaService,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        ILogger<GrpcAuthenticationService> logger)
    {
        _jwtService = jwtService;
        _mfaService = mfaService;
        _userManager = userManager;
        _context = context;
        _logger = logger;
    }

    public override async Task<ValidateTokenResponse> ValidateToken(
        ValidateTokenRequest request,
        ServerCallContext context)
    {
        try
        {
            var principal = _jwtService.ValidateToken(request.Token);

            if (principal == null)
            {
                return new ValidateTokenResponse
                {
                    IsValid = false,
                    ErrorMessage = "Invalid or expired token"
                };
            }

            var userId = _jwtService.GetUserIdFromClaims(principal);
            var username = principal.Identity?.Name ?? string.Empty;
            var email = principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? string.Empty;

            var response = new ValidateTokenResponse
            {
                IsValid = true,
                UserId = userId?.ToString() ?? string.Empty,
                Username = username,
                Email = email
            };

            // Get roles
            var roles = principal.FindAll(System.Security.Claims.ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();
            response.Roles.AddRange(roles);

            // Include all claims if requested
            if (request.IncludeClaims)
            {
                foreach (var claim in principal.Claims)
                {
                    if (!response.Claims.ContainsKey(claim.Type))
                    {
                        response.Claims[claim.Type] = claim.Value;
                    }
                }
            }

            // Get token expiration
            var exp = principal.FindFirst("exp")?.Value;
            if (long.TryParse(exp, out var expTimestamp))
            {
                response.ExpiresAt = expTimestamp;
            }

            _logger.LogDebug("Token validated for user: {Username}", username);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating token");
            return new ValidateTokenResponse
            {
                IsValid = false,
                ErrorMessage = "Token validation failed"
            };
        }
    }

    public override async Task<GenerateServiceTokenResponse> GenerateServiceToken(
        GenerateServiceTokenRequest request,
        ServerCallContext context)
    {
        try
        {
            // Create a service user for token generation
            var serviceUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"service:{request.ServiceName}",
                Email = $"{request.ServiceName}@service.local"
            };

            var roles = request.Scopes.Select(s => $"service:{s}").ToList();

            var token = _jwtService.GenerateAccessToken(serviceUser, roles);

            var response = new GenerateServiceTokenResponse
            {
                AccessToken = token,
                TokenType = "Bearer",
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(request.ExpirationMinutes > 0 ? request.ExpirationMinutes : 60).ToUnixTimeSeconds()
            };

            _logger.LogInformation("Service token generated for: {ServiceName}", request.ServiceName);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating service token for {ServiceName}", request.ServiceName);
            throw new RpcException(new Status(StatusCode.Internal, "Failed to generate service token"));
        }
    }

    public override async Task<VerifyCredentialsResponse> VerifyCredentials(
        VerifyCredentialsRequest request,
        ServerCallContext context)
    {
        try
        {
            var user = await _userManager.FindByNameAsync(request.Username);

            if (user == null)
            {
                return new VerifyCredentialsResponse
                {
                    IsValid = false,
                    ErrorMessage = "Invalid credentials"
                };
            }

            var isValid = await _userManager.CheckPasswordAsync(user, request.Password);

            if (!isValid)
            {
                return new VerifyCredentialsResponse
                {
                    IsValid = false,
                    ErrorMessage = "Invalid credentials"
                };
            }

            var response = new VerifyCredentialsResponse
            {
                IsValid = true,
                UserId = user.Id.ToString(),
                MfaRequired = user.MfaEnabled
            };

            // If MFA is enabled and code is provided, verify it
            if (user.MfaEnabled && !string.IsNullOrEmpty(request.MfaCode))
            {
                var mfaValid = await _mfaService.VerifyTotpCodeAsync(user.Id, request.MfaCode);

                if (!mfaValid)
                {
                    return new VerifyCredentialsResponse
                    {
                        IsValid = false,
                        ErrorMessage = "Invalid MFA code"
                    };
                }

                response.MfaRequired = false;
            }

            _logger.LogInformation("Credentials verified for user: {Username}", request.Username);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying credentials for {Username}", request.Username);
            throw new RpcException(new Status(StatusCode.Internal, "Failed to verify credentials"));
        }
    }

    public override async Task<GetUserInfoResponse> GetUserInfo(
        GetUserInfoRequest request,
        ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.UserId, out var userId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user ID"));
            }

            var user = await _userManager.FindByIdAsync(userId.ToString());

            if (user == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "User not found"));
            }

            var roles = await _userManager.GetRolesAsync(user);

            var response = new GetUserInfoResponse
            {
                UserId = user.Id.ToString(),
                Username = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty,
                Status = user.Status,
                MfaEnabled = user.MfaEnabled
            };

            response.Roles.AddRange(roles);

            return response;
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user info for {UserId}", request.UserId);
            throw new RpcException(new Status(StatusCode.Internal, "Failed to get user info"));
        }
    }

    public override async Task<RevokeTokenResponse> RevokeToken(
        RevokeTokenRequest request,
        ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.UserId, out var userId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user ID"));
            }

            var sessionsRevoked = await _context.Sessions
                .Where(s => s.UserId == userId && !s.Revoked)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Revoked, true)
                    .SetProperty(x => x.RevokedAt, DateTime.UtcNow));

            _logger.LogInformation("Token revocation completed for user {UserId}: {Count} sessions invalidated",
                request.UserId, sessionsRevoked);

            return new RevokeTokenResponse
            {
                Success = true,
                Message = $"{sessionsRevoked} active session(s) revoked"
            };
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking token for user {UserId}", request.UserId);
            throw new RpcException(new Status(StatusCode.Internal, "Failed to revoke token"));
        }
    }
}

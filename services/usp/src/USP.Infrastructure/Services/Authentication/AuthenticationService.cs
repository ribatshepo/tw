using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.Authentication;
using USP.Core.Models.Entities;
using USP.Core.Services.Authentication;
using USP.Core.Services.Device;
using USP.Core.Services.Mfa;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Authentication;

/// <summary>
/// Authentication service implementation
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtService _jwtService;
    private readonly IMfaService _mfaService;
    private readonly IDeviceFingerprintService _deviceService;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IJwtService jwtService,
        IMfaService mfaService,
        IDeviceFingerprintService deviceService,
        ILogger<AuthenticationService> logger)
    {
        _context = context;
        _userManager = userManager;
        _jwtService = jwtService;
        _mfaService = mfaService;
        _deviceService = deviceService;
        _logger = logger;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, string ipAddress, string userAgent)
    {
        var user = await _userManager.FindByNameAsync(request.Username);

        if (user == null)
        {
            _logger.LogWarning("Login failed: User {Username} not found", request.Username);
            throw new UnauthorizedAccessException("Invalid username or password");
        }

        if (user.Status != "active")
        {
            _logger.LogWarning("Login failed: User {UserId} status is {Status}", user.Id, user.Status);
            throw new UnauthorizedAccessException($"Account is {user.Status}");
        }

        if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
        {
            _logger.LogWarning("Login failed: User {UserId} is locked until {LockedUntil}", user.Id, user.LockedUntil);
            throw new UnauthorizedAccessException("Account is temporarily locked");
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);

        if (!passwordValid)
        {
            await HandleFailedLoginAsync(user, ipAddress);
            throw new UnauthorizedAccessException("Invalid username or password");
        }

        // Check if device is trusted
        var deviceFingerprint = _deviceService.GenerateFingerprint(userAgent, ipAddress);
        var isTrustedDevice = await _deviceService.IsTrustedDeviceAsync(user.Id, deviceFingerprint);

        // Require MFA if enabled and device is not trusted
        if (user.MfaEnabled && !isTrustedDevice && string.IsNullOrEmpty(request.MfaCode))
        {
            _logger.LogInformation("MFA required for user {UserId} on untrusted device", user.Id);
            return new LoginResponse
            {
                MfaRequired = true,
                User = MapToUserInfo(user, new List<string>())
            };
        }

        if (user.MfaEnabled && !isTrustedDevice && !await VerifyMfaCodeAsync(user.Id, request.MfaCode!))
        {
            await HandleFailedLoginAsync(user, ipAddress);
            throw new UnauthorizedAccessException("Invalid MFA code");
        }

        await ResetFailedLoginAttemptsAsync(user);

        // Update trusted device last used timestamp
        if (isTrustedDevice)
        {
            await _deviceService.UpdateDeviceLastUsedAsync(user.Id, deviceFingerprint);
            _logger.LogInformation("User {UserId} logged in from trusted device", user.Id);
        }

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _jwtService.GenerateAccessToken(user, roles);
        var refreshToken = _jwtService.GenerateRefreshToken();

        await CreateSessionAsync(user.Id, accessToken, refreshToken, ipAddress, userAgent);

        _logger.LogInformation("User {UserId} logged in successfully", user.Id);

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            User = MapToUserInfo(user, roles.ToList())
        };
    }

    public async Task<LoginResponse> RegisterAsync(RegisterRequest request, string ipAddress, string userAgent)
    {
        var existingUser = await _userManager.FindByNameAsync(request.Username);
        if (existingUser != null)
        {
            throw new InvalidOperationException("Username already exists");
        }

        existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            throw new InvalidOperationException("Email already exists");
        }

        var user = new ApplicationUser
        {
            UserName = request.Username,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogError("User registration failed: {Errors}", errors);
            throw new InvalidOperationException($"Registration failed: {errors}");
        }

        await _userManager.AddToRoleAsync(user, "User");

        _logger.LogInformation("User {UserId} registered successfully", user.Id);

        return await LoginAsync(new LoginRequest
        {
            Username = request.Username,
            Password = request.Password
        }, ipAddress, userAgent);
    }

    public async Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request, string ipAddress, string userAgent)
    {
        var tokenHash = _jwtService.HashToken(request.RefreshToken);

        var session = await _context.Sessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.RefreshTokenHash == tokenHash && !s.Revoked);

        if (session == null || session.ExpiresAt < DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException("Invalid or expired refresh token");
        }

        session.Revoked = true;
        session.RevokedAt = DateTime.UtcNow;

        var user = session.User;
        var roles = await _userManager.GetRolesAsync(user);

        var newAccessToken = _jwtService.GenerateAccessToken(user, roles);
        var newRefreshToken = _jwtService.GenerateRefreshToken();

        await CreateSessionAsync(user.Id, newAccessToken, newRefreshToken, ipAddress, userAgent);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Token refreshed for user {UserId}", user.Id);

        return new LoginResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            User = MapToUserInfo(user, roles.ToList())
        };
    }

    public async Task LogoutAsync(Guid userId, string token)
    {
        var tokenHash = _jwtService.HashToken(token);

        var session = await _context.Sessions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.TokenHash == tokenHash && !s.Revoked);

        if (session != null)
        {
            session.Revoked = true;
            session.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation("User {UserId} logged out", userId);
    }

    public async Task<bool> VerifyMfaCodeAsync(Guid userId, string code)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user == null || !user.MfaEnabled)
        {
            return false;
        }

        // Try TOTP verification first
        var totpValid = await _mfaService.VerifyTotpCodeAsync(userId, code);
        if (totpValid)
        {
            return true;
        }

        // If TOTP fails, try backup code
        var backupCodeValid = await _mfaService.VerifyBackupCodeAsync(userId, code);
        return backupCodeValid;
    }

    private async Task CreateSessionAsync(Guid userId, string accessToken, string refreshToken, string ipAddress, string userAgent)
    {
        var session = new Session
        {
            UserId = userId,
            TokenHash = _jwtService.HashToken(accessToken),
            RefreshTokenHash = _jwtService.HashToken(refreshToken),
            IpAddress = IPAddress.TryParse(ipAddress, out var ip) ? ip : null,
            UserAgent = userAgent,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow
        };

        _context.Sessions.Add(session);
        await _context.SaveChangesAsync();
    }

    private async Task HandleFailedLoginAsync(ApplicationUser user, string ipAddress)
    {
        user.FailedLoginAttempts++;
        user.LastFailedLogin = DateTime.UtcNow;

        var maxAttempts = 5;
        if (user.FailedLoginAttempts >= maxAttempts)
        {
            user.LockedUntil = DateTime.UtcNow.AddMinutes(15);
            _logger.LogWarning("User {UserId} locked due to {Attempts} failed login attempts from {IpAddress}",
                user.Id, user.FailedLoginAttempts, ipAddress);
        }

        await _userManager.UpdateAsync(user);
    }

    private async Task ResetFailedLoginAttemptsAsync(ApplicationUser user)
    {
        if (user.FailedLoginAttempts > 0)
        {
            user.FailedLoginAttempts = 0;
            user.LastFailedLogin = null;
            user.LockedUntil = null;
            await _userManager.UpdateAsync(user);
        }
    }

    private static UserInfo MapToUserInfo(ApplicationUser user, List<string> roles)
    {
        return new UserInfo
        {
            Id = user.Id,
            Username = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Roles = roles
        };
    }
}

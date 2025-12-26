using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using USP.Core.Models.Entities;
using USP.Core.Services.Communication;
using USP.Infrastructure.Data;
using USP.Infrastructure.Services.Mfa;
using Xunit;

namespace USP.UnitTests.Services.Mfa;

/// <summary>
/// Unit tests for MfaService focusing on production-ready requirements
/// </summary>
public class MfaServiceProductionTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<MfaService>> _loggerMock;
    private readonly Mock<ISmsService> _smsServiceMock;
    private readonly IMemoryCache _cache;
    private readonly MfaService _service;

    public MfaServiceProductionTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"MfaServiceTestDb_{Guid.NewGuid()}")
            .Options;
        _context = new ApplicationDbContext(options);

        // Setup UserManager mock
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null, null, null, null, null, null, null, null);

        // Setup other mocks
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<MfaService>>();
        _smsServiceMock = new Mock<ISmsService>();
        _cache = new MemoryCache(new MemoryCacheOptions());

        _service = new MfaService(
            _context,
            _userManagerMock.Object,
            _configurationMock.Object,
            _loggerMock.Object,
            _smsServiceMock.Object,
            _cache);
    }

    [Fact]
    public async Task SendPushNotificationAsync_ThrowsNotSupportedException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var message = "Login attempt from new device";
        var actionType = "approve";

        // Act
        Func<Task> act = async () => await _service.SendPushNotificationAsync(userId, message, actionType);

        // Assert
        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*Firebase Cloud Messaging*")
            .WithMessage("*Apple Push Notification Service*");
    }

    [Fact]
    public async Task SendPushNotificationAsync_ErrorMessageContainsConfiguration()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        Func<Task> act = async () => await _service.SendPushNotificationAsync(userId, "test", "approve");

        // Assert
        var exception = await act.Should().ThrowAsync<NotSupportedException>();
        exception.Which.Message.Should().Contain("MfaSettings:PushNotificationProvider");
        exception.Which.Message.Should().Contain("appsettings.json");
    }

    [Fact]
    public async Task SendPushNotificationAsync_ErrorMessageContainsSupportedProviders()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        Func<Task> act = async () => await _service.SendPushNotificationAsync(userId, "test");

        // Assert
        var exception = await act.Should().ThrowAsync<NotSupportedException>();
        exception.Which.Message.Should().Contain("FCM");
        exception.Which.Message.Should().Contain("APNS");
    }

    [Fact]
    public async Task VerifyHardwareTokenAsync_ThrowsNotSupportedException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otp = "cccccccbcjhiegbrnnlgedjkrkhcvjbdjnfhfjhvuulu"; // Example YubiKey OTP

        // Act
        Func<Task> act = async () => await _service.VerifyHardwareTokenAsync(userId, otp);

        // Assert
        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*YubiKey OTP validation*")
            .WithMessage("*Yubico API*");
    }

    [Fact]
    public async Task VerifyHardwareTokenAsync_ErrorMessageContainsYubicoConfiguration()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otp = "123456";

        // Act
        Func<Task> act = async () => await _service.VerifyHardwareTokenAsync(userId, otp);

        // Assert
        var exception = await act.Should().ThrowAsync<NotSupportedException>();
        exception.Which.Message.Should().Contain("MfaSettings:YubicoClientId");
        exception.Which.Message.Should().Contain("MfaSettings:YubicoSecretKey");
    }

    [Fact]
    public async Task VerifyHardwareTokenAsync_ErrorMessageContainsGetApiKeyUrl()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otp = "test-otp";

        // Act
        Func<Task> act = async () => await _service.VerifyHardwareTokenAsync(userId, otp);

        // Assert
        var exception = await act.Should().ThrowAsync<NotSupportedException>();
        exception.Which.Message.Should().Contain("https://upgrade.yubico.com/getapikey/");
    }

    [Fact]
    public void MfaService_DoesNotContainPlaceholderImplementations()
    {
        // Arrange & Act
        var serviceType = typeof(MfaService);
        var sourceCode = System.IO.File.ReadAllText(
            "/home/tshepo/projects/tw/services/usp/src/USP.Infrastructure/Services/Mfa/MfaService.cs");

        // Assert - No "For now" comments
        sourceCode.Should().NotContain("For now", "service should not have placeholder implementations");
        sourceCode.Should().NotContain("In production", "service should not have conditional production logic");
    }

    [Fact]
    public async Task EnrollTotpAsync_WorksCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "testuser",
            Email = "test@example.com"
        };

        _userManagerMock.Setup(um => um.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);
        _userManagerMock.Setup(um => um.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);
        _configurationMock.Setup(c => c["Jwt:Issuer"]).Returns("USP-Test");

        // Act
        var response = await _service.EnrollTotpAsync(userId, "Authenticator App");

        // Assert
        response.Should().NotBeNull();
        response.Secret.Should().NotBeNullOrEmpty();
        response.QrCodeDataUrl.Should().NotBeNullOrEmpty();
        response.QrCodeDataUrl.Should().StartWith("data:image/png;base64,");
        response.ManualEntryKey.Should().NotBeNullOrEmpty();
        response.Issuer.Should().Be("USP-Test");
    }

    [Fact]
    public async Task VerifyTotpCodeAsync_WithValidCode_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var secret = OtpNet.KeyGeneration.GenerateRandomKey(20);
        var base32Secret = OtpNet.Base32Encoding.ToString(secret);

        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "testuser",
            Email = "test@example.com",
            MfaSecret = base32Secret,
            MfaEnabled = true
        };

        var device = new Core.Models.Entities.MfaDevice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DeviceType = "TOTP",
            DeviceName = "Authenticator",
            IsActive = true,
            RegisteredAt = DateTime.UtcNow
        };

        await _context.MfaDevices.AddAsync(device);
        await _context.SaveChangesAsync();

        _userManagerMock.Setup(um => um.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);

        // Generate valid TOTP code
        var totp = new OtpNet.Totp(secret);
        var validCode = totp.ComputeTotp();

        // Act
        var result = await _service.VerifyTotpCodeAsync(userId, validCode);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateBackupCodesAsync_GeneratesCorrectNumberOfCodes()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "testuser",
            Email = "test@example.com",
            MfaEnabled = true
        };

        _userManagerMock.Setup(um => um.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);

        // Act
        var response = await _service.GenerateBackupCodesAsync(userId);

        // Assert
        response.Should().NotBeNull();
        response.BackupCodes.Should().HaveCount(10);
        response.TotalCodes.Should().Be(10);
        foreach (var code in response.BackupCodes)
        {
            code.Should().MatchRegex(@"^[A-Z0-9]{4}-[A-Z0-9]{4}$");
        }
    }

    [Fact]
    public async Task VerifyBackupCodeAsync_WithValidCode_MarksCodeAsUsed()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var code = "ABCD-1234";
        var codeHash = ComputeBackupCodeHash(code);

        var backupCode = new Core.Models.Entities.MfaBackupCode
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CodeHash = codeHash,
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        await _context.MfaBackupCodes.AddAsync(backupCode);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.VerifyBackupCodeAsync(userId, code);

        // Assert
        result.Should().BeTrue();
        var updatedCode = await _context.MfaBackupCodes.FirstOrDefaultAsync(c => c.Id == backupCode.Id);
        updatedCode.Should().NotBeNull();
        updatedCode!.IsUsed.Should().BeTrue();
        updatedCode.UsedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task VerifyBackupCodeAsync_WithUsedCode_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var code = "WXYZ-9876";
        var codeHash = ComputeBackupCodeHash(code);

        var backupCode = new Core.Models.Entities.MfaBackupCode
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CodeHash = codeHash,
            IsUsed = true,
            UsedAt = DateTime.UtcNow.AddMinutes(-10),
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        await _context.MfaBackupCodes.AddAsync(backupCode);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.VerifyBackupCodeAsync(userId, code);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendSmsOtpAsync_WithValidDevice_SendsSms()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var phoneNumber = "+1234567890";

        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "testuser",
            Email = "test@example.com",
            VerifiedPhoneNumber = phoneNumber
        };

        var device = new Core.Models.Entities.MfaDevice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DeviceType = "SMS",
            DeviceName = "SMS (****7890)",
            IsActive = true,
            RegisteredAt = DateTime.UtcNow
        };

        await _context.MfaDevices.AddAsync(device);
        await _context.SaveChangesAsync();

        _userManagerMock.Setup(um => um.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);
        _smsServiceMock.Setup(s => s.SendOtpSmsAsync(phoneNumber, It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.SendSmsOtpAsync(userId);

        // Assert
        result.Should().BeTrue();
        _smsServiceMock.Verify(s => s.SendOtpSmsAsync(phoneNumber, It.IsAny<string>(), It.IsAny<int>()), Times.Once);
    }

    private static string ComputeBackupCodeHash(string code)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(code.ToUpperInvariant().Replace("-", ""));
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    public void Dispose()
    {
        _context?.Dispose();
        _cache?.Dispose();
    }
}

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using USP.Core.Models.DTOs.Authentication;
using USP.Core.Services.Authentication;
using USP.Infrastructure.Data;
using USP.Infrastructure.Services.Authentication;
using Xunit;

namespace USP.UnitTests.Services.Authentication;

/// <summary>
/// Unit tests for BiometricAuthService focusing on production-ready requirements
/// </summary>
public class BiometricAuthServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IJwtService> _jwtServiceMock;
    private readonly Mock<ILogger<BiometricAuthService>> _loggerMock;
    private readonly Mock<IRiskAssessmentService> _riskServiceMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly BiometricAuthService _service;

    public BiometricAuthServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"BiometricAuthServiceTestDb_{Guid.NewGuid()}")
            .Options;
        _context = new ApplicationDbContext(options);

        // Setup mocks
        _jwtServiceMock = new Mock<IJwtService>();
        _loggerMock = new Mock<ILogger<BiometricAuthService>>();
        _riskServiceMock = new Mock<IRiskAssessmentService>();
        _configurationMock = new Mock<IConfiguration>();

        _service = new BiometricAuthService(
            _context,
            _jwtServiceMock.Object,
            _loggerMock.Object,
            _riskServiceMock.Object,
            _configurationMock.Object);
    }

    [Fact]
    public async Task EnrollBiometricAsync_WithoutEncryptionKey_ThrowsInvalidOperationException()
    {
        // Arrange
        _configurationMock.Setup(c => c["Biometric:EncryptionKey"]).Returns((string?)null);

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "testuser",
            Email = "test@example.com"
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var request = new EnrollBiometricRequest
        {
            UserId = user.Id,
            BiometricType = "Fingerprint",
            TemplateData = "biometric_template_data",
            DeviceId = "device123",
            DeviceName = "iPhone 15"
        };

        // Act
        Func<Task> act = async () => await _service.EnrollBiometricAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Biometric encryption key not configured*");
    }

    [Fact]
    public async Task EnrollBiometricAsync_WithEmptyEncryptionKey_ThrowsInvalidOperationException()
    {
        // Arrange
        _configurationMock.Setup(c => c["Biometric:EncryptionKey"]).Returns(string.Empty);

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "testuser",
            Email = "test@example.com"
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var request = new EnrollBiometricRequest
        {
            UserId = user.Id,
            BiometricType = "Face",
            TemplateData = "face_template_data",
            DeviceId = "device456",
            DeviceName = "MacBook Pro"
        };

        // Act
        Func<Task> act = async () => await _service.EnrollBiometricAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Biometric encryption key not configured*");
    }

    [Fact]
    public async Task VerifyBiometricAsync_ThrowsNotSupportedException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var biometricType = "Fingerprint";
        var templateData = "test_template";

        // Act
        Func<Task> act = async () => await _service.VerifyBiometricAsync(userId, biometricType, templateData);

        // Assert
        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*certified biometric SDK*");
    }

    [Fact]
    public async Task AuthenticateWithBiometricOrPinAsync_ThrowsNotSupportedException()
    {
        // Arrange
        var request = new BiometricPinAuthRequest
        {
            Identifier = "test@example.com",
            BiometricData = "biometric_data",
            PinCode = "1234"
        };

        // Act
        Func<Task> act = async () => await _service.AuthenticateWithBiometricOrPinAsync(
            request, "127.0.0.1", "Mozilla/5.0");

        // Assert
        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*certified biometric SDK*");
    }

    [Fact]
    public void BiometricAuthService_DoesNotHaveRuntimeKeyGeneration()
    {
        // Arrange & Act
        var serviceType = typeof(BiometricAuthService);
        var methods = serviceType.GetMethods(System.Reflection.BindingFlags.NonPublic |
                                             System.Reflection.BindingFlags.Static |
                                             System.Reflection.BindingFlags.Instance);

        var generatesDefaultKey = methods.Any(m => m.Name.Contains("GenerateDefaultKey"));

        // Assert
        generatesDefaultKey.Should().BeFalse("service should not generate encryption keys at runtime");
    }

    [Theory]
    [InlineData("Fingerprint")]
    [InlineData("Face")]
    [InlineData("Iris")]
    [InlineData("Voice")]
    public async Task EnrollBiometricAsync_WithValidKey_DoesNotGenerateRuntimeKey(string biometricType)
    {
        // Arrange
        var validKey = Convert.ToBase64String(new byte[32]); // 256-bit key
        _configurationMock.Setup(c => c["Biometric:EncryptionKey"]).Returns(validKey);

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "testuser",
            Email = "test@example.com"
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var request = new EnrollBiometricRequest
        {
            UserId = user.Id,
            BiometricType = biometricType,
            TemplateData = "template_data",
            DeviceId = "device123",
            DeviceName = "Test Device"
        };

        // Act
        var response = await _service.EnrollBiometricAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.BiometricType.Should().Be(biometricType);
    }

    [Fact]
    public async Task GetUserBiometricsAsync_ReturnsEnrolledBiometrics()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var biometric = new Core.Models.Entities.BiometricTemplate
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BiometricType = "Fingerprint",
            EncryptedTemplateData = "encrypted_data",
            EncryptionIv = "iv_data",
            DeviceId = "device123",
            DeviceName = "iPhone",
            IsActive = true,
            IsPrimary = true,
            EnrolledAt = DateTime.UtcNow
        };

        await _context.Set<Core.Models.Entities.BiometricTemplate>().AddAsync(biometric);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetUserBiometricsAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        var dto = result.First();
        dto.BiometricType.Should().Be("Fingerprint");
        dto.DeviceName.Should().Be("iPhone");
        dto.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveBiometricAsync_DeactivatesBiometric()
    {
        // Arrange
        var biometricId = Guid.NewGuid();
        var biometric = new Core.Models.Entities.BiometricTemplate
        {
            Id = biometricId,
            UserId = Guid.NewGuid(),
            BiometricType = "Face",
            EncryptedTemplateData = "encrypted",
            EncryptionIv = "iv",
            DeviceId = "device123",
            DeviceName = "MacBook",
            IsActive = true,
            EnrolledAt = DateTime.UtcNow
        };

        await _context.Set<Core.Models.Entities.BiometricTemplate>().AddAsync(biometric);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.RemoveBiometricAsync(biometricId);

        // Assert
        result.Should().BeTrue();
        var updated = await _context.Set<Core.Models.Entities.BiometricTemplate>()
            .FirstOrDefaultAsync(b => b.Id == biometricId);
        updated.Should().NotBeNull();
        updated!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task SetPrimaryBiometricAsync_UpdatesPrimaryFlag()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var biometric1 = new Core.Models.Entities.BiometricTemplate
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BiometricType = "Fingerprint",
            EncryptedTemplateData = "encrypted1",
            EncryptionIv = "iv1",
            DeviceId = "device1",
            DeviceName = "Device 1",
            IsActive = true,
            IsPrimary = true,
            EnrolledAt = DateTime.UtcNow
        };

        var biometric2 = new Core.Models.Entities.BiometricTemplate
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BiometricType = "Fingerprint",
            EncryptedTemplateData = "encrypted2",
            EncryptionIv = "iv2",
            DeviceId = "device2",
            DeviceName = "Device 2",
            IsActive = true,
            IsPrimary = false,
            EnrolledAt = DateTime.UtcNow
        };

        await _context.Set<Core.Models.Entities.BiometricTemplate>().AddRangeAsync(biometric1, biometric2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.SetPrimaryBiometricAsync(userId, biometric2.Id);

        // Assert
        result.Should().BeTrue();
        var updated1 = await _context.Set<Core.Models.Entities.BiometricTemplate>()
            .FirstOrDefaultAsync(b => b.Id == biometric1.Id);
        var updated2 = await _context.Set<Core.Models.Entities.BiometricTemplate>()
            .FirstOrDefaultAsync(b => b.Id == biometric2.Id);

        updated1!.IsPrimary.Should().BeFalse();
        updated2!.IsPrimary.Should().BeTrue();
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}

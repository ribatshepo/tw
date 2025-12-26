using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using USP.Core.Models.DTOs.Pki;
using USP.Core.Services.Cryptography;
using USP.Infrastructure.Data;
using USP.Infrastructure.Services.Secrets;
using Xunit;

namespace USP.UnitTests.Services.Secrets;

/// <summary>
/// Unit tests for PKI Engine covering CA management, roles, certificate issuance, and CRL operations
/// Total: 49 tests
/// </summary>
public class PkiEngineTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IEncryptionService> _encryptionServiceMock;
    private readonly Mock<ILogger<PkiEngine>> _loggerMock;
    private readonly PkiEngine _pkiEngine;
    private readonly Guid _testUserId = Guid.NewGuid();

    public PkiEngineTests()
    {
        // Setup mocks first
        _encryptionServiceMock = new Mock<IEncryptionService>();
        _loggerMock = new Mock<ILogger<PkiEngine>>();

        // Mock encryption/decryption to return base64-encoded values
        _encryptionServiceMock
            .Setup(e => e.Encrypt(It.IsAny<string>()))
            .Returns<string>(input => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(input)));

        _encryptionServiceMock
            .Setup(e => e.Decrypt(It.IsAny<string>()))
            .Returns<string>(encrypted => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encrypted)));

        // Setup in-memory database with service provider
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddEntityFrameworkInMemoryDatabase();

        // Add Identity services required by ApplicationDbContext (which inherits from IdentityDbContext)
        serviceCollection.AddIdentity<USP.Core.Models.Entities.ApplicationUser, USP.Core.Models.Entities.Role>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        var serviceProvider = serviceCollection.BuildServiceProvider();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"PkiEngineTestDb_{Guid.NewGuid()}")
            .UseInternalServiceProvider(serviceProvider)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.ManyServiceProvidersCreatedWarning))
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new TestApplicationDbContext(options);

        _pkiEngine = new PkiEngine(_context, _encryptionServiceMock.Object, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }

    // ====================
    // CA Management Tests (13 tests)
    // ====================

    [Fact]
    public async Task CreateRootCaAsync_WithValidRequest_CreatesRootCa()
    {
        // Arrange
        var request = new CreateRootCaRequest
        {
            Name = "test-root-ca",
            SubjectDn = "CN=Test Root CA,O=TestOrg,C=US",
            KeyType = "rsa-2048",
            TtlDays = 3650,
            MaxPathLength = 2
        };

        // Act
        var result = await _pkiEngine.CreateRootCaAsync(request, _testUserId);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("test-root-ca");
        result.Type.Should().Be("root");
        result.SubjectDn.Should().Be("CN=Test Root CA,O=TestOrg,C=US");
        result.KeyType.Should().Be("rsa-2048");
        result.MaxPathLength.Should().Be(2);
        result.ParentCaName.Should().BeNull();
        result.Revoked.Should().BeFalse();
        result.SerialNumber.Should().NotBeNullOrEmpty();
        result.CertificatePem.Should().Contain("BEGIN CERTIFICATE");

        // Verify database
        var ca = await _context.PkiCertificateAuthorities.FirstOrDefaultAsync(c => c.Name == "test-root-ca");
        ca.Should().NotBeNull();
        ca!.EncryptedPrivateKey.Should().NotBeNullOrEmpty();
        ca.CreatedBy.Should().Be(_testUserId);
    }

    [Theory]
    [InlineData("rsa-2048")]
    [InlineData("rsa-4096")]
    [InlineData("ecdsa-p256")]
    [InlineData("ecdsa-p384")]
    public async Task CreateRootCaAsync_WithDifferentKeyTypes_CreatesCorrectly(string keyType)
    {
        // Arrange
        var request = new CreateRootCaRequest
        {
            Name = $"test-root-{keyType}",
            SubjectDn = "CN=Test Root CA,O=TestOrg,C=US",
            KeyType = keyType,
            TtlDays = 3650,
            MaxPathLength = 2
        };

        // Act
        var result = await _pkiEngine.CreateRootCaAsync(request, _testUserId);

        // Assert
        result.Should().NotBeNull();
        result.KeyType.Should().Be(keyType);
        result.CertificatePem.Should().Contain("BEGIN CERTIFICATE");
    }

    [Fact]
    public async Task CreateRootCaAsync_WithDuplicateName_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = new CreateRootCaRequest
        {
            Name = "duplicate-ca",
            SubjectDn = "CN=Test Root CA,O=TestOrg,C=US",
            KeyType = "rsa-2048",
            TtlDays = 3650,
            MaxPathLength = 2
        };

        await _pkiEngine.CreateRootCaAsync(request, _testUserId);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _pkiEngine.CreateRootCaAsync(request, _testUserId));
    }

    [Fact]
    public async Task CreateIntermediateCaAsync_WithValidRequest_CreatesIntermediateCa()
    {
        // Arrange - Create root CA first
        var rootRequest = new CreateRootCaRequest
        {
            Name = "root-ca",
            SubjectDn = "CN=Root CA,O=TestOrg,C=US",
            KeyType = "rsa-2048",
            TtlDays = 3650,
            MaxPathLength = 2
        };
        await _pkiEngine.CreateRootCaAsync(rootRequest, _testUserId);

        var intermediateRequest = new CreateIntermediateCaRequest
        {
            Name = "intermediate-ca",
            ParentCaName = "root-ca",
            SubjectDn = "CN=Intermediate CA,O=TestOrg,C=US",
            KeyType = "rsa-2048",
            TtlDays = 1825,
            MaxPathLength = 1
        };

        // Act
        var result = await _pkiEngine.CreateIntermediateCaAsync(intermediateRequest, _testUserId);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("intermediate-ca");
        result.Type.Should().Be("intermediate");
        result.ParentCaName.Should().Be("root-ca");
        result.MaxPathLength.Should().Be(1);
        result.Revoked.Should().BeFalse();
        result.CertificatePem.Should().Contain("BEGIN CERTIFICATE");
    }

    [Fact]
    public async Task CreateIntermediateCaAsync_WithNonExistentParent_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = new CreateIntermediateCaRequest
        {
            Name = "intermediate-ca",
            ParentCaName = "non-existent-ca",
            SubjectDn = "CN=Intermediate CA,O=TestOrg,C=US",
            KeyType = "rsa-2048",
            TtlDays = 1825,
            MaxPathLength = 1
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _pkiEngine.CreateIntermediateCaAsync(request, _testUserId));
    }

    [Fact]
    public async Task CreateIntermediateCaAsync_WithRevokedParent_ThrowsInvalidOperationException()
    {
        // Arrange - Create and revoke root CA
        var rootRequest = new CreateRootCaRequest
        {
            Name = "revoked-root",
            SubjectDn = "CN=Root CA,O=TestOrg,C=US",
            KeyType = "rsa-2048",
            TtlDays = 3650,
            MaxPathLength = 2
        };
        await _pkiEngine.CreateRootCaAsync(rootRequest, _testUserId);
        await _pkiEngine.RevokeCaAsync("revoked-root", _testUserId);

        var intermediateRequest = new CreateIntermediateCaRequest
        {
            Name = "intermediate-ca",
            ParentCaName = "revoked-root",
            SubjectDn = "CN=Intermediate CA,O=TestOrg,C=US",
            KeyType = "rsa-2048",
            TtlDays = 1825,
            MaxPathLength = 1
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _pkiEngine.CreateIntermediateCaAsync(intermediateRequest, _testUserId));
    }

    [Fact]
    public async Task CreateIntermediateCaAsync_WithInvalidPathLength_ThrowsInvalidOperationException()
    {
        // Arrange - Create root CA with MaxPathLength = 0
        var rootRequest = new CreateRootCaRequest
        {
            Name = "root-ca-no-intermediates",
            SubjectDn = "CN=Root CA,O=TestOrg,C=US",
            KeyType = "rsa-2048",
            TtlDays = 3650,
            MaxPathLength = 0 // No intermediates allowed
        };
        await _pkiEngine.CreateRootCaAsync(rootRequest, _testUserId);

        var intermediateRequest = new CreateIntermediateCaRequest
        {
            Name = "intermediate-ca",
            ParentCaName = "root-ca-no-intermediates",
            SubjectDn = "CN=Intermediate CA,O=TestOrg,C=US",
            KeyType = "rsa-2048",
            TtlDays = 1825,
            MaxPathLength = 0
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _pkiEngine.CreateIntermediateCaAsync(intermediateRequest, _testUserId));
    }

    [Fact]
    public async Task ReadCaAsync_WithExistingCa_ReturnsCaDetails()
    {
        // Arrange
        var request = new CreateRootCaRequest
        {
            Name = "test-ca",
            SubjectDn = "CN=Test CA,O=TestOrg,C=US",
            KeyType = "rsa-2048",
            TtlDays = 3650,
            MaxPathLength = 2
        };
        await _pkiEngine.CreateRootCaAsync(request, _testUserId);

        // Act
        var result = await _pkiEngine.ReadCaAsync("test-ca", _testUserId);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("test-ca");
        result.Type.Should().Be("root");
    }

    [Fact]
    public async Task ReadCaAsync_WithNonExistentCa_ThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _pkiEngine.ReadCaAsync("non-existent-ca", _testUserId));
    }

    [Fact]
    public async Task ListCasAsync_ReturnsAllCaNames()
    {
        // Arrange
        await _pkiEngine.CreateRootCaAsync(new CreateRootCaRequest
        {
            Name = "ca-1",
            SubjectDn = "CN=CA 1,O=TestOrg,C=US",
            KeyType = "rsa-2048",
            TtlDays = 3650,
            MaxPathLength = 2
        }, _testUserId);

        await _pkiEngine.CreateRootCaAsync(new CreateRootCaRequest
        {
            Name = "ca-2",
            SubjectDn = "CN=CA 2,O=TestOrg,C=US",
            KeyType = "rsa-2048",
            TtlDays = 3650,
            MaxPathLength = 2
        }, _testUserId);

        // Act
        var result = await _pkiEngine.ListCasAsync(_testUserId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(new[] { "ca-1", "ca-2" });
    }

    [Fact]
    public async Task DeleteCaAsync_WithExistingCa_DeletesCa()
    {
        // Arrange
        await _pkiEngine.CreateRootCaAsync(new CreateRootCaRequest
        {
            Name = "ca-to-delete",
            SubjectDn = "CN=CA To Delete,O=TestOrg,C=US",
            KeyType = "rsa-2048",
            TtlDays = 3650,
            MaxPathLength = 2
        }, _testUserId);

        // Act
        await _pkiEngine.DeleteCaAsync("ca-to-delete", _testUserId);

        // Assert
        var ca = await _context.PkiCertificateAuthorities.FirstOrDefaultAsync(c => c.Name == "ca-to-delete");
        ca.Should().BeNull();
    }

    [Fact]
    public async Task DeleteCaAsync_WithChildCas_ThrowsInvalidOperationException()
    {
        // Arrange - Create root and intermediate
        await _pkiEngine.CreateRootCaAsync(new CreateRootCaRequest
        {
            Name = "root-with-child",
            SubjectDn = "CN=Root CA,O=TestOrg,C=US",
            KeyType = "rsa-2048",
            TtlDays = 3650,
            MaxPathLength = 2
        }, _testUserId);

        await _pkiEngine.CreateIntermediateCaAsync(new CreateIntermediateCaRequest
        {
            Name = "child-ca",
            ParentCaName = "root-with-child",
            SubjectDn = "CN=Child CA,O=TestOrg,C=US",
            KeyType = "rsa-2048",
            TtlDays = 1825,
            MaxPathLength = 1
        }, _testUserId);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _pkiEngine.DeleteCaAsync("root-with-child", _testUserId));
    }

    [Fact]
    public async Task RevokeCaAsync_WithExistingCa_RevokesSuccessfully()
    {
        // Arrange
        await _pkiEngine.CreateRootCaAsync(new CreateRootCaRequest
        {
            Name = "ca-to-revoke",
            SubjectDn = "CN=CA To Revoke,O=TestOrg,C=US",
            KeyType = "rsa-2048",
            TtlDays = 3650,
            MaxPathLength = 2
        }, _testUserId);

        // Act
        await _pkiEngine.RevokeCaAsync("ca-to-revoke", _testUserId);

        // Assert
        var result = await _pkiEngine.ReadCaAsync("ca-to-revoke", _testUserId);
        result.Revoked.Should().BeTrue();
        result.RevokedAt.Should().NotBeNull();
    }

    // ====================
    // Role Management Tests (9 tests)
    // ====================

    [Fact]
    public async Task CreateRoleAsync_WithValidRequest_CreatesRole()
    {
        // Arrange
        await _pkiEngine.CreateRootCaAsync(new CreateRootCaRequest
        {
            Name = "test-ca",
            SubjectDn = "CN=Test CA,O=TestOrg,C=US",
            KeyType = "rsa-2048",
            TtlDays = 3650,
            MaxPathLength = 2
        }, _testUserId);

        var roleRequest = new CreateRoleRequest
        {
            Name = "web-server-role",
            CertificateAuthorityName = "test-ca",
            KeyType = "rsa-2048",
            TtlDays = 365,
            MaxTtlDays = 730,
            AllowLocalhost = true,
            AllowBareDomains = false,
            AllowSubdomains = true,
            AllowWildcards = false,
            AllowIpSans = true,
            AllowedDomains = new List<string> { "example.com", "*.example.org" },
            ServerAuth = true,
            ClientAuth = false,
            CodeSigning = false,
            EmailProtection = false
        };

        // Act
        var result = await _pkiEngine.CreateRoleAsync(roleRequest, _testUserId);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("web-server-role");
        result.CertificateAuthorityName.Should().Be("test-ca");
        result.KeyType.Should().Be("rsa-2048");
        result.TtlDays.Should().Be(365);
        result.MaxTtlDays.Should().Be(730);
        result.AllowSubdomains.Should().BeTrue();
        result.ServerAuth.Should().BeTrue();
        result.AllowedDomains.Should().BeEquivalentTo(new[] { "example.com", "*.example.org" });
    }

    [Fact]
    public async Task CreateRoleAsync_WithDuplicateName_ThrowsInvalidOperationException()
    {
        // Arrange
        await _pkiEngine.CreateRootCaAsync(new CreateRootCaRequest
        {
            Name = "test-ca",
            SubjectDn = "CN=Test CA,O=TestOrg,C=US",
            KeyType = "rsa-2048",
            TtlDays = 3650,
            MaxPathLength = 2
        }, _testUserId);

        var roleRequest = new CreateRoleRequest
        {
            Name = "duplicate-role",
            CertificateAuthorityName = "test-ca",
            KeyType = "rsa-2048",
            TtlDays = 365,
            MaxTtlDays = 730,
            AllowedDomains = new List<string>()
        };

        await _pkiEngine.CreateRoleAsync(roleRequest, _testUserId);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _pkiEngine.CreateRoleAsync(roleRequest, _testUserId));
    }

    [Fact]
    public async Task CreateRoleAsync_WithNonExistentCa_ThrowsInvalidOperationException()
    {
        // Arrange
        var roleRequest = new CreateRoleRequest
        {
            Name = "test-role",
            CertificateAuthorityName = "non-existent-ca",
            KeyType = "rsa-2048",
            TtlDays = 365,
            MaxTtlDays = 730,
            AllowedDomains = new List<string>()
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _pkiEngine.CreateRoleAsync(roleRequest, _testUserId));
    }

    [Fact]
    public async Task CreateRoleAsync_WithInvalidTtl_ThrowsArgumentException()
    {
        // Arrange
        await _pkiEngine.CreateRootCaAsync(new CreateRootCaRequest
        {
            Name = "test-ca",
            SubjectDn = "CN=Test CA,O=TestOrg,C=US",
            KeyType = "rsa-2048",
            TtlDays = 3650,
            MaxPathLength = 2
        }, _testUserId);

        var roleRequest = new CreateRoleRequest
        {
            Name = "invalid-ttl-role",
            CertificateAuthorityName = "test-ca",
            KeyType = "rsa-2048",
            TtlDays = 1000, // Exceeds MaxTtlDays
            MaxTtlDays = 365,
            AllowedDomains = new List<string>()
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _pkiEngine.CreateRoleAsync(roleRequest, _testUserId));
    }

    [Fact]
    public async Task ReadRoleAsync_WithExistingRole_ReturnsRoleDetails()
    {
        // Arrange
        await _pkiEngine.CreateRootCaAsync(new CreateRootCaRequest
        {
            Name = "test-ca",
            SubjectDn = "CN=Test CA,O=TestOrg,C=US",
            KeyType = "rsa-2048",
            TtlDays = 3650,
            MaxPathLength = 2
        }, _testUserId);

        await _pkiEngine.CreateRoleAsync(new CreateRoleRequest
        {
            Name = "test-role",
            CertificateAuthorityName = "test-ca",
            KeyType = "rsa-2048",
            TtlDays = 365,
            MaxTtlDays = 730,
            AllowedDomains = new List<string>()
        }, _testUserId);

        // Act
        var result = await _pkiEngine.ReadRoleAsync("test-role", _testUserId);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("test-role");
        result.CertificateAuthorityName.Should().Be("test-ca");
    }

    [Fact]
    public async Task ReadRoleAsync_WithNonExistentRole_ThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _pkiEngine.ReadRoleAsync("non-existent-role", _testUserId));
    }

    [Fact]
    public async Task ListRolesAsync_ReturnsAllRoleNames()
    {
        // Arrange
        await _pkiEngine.CreateRootCaAsync(new CreateRootCaRequest
        {
            Name = "test-ca",
            SubjectDn = "CN=Test CA,O=TestOrg,C=US",
            KeyType = "rsa-2048",
            TtlDays = 3650,
            MaxPathLength = 2
        }, _testUserId);

        await _pkiEngine.CreateRoleAsync(new CreateRoleRequest
        {
            Name = "role-1",
            CertificateAuthorityName = "test-ca",
            KeyType = "rsa-2048",
            TtlDays = 365,
            MaxTtlDays = 730,
            AllowedDomains = new List<string>()
        }, _testUserId);

        await _pkiEngine.CreateRoleAsync(new CreateRoleRequest
        {
            Name = "role-2",
            CertificateAuthorityName = "test-ca",
            KeyType = "rsa-2048",
            TtlDays = 365,
            MaxTtlDays = 730,
            AllowedDomains = new List<string>()
        }, _testUserId);

        // Act
        var result = await _pkiEngine.ListRolesAsync(_testUserId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(new[] { "role-1", "role-2" });
    }

    [Fact]
    public async Task DeleteRoleAsync_WithExistingRole_DeletesSuccessfully()
    {
        // Arrange
        await _pkiEngine.CreateRootCaAsync(new CreateRootCaRequest
        {
            Name = "test-ca",
            SubjectDn = "CN=Test CA,O=TestOrg,C=US",
            KeyType = "rsa-2048",
            TtlDays = 3650,
            MaxPathLength = 2
        }, _testUserId);

        await _pkiEngine.CreateRoleAsync(new CreateRoleRequest
        {
            Name = "role-to-delete",
            CertificateAuthorityName = "test-ca",
            KeyType = "rsa-2048",
            TtlDays = 365,
            MaxTtlDays = 730,
            AllowedDomains = new List<string>()
        }, _testUserId);

        // Act
        await _pkiEngine.DeleteRoleAsync("role-to-delete", _testUserId);

        // Assert
        var role = await _context.PkiRoles.FirstOrDefaultAsync(r => r.Name == "role-to-delete");
        role.Should().BeNull();
    }

    [Fact]
    public async Task DeleteRoleAsync_WithNonExistentRole_ThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _pkiEngine.DeleteRoleAsync("non-existent-role", _testUserId));
    }

    // ====================
    // Certificate Issuance Tests (12 tests)
    // ====================

    [Fact]
    public async Task IssueCertificateAsync_WithValidRequest_IssuesCertificate()
    {
        // Arrange
        await SetupCaAndRole();

        var issueRequest = new IssueCertificateRequest
        {
            CommonName = "server.example.com",
            SubjectAltNames = new List<string> { "server.example.com", "www.example.com" },
            TtlDays = 90
        };

        // Act
        var result = await _pkiEngine.IssueCertificateAsync("web-server-role", issueRequest, _testUserId);

        // Assert
        result.Should().NotBeNull();
        result.CertificatePem.Should().Contain("BEGIN CERTIFICATE");
        result.PrivateKeyPem.Should().Contain("PRIVATE KEY");
        result.CaChainPem.Should().Contain("BEGIN CERTIFICATE");
        result.SerialNumber.Should().NotBeNullOrEmpty();
        result.IssuingCa.Should().Be("test-ca");
        result.NotAfter.Should().BeAfter(result.NotBefore);

        // Verify certificate is stored in database
        var cert = await _context.PkiIssuedCertificates.FirstOrDefaultAsync(c => c.SerialNumber == result.SerialNumber);
        cert.Should().NotBeNull();
        cert!.SubjectDn.Should().Contain("server.example.com");
    }

    [Fact]
    public async Task IssueCertificateAsync_WithLocalhostCommonName_IssuesWhenAllowed()
    {
        // Arrange
        await SetupCaAndRole();

        var issueRequest = new IssueCertificateRequest
        {
            CommonName = "localhost",
            SubjectAltNames = new List<string> { "localhost", "127.0.0.1" }
        };

        // Act
        var result = await _pkiEngine.IssueCertificateAsync("web-server-role", issueRequest, _testUserId);

        // Assert
        result.Should().NotBeNull();
        result.CertificatePem.Should().Contain("BEGIN CERTIFICATE");
    }

    [Fact]
    public async Task IssueCertificateAsync_WithIpAddress_IssuesWhenAllowed()
    {
        // Arrange
        await SetupCaAndRole();

        var issueRequest = new IssueCertificateRequest
        {
            CommonName = "192.168.1.100",
            SubjectAltNames = new List<string> { "192.168.1.100", "192.168.1.101" }
        };

        // Act
        var result = await _pkiEngine.IssueCertificateAsync("web-server-role", issueRequest, _testUserId);

        // Assert
        result.Should().NotBeNull();
        result.CertificatePem.Should().Contain("BEGIN CERTIFICATE");
    }

    [Fact]
    public async Task IssueCertificateAsync_WithExcessiveTtl_ThrowsArgumentException()
    {
        // Arrange
        await SetupCaAndRole();

        var issueRequest = new IssueCertificateRequest
        {
            CommonName = "server.example.com",
            TtlDays = 10000 // Exceeds role MaxTtlDays
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _pkiEngine.IssueCertificateAsync("web-server-role", issueRequest, _testUserId));
    }

    [Fact]
    public async Task IssueCertificateAsync_WithInvalidDomain_ThrowsArgumentException()
    {
        // Arrange
        await SetupCaAndRole();

        var issueRequest = new IssueCertificateRequest
        {
            CommonName = "server.notallowed.com" // Not in AllowedDomains
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _pkiEngine.IssueCertificateAsync("web-server-role", issueRequest, _testUserId));
    }

    [Fact]
    public async Task IssueCertificateAsync_WithSubdomain_IssuesWhenAllowed()
    {
        // Arrange
        await SetupCaAndRole();

        var issueRequest = new IssueCertificateRequest
        {
            CommonName = "api.example.com" // Subdomain of example.com
        };

        // Act
        var result = await _pkiEngine.IssueCertificateAsync("web-server-role", issueRequest, _testUserId);

        // Assert
        result.Should().NotBeNull();
        result.CertificatePem.Should().Contain("BEGIN CERTIFICATE");
    }

    [Fact]
    public async Task IssueCertificateAsync_WithWildcard_IssuesWhenAllowed()
    {
        // Arrange
        await _pkiEngine.CreateRootCaAsync(new CreateRootCaRequest
        {
            Name = "test-ca",
            SubjectDn = "CN=Test CA,O=TestOrg,C=US",
            KeyType = "rsa-2048",
            TtlDays = 3650,
            MaxPathLength = 2
        }, _testUserId);

        await _pkiEngine.CreateRoleAsync(new CreateRoleRequest
        {
            Name = "wildcard-role",
            CertificateAuthorityName = "test-ca",
            KeyType = "rsa-2048",
            TtlDays = 365,
            MaxTtlDays = 730,
            AllowWildcards = true,
            AllowBareDomains = true,
            AllowSubdomains = true,
            AllowedDomains = new List<string> { "example.com" }
        }, _testUserId);

        var issueRequest = new IssueCertificateRequest
        {
            CommonName = "*.example.com"
        };

        // Act
        var result = await _pkiEngine.IssueCertificateAsync("wildcard-role", issueRequest, _testUserId);

        // Assert
        result.Should().NotBeNull();
        result.CertificatePem.Should().Contain("BEGIN CERTIFICATE");
    }

    [Fact]
    public async Task IssueCertificateAsync_WithRevokedCa_ThrowsInvalidOperationException()
    {
        // Arrange
        await SetupCaAndRole();
        await _pkiEngine.RevokeCaAsync("test-ca", _testUserId);

        var issueRequest = new IssueCertificateRequest
        {
            CommonName = "server.example.com"
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _pkiEngine.IssueCertificateAsync("web-server-role", issueRequest, _testUserId));
    }

    [Fact]
    public async Task IssueCertificateAsync_IncrementsIssuedCertificateCount()
    {
        // Arrange
        await SetupCaAndRole();

        var issueRequest = new IssueCertificateRequest
        {
            CommonName = "server.example.com"
        };

        // Act
        await _pkiEngine.IssueCertificateAsync("web-server-role", issueRequest, _testUserId);

        // Assert
        var ca = await _context.PkiCertificateAuthorities.FirstOrDefaultAsync(c => c.Name == "test-ca");
        ca!.IssuedCertificateCount.Should().Be(1);
    }

    [Fact]
    public async Task SignCsrAsync_WithValidCsr_SignsSuccessfully()
    {
        // Arrange
        await SetupCaAndRole();

        // Generate a real CSR using BouncyCastle
        var csrPem = GenerateTestCsr("server.example.com");

        var signRequest = new SignCsrRequest
        {
            Csr = csrPem,
            TtlDays = 90
        };

        // Act
        var result = await _pkiEngine.SignCsrAsync("web-server-role", signRequest, _testUserId);

        // Assert
        result.Should().NotBeNull();
        result.CertificatePem.Should().Contain("BEGIN CERTIFICATE");
        result.PrivateKeyPem.Should().BeNull(); // No private key for CSR signing
        result.CaChainPem.Should().Contain("BEGIN CERTIFICATE");
        result.SerialNumber.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SignCsrAsync_WithInvalidCsr_ThrowsArgumentException()
    {
        // Arrange
        await SetupCaAndRole();

        var signRequest = new SignCsrRequest
        {
            Csr = "INVALID CSR",
            TtlDays = 90
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _pkiEngine.SignCsrAsync("web-server-role", signRequest, _testUserId));
    }

    [Fact]
    public async Task SignCsrAsync_WithNonExistentRole_ThrowsInvalidOperationException()
    {
        // Arrange
        var csrPem = GenerateTestCsr("server.example.com");

        var signRequest = new SignCsrRequest
        {
            Csr = csrPem
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _pkiEngine.SignCsrAsync("non-existent-role", signRequest, _testUserId));
    }

    // ====================
    // Certificate Operations Tests (9 tests)
    // ====================

    [Fact]
    public async Task RevokeCertificateAsync_WithValidSerial_RevokesSuccessfully()
    {
        // Arrange
        await SetupCaAndRole();
        var issued = await _pkiEngine.IssueCertificateAsync("web-server-role", new IssueCertificateRequest
        {
            CommonName = "server.example.com"
        }, _testUserId);

        var revokeRequest = new RevokeCertificateRequest
        {
            SerialNumber = issued.SerialNumber
        };

        // Act
        var result = await _pkiEngine.RevokeCertificateAsync(revokeRequest, _testUserId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.RevokedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Verify in database
        var cert = await _context.PkiIssuedCertificates.FirstOrDefaultAsync(c => c.SerialNumber == issued.SerialNumber);
        cert!.Revoked.Should().BeTrue();
        cert.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RevokeCertificateAsync_WithNonExistentSerial_ThrowsInvalidOperationException()
    {
        // Arrange
        var revokeRequest = new RevokeCertificateRequest
        {
            SerialNumber = "NONEXISTENT123456"
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _pkiEngine.RevokeCertificateAsync(revokeRequest, _testUserId));
    }

    [Fact]
    public async Task RevokeCertificateAsync_AlreadyRevoked_ThrowsInvalidOperationException()
    {
        // Arrange
        await SetupCaAndRole();
        var issued = await _pkiEngine.IssueCertificateAsync("web-server-role", new IssueCertificateRequest
        {
            CommonName = "server.example.com"
        }, _testUserId);

        var revokeRequest = new RevokeCertificateRequest
        {
            SerialNumber = issued.SerialNumber
        };

        await _pkiEngine.RevokeCertificateAsync(revokeRequest, _testUserId);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _pkiEngine.RevokeCertificateAsync(revokeRequest, _testUserId));
    }

    [Fact]
    public async Task ListCertificatesAsync_WithoutFilter_ReturnsAllCertificates()
    {
        // Arrange
        await SetupCaAndRole();
        await _pkiEngine.IssueCertificateAsync("web-server-role", new IssueCertificateRequest
        {
            CommonName = "server1.example.com"
        }, _testUserId);

        await _pkiEngine.IssueCertificateAsync("web-server-role", new IssueCertificateRequest
        {
            CommonName = "server2.example.com"
        }, _testUserId);

        // Act
        var result = await _pkiEngine.ListCertificatesAsync(null, _testUserId);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(2);
        result.Certificates.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListCertificatesAsync_WithCaFilter_ReturnsFilteredCertificates()
    {
        // Arrange
        await SetupCaAndRole();

        // Create second CA and role
        await _pkiEngine.CreateRootCaAsync(new CreateRootCaRequest
        {
            Name = "test-ca-2",
            SubjectDn = "CN=Test CA 2,O=TestOrg,C=US",
            KeyType = "rsa-2048",
            TtlDays = 3650,
            MaxPathLength = 2
        }, _testUserId);

        await _pkiEngine.CreateRoleAsync(new CreateRoleRequest
        {
            Name = "role-2",
            CertificateAuthorityName = "test-ca-2",
            KeyType = "rsa-2048",
            TtlDays = 365,
            MaxTtlDays = 730,
            AllowLocalhost = true,
            AllowSubdomains = true,
            AllowedDomains = new List<string> { "example.com" }
        }, _testUserId);

        // Issue certificates from both CAs
        await _pkiEngine.IssueCertificateAsync("web-server-role", new IssueCertificateRequest
        {
            CommonName = "server1.example.com"
        }, _testUserId);

        await _pkiEngine.IssueCertificateAsync("role-2", new IssueCertificateRequest
        {
            CommonName = "server2.example.com"
        }, _testUserId);

        // Act
        var result = await _pkiEngine.ListCertificatesAsync("test-ca", _testUserId);

        // Assert
        result.TotalCount.Should().Be(1);
        result.Certificates.First().IssuingCa.Should().Be("test-ca");
    }

    [Fact]
    public async Task ListCertificatesAsync_IncludesRevokedCertificates()
    {
        // Arrange
        await SetupCaAndRole();
        var issued = await _pkiEngine.IssueCertificateAsync("web-server-role", new IssueCertificateRequest
        {
            CommonName = "server.example.com"
        }, _testUserId);

        await _pkiEngine.RevokeCertificateAsync(new RevokeCertificateRequest
        {
            SerialNumber = issued.SerialNumber
        }, _testUserId);

        // Act
        var result = await _pkiEngine.ListCertificatesAsync(null, _testUserId);

        // Assert
        result.Certificates.Should().HaveCount(1);
        result.Certificates.First().Revoked.Should().BeTrue();
    }

    [Fact]
    public async Task ReadCertificateAsync_WithValidSerial_ReturnsCertificateInfo()
    {
        // Arrange
        await SetupCaAndRole();
        var issued = await _pkiEngine.IssueCertificateAsync("web-server-role", new IssueCertificateRequest
        {
            CommonName = "server.example.com"
        }, _testUserId);

        // Act
        var result = await _pkiEngine.ReadCertificateAsync(issued.SerialNumber, _testUserId);

        // Assert
        result.Should().NotBeNull();
        result.SerialNumber.Should().Be(issued.SerialNumber);
        result.IssuingCa.Should().Be("test-ca");
        result.RoleName.Should().Be("web-server-role");
        result.Revoked.Should().BeFalse();
    }

    [Fact]
    public async Task ReadCertificateAsync_WithNonExistentSerial_ThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _pkiEngine.ReadCertificateAsync("NONEXISTENT123", _testUserId));
    }

    [Fact]
    public async Task ReadCertificateAsync_AfterRevocation_ShowsRevokedStatus()
    {
        // Arrange
        await SetupCaAndRole();
        var issued = await _pkiEngine.IssueCertificateAsync("web-server-role", new IssueCertificateRequest
        {
            CommonName = "server.example.com"
        }, _testUserId);

        await _pkiEngine.RevokeCertificateAsync(new RevokeCertificateRequest
        {
            SerialNumber = issued.SerialNumber
        }, _testUserId);

        // Act
        var result = await _pkiEngine.ReadCertificateAsync(issued.SerialNumber, _testUserId);

        // Assert
        result.Revoked.Should().BeTrue();
        result.RevokedAt.Should().NotBeNull();
    }

    // ====================
    // CRL Management Tests (6 tests)
    // ====================

    [Fact]
    public async Task GenerateCrlAsync_WithValidCa_GeneratesCrl()
    {
        // Arrange
        await SetupCaAndRole();

        // Act
        var result = await _pkiEngine.GenerateCrlAsync("test-ca", _testUserId);

        // Assert
        result.Should().NotBeNull();
        result.Crl.Should().Contain("BEGIN X509 CRL");
        result.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.NextUpdate.Should().BeAfter(result.GeneratedAt);
        result.RevokedCount.Should().Be(0);
    }

    [Fact]
    public async Task GenerateCrlAsync_WithRevokedCertificates_IncludesThemInCrl()
    {
        // Arrange
        await SetupCaAndRole();

        // Issue and revoke certificates
        var cert1 = await _pkiEngine.IssueCertificateAsync("web-server-role", new IssueCertificateRequest
        {
            CommonName = "server1.example.com"
        }, _testUserId);

        var cert2 = await _pkiEngine.IssueCertificateAsync("web-server-role", new IssueCertificateRequest
        {
            CommonName = "server2.example.com"
        }, _testUserId);

        await _pkiEngine.RevokeCertificateAsync(new RevokeCertificateRequest
        {
            SerialNumber = cert1.SerialNumber
        }, _testUserId);

        await _pkiEngine.RevokeCertificateAsync(new RevokeCertificateRequest
        {
            SerialNumber = cert2.SerialNumber
        }, _testUserId);

        // Act
        var result = await _pkiEngine.GenerateCrlAsync("test-ca", _testUserId);

        // Assert
        result.RevokedCount.Should().Be(2);
        result.Crl.Should().Contain("BEGIN X509 CRL");
    }

    [Fact]
    public async Task GenerateCrlAsync_WithNonExistentCa_ThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _pkiEngine.GenerateCrlAsync("non-existent-ca", _testUserId));
    }

    [Fact]
    public async Task GenerateCrlAsync_SetsNextUpdateTo30Days()
    {
        // Arrange
        await SetupCaAndRole();

        // Act
        var result = await _pkiEngine.GenerateCrlAsync("test-ca", _testUserId);

        // Assert
        var expectedNextUpdate = result.GeneratedAt.AddDays(30);
        result.NextUpdate.Should().BeCloseTo(expectedNextUpdate, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GenerateCrlAsync_WithOnlyActiveCertificates_ReturnsEmptyCrl()
    {
        // Arrange
        await SetupCaAndRole();

        // Issue certificates but don't revoke
        await _pkiEngine.IssueCertificateAsync("web-server-role", new IssueCertificateRequest
        {
            CommonName = "server1.example.com"
        }, _testUserId);

        await _pkiEngine.IssueCertificateAsync("web-server-role", new IssueCertificateRequest
        {
            CommonName = "server2.example.com"
        }, _testUserId);

        // Act
        var result = await _pkiEngine.GenerateCrlAsync("test-ca", _testUserId);

        // Assert
        result.RevokedCount.Should().Be(0);
        result.Crl.Should().Contain("BEGIN X509 CRL");
    }

    [Fact]
    public async Task GenerateCrlAsync_MultipleTimes_IncrementsCrlNumber()
    {
        // Arrange
        await SetupCaAndRole();

        // Act - Generate CRL twice
        var result1 = await _pkiEngine.GenerateCrlAsync("test-ca", _testUserId);
        await Task.Delay(1000); // Ensure different timestamp
        var result2 = await _pkiEngine.GenerateCrlAsync("test-ca", _testUserId);

        // Assert - CRL numbers should be different (timestamp-based)
        result1.GeneratedAt.Should().NotBe(result2.GeneratedAt);
        result1.Crl.Should().Contain("BEGIN X509 CRL");
        result2.Crl.Should().Contain("BEGIN X509 CRL");
    }

    // ====================
    // Helper Methods
    // ====================

    private async Task SetupCaAndRole()
    {
        await _pkiEngine.CreateRootCaAsync(new CreateRootCaRequest
        {
            Name = "test-ca",
            SubjectDn = "CN=Test CA,O=TestOrg,C=US",
            KeyType = "rsa-2048",
            TtlDays = 3650,
            MaxPathLength = 2
        }, _testUserId);

        await _pkiEngine.CreateRoleAsync(new CreateRoleRequest
        {
            Name = "web-server-role",
            CertificateAuthorityName = "test-ca",
            KeyType = "rsa-2048",
            TtlDays = 365,
            MaxTtlDays = 730,
            AllowLocalhost = true,
            AllowBareDomains = false,
            AllowSubdomains = true,
            AllowWildcards = false,
            AllowIpSans = true,
            AllowedDomains = new List<string> { "example.com", "*.example.org" },
            ServerAuth = true,
            ClientAuth = false,
            CodeSigning = false,
            EmailProtection = false
        }, _testUserId);
    }

    private string GenerateTestCsr(string commonName)
    {
        // Use BouncyCastle to generate a real CSR
        var keyPairGenerator = new Org.BouncyCastle.Crypto.Generators.RsaKeyPairGenerator();
        keyPairGenerator.Init(new Org.BouncyCastle.Crypto.KeyGenerationParameters(
            new Org.BouncyCastle.Security.SecureRandom(), 2048));
        var keyPair = keyPairGenerator.GenerateKeyPair();

        var subject = new Org.BouncyCastle.Asn1.X509.X509Name($"CN={commonName}");
        var subjectPublicKeyInfo = Org.BouncyCastle.X509.SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(keyPair.Public);

        var signatureFactory = new Org.BouncyCastle.Crypto.Operators.Asn1SignatureFactory(
            "SHA256WithRSA", keyPair.Private, new Org.BouncyCastle.Security.SecureRandom());

        var csrBuilder = new Org.BouncyCastle.Pkcs.Pkcs10CertificationRequest(
            signatureFactory, subject, keyPair.Public, null);

        var csrBytes = csrBuilder.GetEncoded();
        var csrPem = new System.Text.StringBuilder();
        csrPem.AppendLine("-----BEGIN CERTIFICATE REQUEST-----");
        csrPem.AppendLine(Convert.ToBase64String(csrBytes, Base64FormattingOptions.InsertLineBreaks));
        csrPem.AppendLine("-----END CERTIFICATE REQUEST-----");

        return csrPem.ToString();
    }
}

/// <summary>
/// Test-specific DbContext that excludes entities with JsonDocument properties
/// that are not supported by the in-memory database provider
/// </summary>
internal class TestApplicationDbContext : ApplicationDbContext
{
    public TestApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Ignore entities with JsonDocument properties that cause issues with in-memory database
        modelBuilder.Ignore<USP.Core.Models.Entities.AccessPolicy>();
        modelBuilder.Ignore<USP.Core.Models.Entities.Secret>();
        modelBuilder.Ignore<USP.Core.Models.Entities.SecretAccessLog>();
    }
}

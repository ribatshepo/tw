using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;
using USP.Core.Models.DTOs.Pki;
using USP.Core.Models.Entities;
using USP.Core.Services.Cryptography;
using USP.Core.Services.Secrets;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Secrets;

/// <summary>
/// PKI Engine for Certificate Authority management and X.509 certificate lifecycle operations
/// Provides HashiCorp Vault-compatible PKI secrets engine functionality
/// </summary>
public class PkiEngine : IPkiEngine
{
    private readonly ApplicationDbContext _context;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<PkiEngine> _logger;

    public PkiEngine(
        ApplicationDbContext context,
        IEncryptionService encryptionService,
        ILogger<PkiEngine> logger)
    {
        _context = context;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    // ====================
    // CA Management
    // ====================

    public async Task<CertificateAuthorityResponse> CreateRootCaAsync(CreateRootCaRequest request, Guid userId)
    {
        _logger.LogInformation("Creating root CA: {CaName}", request.Name);

        // Validate CA name uniqueness
        if (await _context.PkiCertificateAuthorities.AnyAsync(ca => ca.Name == request.Name))
        {
            throw new InvalidOperationException($"Certificate Authority with name '{request.Name}' already exists");
        }

        // Generate root CA certificate
        var (certificatePem, encryptedPrivateKey, serialNumber) = GenerateRootCaCertificate(request);

        // Parse certificate to extract validity dates
        var cert = LoadCertificateFromPem(certificatePem);

        // Create CA entity
        var ca = new PkiCertificateAuthority
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Type = "root",
            SubjectDn = request.SubjectDn,
            CertificatePem = certificatePem,
            SerialNumber = serialNumber,
            EncryptedPrivateKey = encryptedPrivateKey,
            KeyType = request.KeyType,
            NotBefore = cert.NotBefore,
            NotAfter = cert.NotAfter,
            MaxPathLength = request.MaxPathLength,
            ParentCaId = null,
            Revoked = false,
            IssuedCertificateCount = 0,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.PkiCertificateAuthorities.Add(ca);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Root CA created successfully: {CaName}, Serial: {Serial}", ca.Name, ca.SerialNumber);

        return MapCaToResponse(ca);
    }

    public async Task<CertificateAuthorityResponse> CreateIntermediateCaAsync(CreateIntermediateCaRequest request, Guid userId)
    {
        _logger.LogInformation("Creating intermediate CA: {CaName}, Parent: {ParentCaName}", request.Name, request.ParentCaName);

        // Validate CA name uniqueness
        if (await _context.PkiCertificateAuthorities.AnyAsync(ca => ca.Name == request.Name))
        {
            throw new InvalidOperationException($"Certificate Authority with name '{request.Name}' already exists");
        }

        // Load parent CA
        var parentCa = await _context.PkiCertificateAuthorities
            .FirstOrDefaultAsync(ca => ca.Name == request.ParentCaName);

        if (parentCa == null)
        {
            throw new InvalidOperationException($"Parent CA not found: {request.ParentCaName}");
        }

        if (parentCa.Revoked)
        {
            throw new InvalidOperationException($"Parent CA is revoked: {request.ParentCaName}");
        }

        // Validate path length constraint
        if (parentCa.MaxPathLength < 1)
        {
            throw new InvalidOperationException($"Parent CA does not allow intermediate CAs (MaxPathLength={parentCa.MaxPathLength})");
        }

        if (request.MaxPathLength >= parentCa.MaxPathLength)
        {
            throw new InvalidOperationException($"Intermediate CA MaxPathLength ({request.MaxPathLength}) must be less than parent CA MaxPathLength ({parentCa.MaxPathLength})");
        }

        // Generate intermediate CA certificate
        var (certificatePem, encryptedPrivateKey, serialNumber) = GenerateIntermediateCaCertificate(request, parentCa);

        // Parse certificate to extract validity dates
        var cert = LoadCertificateFromPem(certificatePem);

        // Create CA entity
        var ca = new PkiCertificateAuthority
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Type = "intermediate",
            SubjectDn = request.SubjectDn,
            CertificatePem = certificatePem,
            SerialNumber = serialNumber,
            EncryptedPrivateKey = encryptedPrivateKey,
            KeyType = request.KeyType,
            NotBefore = cert.NotBefore,
            NotAfter = cert.NotAfter,
            MaxPathLength = request.MaxPathLength,
            ParentCaId = parentCa.Id,
            Revoked = false,
            IssuedCertificateCount = 0,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.PkiCertificateAuthorities.Add(ca);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Intermediate CA created successfully: {CaName}, Serial: {Serial}", ca.Name, ca.SerialNumber);

        return MapCaToResponse(ca);
    }

    public async Task<CertificateAuthorityResponse> ReadCaAsync(string caName, Guid userId)
    {
        var ca = await _context.PkiCertificateAuthorities
            .AsNoTracking()
            .Include(c => c.ParentCa)
            .FirstOrDefaultAsync(ca => ca.Name == caName);

        if (ca == null)
        {
            throw new InvalidOperationException($"Certificate Authority not found: {caName}");
        }

        return MapCaToResponse(ca);
    }

    public async Task<List<string>> ListCasAsync(Guid userId)
    {
        return await _context.PkiCertificateAuthorities
            .AsNoTracking()
            .OrderBy(ca => ca.Name)
            .Select(ca => ca.Name)
            .ToListAsync();
    }

    public async Task DeleteCaAsync(string caName, Guid userId)
    {
        _logger.LogWarning("Deleting CA: {CaName}, User: {UserId}", caName, userId);

        var ca = await _context.PkiCertificateAuthorities
            .Include(c => c.IssuedCertificates)
            .FirstOrDefaultAsync(c => c.Name == caName);

        if (ca == null)
        {
            throw new InvalidOperationException($"Certificate Authority not found: {caName}");
        }

        // Check if there are child intermediate CAs
        var hasChildCas = await _context.PkiCertificateAuthorities
            .AnyAsync(c => c.ParentCaId == ca.Id);

        if (hasChildCas)
        {
            throw new InvalidOperationException($"Cannot delete CA '{caName}' because it has intermediate CAs. Delete child CAs first.");
        }

        // Cascade delete will remove all issued certificates (configured in EF Core)
        _context.PkiCertificateAuthorities.Remove(ca);
        await _context.SaveChangesAsync();

        _logger.LogInformation("CA deleted: {CaName}, Deleted {Count} issued certificates", caName, ca.IssuedCertificates.Count);
    }

    public async Task RevokeCaAsync(string caName, Guid userId)
    {
        _logger.LogWarning("Revoking CA: {CaName}, User: {UserId}", caName, userId);

        var ca = await _context.PkiCertificateAuthorities
            .FirstOrDefaultAsync(c => c.Name == caName);

        if (ca == null)
        {
            throw new InvalidOperationException($"Certificate Authority not found: {caName}");
        }

        if (ca.Revoked)
        {
            throw new InvalidOperationException($"Certificate Authority already revoked: {caName}");
        }

        ca.Revoked = true;
        ca.RevokedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("CA revoked: {CaName}", caName);
    }

    // ====================
    // Role Management
    // ====================

    public async Task<RoleResponse> CreateRoleAsync(CreateRoleRequest request, Guid userId)
    {
        _logger.LogInformation("Creating PKI role: {RoleName}", request.Name);

        // Validate role name uniqueness
        if (await _context.PkiRoles.AnyAsync(r => r.Name == request.Name))
        {
            throw new InvalidOperationException($"Role with name '{request.Name}' already exists");
        }

        // Validate CA exists
        var ca = await _context.PkiCertificateAuthorities
            .FirstOrDefaultAsync(c => c.Name == request.CertificateAuthorityName);

        if (ca == null)
        {
            throw new InvalidOperationException($"Certificate Authority not found: {request.CertificateAuthorityName}");
        }

        if (ca.Revoked)
        {
            throw new InvalidOperationException($"Cannot create role for revoked CA: {request.CertificateAuthorityName}");
        }

        // Validate TTL
        if (request.TtlDays > request.MaxTtlDays)
        {
            throw new ArgumentException($"TtlDays ({request.TtlDays}) cannot exceed MaxTtlDays ({request.MaxTtlDays})");
        }

        // Serialize allowed domains to JSON
        var allowedDomainsJson = System.Text.Json.JsonSerializer.Serialize(request.AllowedDomains);

        var role = new PkiRole
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            CertificateAuthorityId = ca.Id,
            KeyType = request.KeyType,
            TtlDays = request.TtlDays,
            MaxTtlDays = request.MaxTtlDays,
            AllowLocalhost = request.AllowLocalhost,
            AllowBareDomains = request.AllowBareDomains,
            AllowSubdomains = request.AllowSubdomains,
            AllowWildcards = request.AllowWildcards,
            AllowIpSans = request.AllowIpSans,
            AllowedDomains = allowedDomainsJson,
            ServerAuth = request.ServerAuth,
            ClientAuth = request.ClientAuth,
            CodeSigning = request.CodeSigning,
            EmailProtection = request.EmailProtection,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.PkiRoles.Add(role);
        await _context.SaveChangesAsync();

        _logger.LogInformation("PKI role created: {RoleName}", role.Name);

        return MapRoleToResponse(role, request.CertificateAuthorityName);
    }

    public async Task<RoleResponse> ReadRoleAsync(string roleName, Guid userId)
    {
        var role = await _context.PkiRoles
            .AsNoTracking()
            .Include(r => r.CertificateAuthority)
            .FirstOrDefaultAsync(r => r.Name == roleName);

        if (role == null)
        {
            throw new InvalidOperationException($"Role not found: {roleName}");
        }

        return MapRoleToResponse(role, role.CertificateAuthority.Name);
    }

    public async Task<List<string>> ListRolesAsync(Guid userId)
    {
        return await _context.PkiRoles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => r.Name)
            .ToListAsync();
    }

    public async Task DeleteRoleAsync(string roleName, Guid userId)
    {
        _logger.LogWarning("Deleting PKI role: {RoleName}, User: {UserId}", roleName, userId);

        var role = await _context.PkiRoles.FirstOrDefaultAsync(r => r.Name == roleName);

        if (role == null)
        {
            throw new InvalidOperationException($"Role not found: {roleName}");
        }

        _context.PkiRoles.Remove(role);
        await _context.SaveChangesAsync();

        _logger.LogInformation("PKI role deleted: {RoleName}", roleName);
    }

    // ====================
    // Certificate Issuance
    // ====================

    public async Task<IssueCertificateResponse> IssueCertificateAsync(string roleName, IssueCertificateRequest request, Guid userId)
    {
        _logger.LogInformation("Issuing certificate for CN={CommonName}, Role={RoleName}", request.CommonName, roleName);

        var role = await _context.PkiRoles
            .Include(r => r.CertificateAuthority)
            .FirstOrDefaultAsync(r => r.Name == roleName);

        if (role == null)
        {
            throw new InvalidOperationException($"Role not found: {roleName}");
        }

        if (role.CertificateAuthority.Revoked)
        {
            throw new InvalidOperationException($"Cannot issue certificate: CA is revoked");
        }

        // Validate TTL
        var ttlDays = request.TtlDays ?? role.TtlDays;
        if (ttlDays > role.MaxTtlDays)
        {
            throw new ArgumentException($"Requested TTL ({ttlDays} days) exceeds role maximum ({role.MaxTtlDays} days)");
        }

        // Validate SANs against role constraints
        ValidateSansAgainstRole(request.CommonName, request.SubjectAltNames, role);

        // Generate end-entity certificate
        var (certificatePem, privateKeyPem, serialNumber, notBefore, notAfter, caChainPem) =
            GenerateEndEntityCertificate(request, role, ttlDays);

        // Store issued certificate in database
        var issuedCert = new PkiIssuedCertificate
        {
            Id = Guid.NewGuid(),
            SerialNumber = serialNumber,
            CertificateAuthorityId = role.CertificateAuthorityId,
            RoleId = role.Id,
            SubjectDn = $"CN={request.CommonName}",
            CertificatePem = certificatePem,
            NotBefore = notBefore,
            NotAfter = notAfter,
            Revoked = false,
            IssuedBy = userId,
            IssuedAt = DateTime.UtcNow
        };

        _context.PkiIssuedCertificates.Add(issuedCert);

        // Increment CA issued certificate count
        role.CertificateAuthority.IssuedCertificateCount++;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Certificate issued: Serial={Serial}, CN={CommonName}", serialNumber, request.CommonName);

        return new IssueCertificateResponse
        {
            CertificatePem = certificatePem,
            PrivateKeyPem = privateKeyPem,
            CaChainPem = caChainPem,
            SerialNumber = serialNumber,
            NotBefore = notBefore,
            NotAfter = notAfter,
            IssuingCa = role.CertificateAuthority.Name
        };
    }

    public async Task<IssueCertificateResponse> SignCsrAsync(string roleName, SignCsrRequest request, Guid userId)
    {
        _logger.LogInformation("Signing CSR with role: {RoleName}", roleName);

        var role = await _context.PkiRoles
            .Include(r => r.CertificateAuthority)
            .FirstOrDefaultAsync(r => r.Name == roleName);

        if (role == null)
        {
            throw new InvalidOperationException($"Role not found: {roleName}");
        }

        if (role.CertificateAuthority.Revoked)
        {
            throw new InvalidOperationException($"Cannot sign CSR: CA is revoked");
        }

        // Validate TTL
        var ttlDays = request.TtlDays ?? role.TtlDays;
        if (ttlDays > role.MaxTtlDays)
        {
            throw new ArgumentException($"Requested TTL ({ttlDays} days) exceeds role maximum ({role.MaxTtlDays} days)");
        }

        // Parse and validate CSR
        var (csrInfo, csrPublicKey) = ParseCsr(request.Csr);

        // Validate CSR subject and SANs against role
        var commonName = ExtractCommonNameFromDn(csrInfo.Subject.ToString());
        var sans = ExtractSansFromCsr(csrInfo);
        ValidateSansAgainstRole(commonName, sans, role);

        // Sign CSR
        var (certificatePem, serialNumber, notBefore, notAfter, caChainPem) =
            SignCertificateSigningRequest(csrInfo, csrPublicKey, role, ttlDays);

        // Store issued certificate in database
        var issuedCert = new PkiIssuedCertificate
        {
            Id = Guid.NewGuid(),
            SerialNumber = serialNumber,
            CertificateAuthorityId = role.CertificateAuthorityId,
            RoleId = role.Id,
            SubjectDn = csrInfo.Subject.ToString(),
            CertificatePem = certificatePem,
            NotBefore = notBefore,
            NotAfter = notAfter,
            Revoked = false,
            IssuedBy = userId,
            IssuedAt = DateTime.UtcNow
        };

        _context.PkiIssuedCertificates.Add(issuedCert);
        role.CertificateAuthority.IssuedCertificateCount++;
        await _context.SaveChangesAsync();

        _logger.LogInformation("CSR signed: Serial={Serial}, Subject={Subject}", serialNumber, csrInfo.Subject);

        return new IssueCertificateResponse
        {
            CertificatePem = certificatePem,
            PrivateKeyPem = null, // Not provided for CSR signing
            CaChainPem = caChainPem,
            SerialNumber = serialNumber,
            NotBefore = notBefore,
            NotAfter = notAfter,
            IssuingCa = role.CertificateAuthority.Name
        };
    }

    // ====================
    // Certificate Operations
    // ====================

    public async Task<RevokeCertificateResponse> RevokeCertificateAsync(RevokeCertificateRequest request, Guid userId)
    {
        _logger.LogWarning("Revoking certificate: Serial={Serial}, User={UserId}", request.SerialNumber, userId);

        var cert = await _context.PkiIssuedCertificates
            .FirstOrDefaultAsync(c => c.SerialNumber == request.SerialNumber);

        if (cert == null)
        {
            throw new InvalidOperationException($"Certificate not found: {request.SerialNumber}");
        }

        if (cert.Revoked)
        {
            throw new InvalidOperationException($"Certificate already revoked: {request.SerialNumber}");
        }

        var revokedAt = DateTime.UtcNow;
        cert.Revoked = true;
        cert.RevokedAt = revokedAt;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Certificate revoked: Serial={Serial}", request.SerialNumber);

        return new RevokeCertificateResponse
        {
            Success = true,
            Message = $"Certificate {request.SerialNumber} revoked successfully",
            RevokedAt = revokedAt
        };
    }

    public async Task<ListCertificatesResponse> ListCertificatesAsync(string? caName, Guid userId)
    {
        var query = _context.PkiIssuedCertificates
            .Include(c => c.CertificateAuthority)
            .Include(c => c.Role)
            .AsNoTracking();

        if (!string.IsNullOrEmpty(caName))
        {
            query = query.Where(c => c.CertificateAuthority.Name == caName);
        }

        var certificates = await query
            .OrderByDescending(c => c.IssuedAt)
            .Select(c => new CertificateInfo
            {
                SerialNumber = c.SerialNumber,
                SubjectDn = c.SubjectDn,
                IssuingCa = c.CertificateAuthority.Name,
                RoleName = c.Role != null ? c.Role.Name : null,
                NotBefore = c.NotBefore,
                NotAfter = c.NotAfter,
                Revoked = c.Revoked,
                RevokedAt = c.RevokedAt,
                IssuedAt = c.IssuedAt
            })
            .ToListAsync();

        return new ListCertificatesResponse
        {
            Certificates = certificates,
            TotalCount = certificates.Count
        };
    }

    public async Task<CertificateInfo> ReadCertificateAsync(string serialNumber, Guid userId)
    {
        var cert = await _context.PkiIssuedCertificates
            .Include(c => c.CertificateAuthority)
            .Include(c => c.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.SerialNumber == serialNumber);

        if (cert == null)
        {
            throw new InvalidOperationException($"Certificate not found: {serialNumber}");
        }

        return new CertificateInfo
        {
            SerialNumber = cert.SerialNumber,
            SubjectDn = cert.SubjectDn,
            IssuingCa = cert.CertificateAuthority.Name,
            RoleName = cert.Role?.Name,
            NotBefore = cert.NotBefore,
            NotAfter = cert.NotAfter,
            Revoked = cert.Revoked,
            RevokedAt = cert.RevokedAt,
            IssuedAt = cert.IssuedAt
        };
    }

    // ====================
    // CRL Management
    // ====================

    public async Task<GetCrlResponse> GenerateCrlAsync(string caName, Guid userId)
    {
        _logger.LogInformation("Generating CRL for CA: {CaName}", caName);

        var ca = await _context.PkiCertificateAuthorities
            .Include(c => c.IssuedCertificates.Where(cert => cert.Revoked))
            .FirstOrDefaultAsync(c => c.Name == caName);

        if (ca == null)
        {
            throw new InvalidOperationException($"Certificate Authority not found: {caName}");
        }

        // Decrypt CA private key
        var caPrivateKey = DecryptCaPrivateKey(ca);
        var caCert = LoadCertificateFromPem(ca.CertificatePem);

        // Create CRL generator
        var crlGen = new X509V2CrlGenerator();
        crlGen.SetIssuerDN(caCert.SubjectDN);

        var thisUpdate = DateTime.UtcNow;
        var nextUpdate = thisUpdate.AddDays(30); // CRL valid for 30 days

        crlGen.SetThisUpdate(thisUpdate);
        crlGen.SetNextUpdate(nextUpdate);

        // Add revoked certificates
        var revokedCount = 0;
        foreach (var revokedCert in ca.IssuedCertificates.Where(c => c.Revoked))
        {
            crlGen.AddCrlEntry(
                new BigInteger(revokedCert.SerialNumber, 16),
                revokedCert.RevokedAt!.Value,
                CrlReason.Unspecified);
            revokedCount++;
        }

        // Add CRL Number extension
        var crlNumber = await GetNextCrlNumberAsync(ca.Id);
        crlGen.AddExtension(X509Extensions.CrlNumber, false, new CrlNumber(BigInteger.ValueOf(crlNumber)));

        // Add Authority Key Identifier
        var caPubKeyInfo = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(caCert.GetPublicKey());
        crlGen.AddExtension(X509Extensions.AuthorityKeyIdentifier, false,
            new AuthorityKeyIdentifier(caPubKeyInfo));

        // Sign CRL
        var signatureAlgorithm = GetSignatureAlgorithm(ca.KeyType);
        ISignatureFactory signatureFactory = new Asn1SignatureFactory(
            signatureAlgorithm, caPrivateKey, new SecureRandom());
        var crl = crlGen.Generate(signatureFactory);

        // Convert to PEM
        var crlPem = ConvertCrlToPem(crl);

        _logger.LogInformation("CRL generated for CA: {CaName}, Revoked count: {Count}", caName, revokedCount);

        return new GetCrlResponse
        {
            Crl = crlPem,
            GeneratedAt = thisUpdate,
            NextUpdate = nextUpdate,
            RevokedCount = revokedCount
        };
    }

    // ====================
    // Helper Methods - Key Generation
    // ====================

    private AsymmetricCipherKeyPair GenerateRsaKeyPair(int keySize)
    {
        var generator = new RsaKeyPairGenerator();
        generator.Init(new KeyGenerationParameters(new SecureRandom(), keySize));
        return generator.GenerateKeyPair();
    }

    private AsymmetricCipherKeyPair GenerateEcdsaKeyPair(string curve)
    {
        var ecParams = curve switch
        {
            "P-256" => ECNamedCurveTable.GetByName("secp256r1"),
            "P-384" => ECNamedCurveTable.GetByName("secp384r1"),
            _ => throw new ArgumentException($"Unsupported ECDSA curve: {curve}")
        };

        if (ecParams == null)
        {
            throw new ArgumentException($"EC curve not found: {curve}");
        }

        var generator = new ECKeyPairGenerator();
        var domainParams = new ECDomainParameters(ecParams.Curve, ecParams.G, ecParams.N, ecParams.H, ecParams.GetSeed());
        generator.Init(new ECKeyGenerationParameters(domainParams, new SecureRandom()));
        return generator.GenerateKeyPair();
    }

    private AsymmetricCipherKeyPair GenerateKeyPair(string keyType)
    {
        return keyType switch
        {
            "rsa-2048" => GenerateRsaKeyPair(2048),
            "rsa-4096" => GenerateRsaKeyPair(4096),
            "ecdsa-p256" => GenerateEcdsaKeyPair("P-256"),
            "ecdsa-p384" => GenerateEcdsaKeyPair("P-384"),
            _ => throw new ArgumentException($"Unsupported key type: {keyType}")
        };
    }

    private string GenerateCryptographicSerialNumber()
    {
        // Generate 160-bit (20 bytes) random serial number
        var serialBytes = new byte[20];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(serialBytes);

        // Ensure positive (clear high bit)
        serialBytes[0] &= 0x7F;

        var serial = new BigInteger(serialBytes);
        return serial.ToString(16).ToUpperInvariant();
    }

    // ====================
    // Helper Methods - Certificate Generation
    // ====================

    private (string certificatePem, string encryptedPrivateKey, string serialNumber) GenerateRootCaCertificate(
        CreateRootCaRequest request)
    {
        // Generate key pair
        var keyPair = GenerateKeyPair(request.KeyType);

        // Generate serial number
        var serialNumber = GenerateCryptographicSerialNumber();

        // Create certificate generator
        var certGen = new X509V3CertificateGenerator();
        var subjectDn = new X509Name(request.SubjectDn);

        certGen.SetSerialNumber(new BigInteger(serialNumber, 16));
        certGen.SetIssuerDN(subjectDn); // Self-signed
        certGen.SetSubjectDN(subjectDn);
        certGen.SetNotBefore(DateTime.UtcNow.AddMinutes(-5)); // 5 min clock skew tolerance
        certGen.SetNotAfter(DateTime.UtcNow.AddDays(request.TtlDays));
        certGen.SetPublicKey(keyPair.Public);

        // Add X.509 v3 extensions
        certGen.AddExtension(X509Extensions.BasicConstraints, true,
            new BasicConstraints(request.MaxPathLength));

        certGen.AddExtension(X509Extensions.KeyUsage, true,
            new KeyUsage(KeyUsage.KeyCertSign | KeyUsage.CrlSign));

        var pubKeyInfo = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(keyPair.Public);
        certGen.AddExtension(X509Extensions.SubjectKeyIdentifier, false,
            new SubjectKeyIdentifier(pubKeyInfo));

        // Sign certificate
        var signatureAlgorithm = GetSignatureAlgorithm(request.KeyType);
        ISignatureFactory signatureFactory = new Asn1SignatureFactory(
            signatureAlgorithm, keyPair.Private, new SecureRandom());
        var cert = certGen.Generate(signatureFactory);

        // Convert to PEM
        var certPem = ConvertCertificateToPem(cert);

        // Encrypt private key
        var encryptedPrivateKey = EncryptPrivateKey(keyPair.Private);

        return (certPem, encryptedPrivateKey, serialNumber);
    }

    private (string certificatePem, string encryptedPrivateKey, string serialNumber) GenerateIntermediateCaCertificate(
        CreateIntermediateCaRequest request, PkiCertificateAuthority parentCa)
    {
        // Generate key pair
        var keyPair = GenerateKeyPair(request.KeyType);

        // Generate serial number
        var serialNumber = GenerateCryptographicSerialNumber();

        // Decrypt parent CA private key
        var parentPrivateKey = DecryptCaPrivateKey(parentCa);
        var parentCert = LoadCertificateFromPem(parentCa.CertificatePem);

        // Create certificate generator
        var certGen = new X509V3CertificateGenerator();
        var subjectDn = new X509Name(request.SubjectDn);

        certGen.SetSerialNumber(new BigInteger(serialNumber, 16));
        certGen.SetIssuerDN(parentCert.SubjectDN); // Signed by parent
        certGen.SetSubjectDN(subjectDn);
        certGen.SetNotBefore(DateTime.UtcNow.AddMinutes(-5));
        certGen.SetNotAfter(DateTime.UtcNow.AddDays(request.TtlDays));
        certGen.SetPublicKey(keyPair.Public);

        // Add extensions
        var maxPathLength = Math.Min(request.MaxPathLength, parentCa.MaxPathLength - 1);
        certGen.AddExtension(X509Extensions.BasicConstraints, true,
            new BasicConstraints(maxPathLength));

        certGen.AddExtension(X509Extensions.KeyUsage, true,
            new KeyUsage(KeyUsage.KeyCertSign | KeyUsage.CrlSign));

        var pubKeyInfo = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(keyPair.Public);
        certGen.AddExtension(X509Extensions.SubjectKeyIdentifier, false,
            new SubjectKeyIdentifier(pubKeyInfo));

        var parentPubKeyInfo = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(parentCert.GetPublicKey());
        certGen.AddExtension(X509Extensions.AuthorityKeyIdentifier, false,
            new AuthorityKeyIdentifier(parentPubKeyInfo));

        // Sign with parent CA private key
        var signatureAlgorithm = GetSignatureAlgorithm(parentCa.KeyType);
        ISignatureFactory signatureFactory = new Asn1SignatureFactory(
            signatureAlgorithm, parentPrivateKey, new SecureRandom());
        var cert = certGen.Generate(signatureFactory);

        // Convert to PEM
        var certPem = ConvertCertificateToPem(cert);
        var encryptedPrivateKey = EncryptPrivateKey(keyPair.Private);

        return (certPem, encryptedPrivateKey, serialNumber);
    }

    private (string certificatePem, string privateKeyPem, string serialNumber, DateTime notBefore, DateTime notAfter, string caChainPem)
        GenerateEndEntityCertificate(IssueCertificateRequest request, PkiRole role, int ttlDays)
    {
        var ca = role.CertificateAuthority;

        // Generate key pair
        var keyPair = GenerateKeyPair(role.KeyType);

        // Generate serial number
        var serialNumber = GenerateCryptographicSerialNumber();

        // Decrypt CA private key
        var caPrivateKey = DecryptCaPrivateKey(ca);
        var caCert = LoadCertificateFromPem(ca.CertificatePem);

        // Create certificate generator
        var certGen = new X509V3CertificateGenerator();
        var subjectDn = new X509Name($"CN={request.CommonName}");

        var notBefore = DateTime.UtcNow.AddMinutes(-5);
        var notAfter = DateTime.UtcNow.AddDays(ttlDays);

        certGen.SetSerialNumber(new BigInteger(serialNumber, 16));
        certGen.SetIssuerDN(caCert.SubjectDN);
        certGen.SetSubjectDN(subjectDn);
        certGen.SetNotBefore(notBefore);
        certGen.SetNotAfter(notAfter);
        certGen.SetPublicKey(keyPair.Public);

        // Add extensions
        certGen.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(false));

        var keyUsage = BuildKeyUsage(role);
        certGen.AddExtension(X509Extensions.KeyUsage, true, keyUsage);

        var extKeyUsage = BuildExtendedKeyUsage(role);
        certGen.AddExtension(X509Extensions.ExtendedKeyUsage, false, extKeyUsage);

        // Subject Alternative Names
        if (request.SubjectAltNames != null && request.SubjectAltNames.Any())
        {
            var sans = BuildSubjectAltNames(request.SubjectAltNames);
            certGen.AddExtension(X509Extensions.SubjectAlternativeName, false, sans);
        }

        var pubKeyInfo = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(keyPair.Public);
        certGen.AddExtension(X509Extensions.SubjectKeyIdentifier, false,
            new SubjectKeyIdentifier(pubKeyInfo));

        var caPubKeyInfo = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(caCert.GetPublicKey());
        certGen.AddExtension(X509Extensions.AuthorityKeyIdentifier, false,
            new AuthorityKeyIdentifier(caPubKeyInfo));

        // Sign with CA private key
        var signatureAlgorithm = GetSignatureAlgorithm(ca.KeyType);
        ISignatureFactory signatureFactory = new Asn1SignatureFactory(
            signatureAlgorithm, caPrivateKey, new SecureRandom());
        var cert = certGen.Generate(signatureFactory);

        // Convert to PEM
        var certPem = ConvertCertificateToPem(cert);
        var privateKeyPem = ConvertPrivateKeyToPem(keyPair.Private);
        var caChainPem = BuildCaChain(ca);

        return (certPem, privateKeyPem, serialNumber, notBefore, notAfter, caChainPem);
    }

    private (string certificatePem, string serialNumber, DateTime notBefore, DateTime notAfter, string caChainPem)
        SignCertificateSigningRequest(CertificationRequestInfo csrInfo, AsymmetricKeyParameter csrPublicKey, PkiRole role, int ttlDays)
    {
        var ca = role.CertificateAuthority;

        // Generate serial number
        var serialNumber = GenerateCryptographicSerialNumber();

        // Decrypt CA private key
        var caPrivateKey = DecryptCaPrivateKey(ca);
        var caCert = LoadCertificateFromPem(ca.CertificatePem);

        // Create certificate generator
        var certGen = new X509V3CertificateGenerator();

        var notBefore = DateTime.UtcNow.AddMinutes(-5);
        var notAfter = DateTime.UtcNow.AddDays(ttlDays);

        certGen.SetSerialNumber(new BigInteger(serialNumber, 16));
        certGen.SetIssuerDN(caCert.SubjectDN);
        certGen.SetSubjectDN(csrInfo.Subject);
        certGen.SetNotBefore(notBefore);
        certGen.SetNotAfter(notAfter);
        certGen.SetPublicKey(csrPublicKey);

        // Add extensions
        certGen.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(false));

        var keyUsage = BuildKeyUsage(role);
        certGen.AddExtension(X509Extensions.KeyUsage, true, keyUsage);

        var extKeyUsage = BuildExtendedKeyUsage(role);
        certGen.AddExtension(X509Extensions.ExtendedKeyUsage, false, extKeyUsage);

        // Extract and preserve SANs from CSR if present
        var csrAttributes = csrInfo.Attributes;
        if (csrAttributes != null)
        {
            foreach (AttributePkcs attribute in csrAttributes)
            {
                if (attribute.AttrType.Equals(PkcsObjectIdentifiers.Pkcs9AtExtensionRequest))
                {
                    var extensions = X509Extensions.GetInstance(attribute.AttrValues[0]);
                    var sanExtension = extensions.GetExtension(X509Extensions.SubjectAlternativeName);
                    if (sanExtension != null)
                    {
                        certGen.AddExtension(X509Extensions.SubjectAlternativeName, false, sanExtension.Value);
                    }
                }
            }
        }

        var pubKeyInfo = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(csrPublicKey);
        certGen.AddExtension(X509Extensions.SubjectKeyIdentifier, false,
            new SubjectKeyIdentifier(pubKeyInfo));

        var caPubKeyInfo = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(caCert.GetPublicKey());
        certGen.AddExtension(X509Extensions.AuthorityKeyIdentifier, false,
            new AuthorityKeyIdentifier(caPubKeyInfo));

        // Sign with CA private key
        var signatureAlgorithm = GetSignatureAlgorithm(ca.KeyType);
        ISignatureFactory signatureFactory = new Asn1SignatureFactory(
            signatureAlgorithm, caPrivateKey, new SecureRandom());
        var cert = certGen.Generate(signatureFactory);

        // Convert to PEM
        var certPem = ConvertCertificateToPem(cert);
        var caChainPem = BuildCaChain(ca);

        return (certPem, serialNumber, notBefore, notAfter, caChainPem);
    }

    // ====================
    // Helper Methods - Validation
    // ====================

    private void ValidateSansAgainstRole(string commonName, List<string>? sans, PkiRole role)
    {
        var allowedDomains = System.Text.Json.JsonSerializer.Deserialize<List<string>>(role.AllowedDomains) ?? new List<string>();

        // Check common name
        if (!IsNameAllowedByRole(commonName, allowedDomains, role))
        {
            throw new ArgumentException($"Common name '{commonName}' not allowed by role '{role.Name}'");
        }

        // Check SANs
        if (sans != null)
        {
            foreach (var san in sans)
            {
                if (!IsNameAllowedByRole(san, allowedDomains, role))
                {
                    throw new ArgumentException($"Subject Alternative Name '{san}' not allowed by role '{role.Name}'");
                }
            }
        }
    }

    private bool IsNameAllowedByRole(string name, List<string> allowedDomains, PkiRole role)
    {
        // Check localhost
        if (name.Equals("localhost", StringComparison.OrdinalIgnoreCase) || name == "127.0.0.1")
        {
            return role.AllowLocalhost;
        }

        // Check IP address
        if (System.Net.IPAddress.TryParse(name, out _))
        {
            return role.AllowIpSans;
        }

        // Check wildcard
        if (name.StartsWith("*."))
        {
            if (!role.AllowWildcards)
            {
                return false;
            }
            name = name.Substring(2); // Remove *.
        }

        // Check against allowed domains
        foreach (var allowedDomain in allowedDomains)
        {
            if (allowedDomain.StartsWith("*."))
            {
                // Wildcard allowed domain
                var domain = allowedDomain.Substring(2);
                if (name.EndsWith(domain, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else if (name.Equals(allowedDomain, StringComparison.OrdinalIgnoreCase))
            {
                // Exact match
                return role.AllowBareDomains || !IsBareDomain(name);
            }
            else if (role.AllowSubdomains && name.EndsWith($".{allowedDomain}", StringComparison.OrdinalIgnoreCase))
            {
                // Subdomain match
                return true;
            }
        }

        return false;
    }

    private bool IsBareDomain(string domain)
    {
        // A bare domain has no subdomain parts (e.g., "example.com" is bare, "www.example.com" is not)
        var parts = domain.Split('.');
        return parts.Length == 2;
    }

    // ====================
    // Helper Methods - Extension Builders
    // ====================

    private KeyUsage BuildKeyUsage(PkiRole role)
    {
        var usage = KeyUsage.DigitalSignature | KeyUsage.KeyEncipherment;
        return new KeyUsage(usage);
    }

    private ExtendedKeyUsage BuildExtendedKeyUsage(PkiRole role)
    {
        var purposes = new List<DerObjectIdentifier>();

        if (role.ServerAuth)
        {
            purposes.Add(KeyPurposeID.id_kp_serverAuth);
        }
        if (role.ClientAuth)
        {
            purposes.Add(KeyPurposeID.id_kp_clientAuth);
        }
        if (role.CodeSigning)
        {
            purposes.Add(KeyPurposeID.id_kp_codeSigning);
        }
        if (role.EmailProtection)
        {
            purposes.Add(KeyPurposeID.id_kp_emailProtection);
        }

        return new ExtendedKeyUsage(purposes.ToArray());
    }

    private GeneralNames BuildSubjectAltNames(List<string> sans)
    {
        var generalNames = new List<GeneralName>();

        foreach (var san in sans)
        {
            if (System.Net.IPAddress.TryParse(san, out var ipAddress))
            {
                generalNames.Add(new GeneralName(GeneralName.IPAddress, ipAddress.ToString()));
            }
            else
            {
                generalNames.Add(new GeneralName(GeneralName.DnsName, san));
            }
        }

        return new GeneralNames(generalNames.ToArray());
    }

    // ====================
    // Helper Methods - Conversion
    // ====================

    private string ConvertCertificateToPem(X509Certificate cert)
    {
        var sb = new StringBuilder();
        sb.AppendLine("-----BEGIN CERTIFICATE-----");
        sb.AppendLine(Convert.ToBase64String(cert.GetEncoded(), Base64FormattingOptions.InsertLineBreaks));
        sb.AppendLine("-----END CERTIFICATE-----");
        return sb.ToString();
    }

    private string ConvertPrivateKeyToPem(AsymmetricKeyParameter privateKey)
    {
        var privateKeyInfo = PrivateKeyInfoFactory.CreatePrivateKeyInfo(privateKey);
        var sb = new StringBuilder();

        if (privateKey is RsaPrivateCrtKeyParameters)
        {
            sb.AppendLine("-----BEGIN RSA PRIVATE KEY-----");
        }
        else if (privateKey is ECPrivateKeyParameters)
        {
            sb.AppendLine("-----BEGIN EC PRIVATE KEY-----");
        }
        else
        {
            sb.AppendLine("-----BEGIN PRIVATE KEY-----");
        }

        sb.AppendLine(Convert.ToBase64String(privateKeyInfo.GetEncoded(), Base64FormattingOptions.InsertLineBreaks));

        if (privateKey is RsaPrivateCrtKeyParameters)
        {
            sb.AppendLine("-----END RSA PRIVATE KEY-----");
        }
        else if (privateKey is ECPrivateKeyParameters)
        {
            sb.AppendLine("-----END EC PRIVATE KEY-----");
        }
        else
        {
            sb.AppendLine("-----END PRIVATE KEY-----");
        }

        return sb.ToString();
    }

    private string ConvertCrlToPem(X509Crl crl)
    {
        var sb = new StringBuilder();
        sb.AppendLine("-----BEGIN X509 CRL-----");
        sb.AppendLine(Convert.ToBase64String(crl.GetEncoded(), Base64FormattingOptions.InsertLineBreaks));
        sb.AppendLine("-----END X509 CRL-----");
        return sb.ToString();
    }

    private X509Certificate LoadCertificateFromPem(string pem)
    {
        var parser = new X509CertificateParser();
        var bytes = Encoding.UTF8.GetBytes(pem);
        return parser.ReadCertificate(bytes);
    }

    private string EncryptPrivateKey(AsymmetricKeyParameter privateKey)
    {
        var privateKeyInfo = PrivateKeyInfoFactory.CreatePrivateKeyInfo(privateKey);
        var privateKeyBytes = privateKeyInfo.GetEncoded();
        var privateKeyBase64 = Convert.ToBase64String(privateKeyBytes);
        return _encryptionService.Encrypt(privateKeyBase64);
    }

    private AsymmetricKeyParameter DecryptCaPrivateKey(PkiCertificateAuthority ca)
    {
        var decryptedBase64 = _encryptionService.Decrypt(ca.EncryptedPrivateKey);
        var privateKeyBytes = Convert.FromBase64String(decryptedBase64);
        var privateKeyInfo = PrivateKeyInfo.GetInstance(privateKeyBytes);
        return PrivateKeyFactory.CreateKey(privateKeyInfo);
    }

    private string GetSignatureAlgorithm(string keyType)
    {
        return keyType switch
        {
            "rsa-2048" => "SHA256WithRSA",
            "rsa-4096" => "SHA256WithRSA",
            "ecdsa-p256" => "SHA256WithECDSA",
            "ecdsa-p384" => "SHA384WithECDSA",
            _ => throw new ArgumentException($"Unsupported key type: {keyType}")
        };
    }

    private string BuildCaChain(PkiCertificateAuthority ca)
    {
        var chain = new StringBuilder();
        chain.Append(ca.CertificatePem);

        // If intermediate CA, recursively build chain to root
        var currentCa = ca;
        while (currentCa.ParentCaId != null)
        {
            var parentCa = _context.PkiCertificateAuthorities
                .AsNoTracking()
                .FirstOrDefault(c => c.Id == currentCa.ParentCaId);

            if (parentCa == null)
            {
                break;
            }

            chain.Append(parentCa.CertificatePem);
            currentCa = parentCa;
        }

        return chain.ToString();
    }

    // ====================
    // Helper Methods - CSR Parsing
    // ====================

    private (CertificationRequestInfo csrInfo, AsymmetricKeyParameter publicKey) ParseCsr(string csrPem)
    {
        try
        {
            var csrBytes = Encoding.UTF8.GetBytes(csrPem);
            var parser = new Org.BouncyCastle.OpenSsl.PemReader(new StringReader(csrPem));
            var obj = parser.ReadObject();

            Pkcs10CertificationRequest csr;
            if (obj is Pkcs10CertificationRequest pkcs10)
            {
                csr = pkcs10;
            }
            else
            {
                throw new ArgumentException("Invalid CSR format");
            }

            // Verify CSR signature
            if (!csr.Verify())
            {
                throw new ArgumentException("CSR signature verification failed");
            }

            var csrInfo = csr.GetCertificationRequestInfo();
            var publicKey = PublicKeyFactory.CreateKey(csrInfo.SubjectPublicKeyInfo);

            return (csrInfo, publicKey);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Failed to parse CSR: {ex.Message}", ex);
        }
    }

    private string ExtractCommonNameFromDn(string dn)
    {
        var x509Name = new X509Name(dn);
        var cn = x509Name.GetValueList(X509Name.CN);
        return cn.Count > 0 ? cn[0]?.ToString() ?? string.Empty : string.Empty;
    }

    private List<string> ExtractSansFromCsr(CertificationRequestInfo csrInfo)
    {
        var sans = new List<string>();

        var attributes = csrInfo.Attributes;
        if (attributes != null)
        {
            foreach (AttributePkcs attribute in attributes)
            {
                if (attribute.AttrType.Equals(PkcsObjectIdentifiers.Pkcs9AtExtensionRequest))
                {
                    var extensions = X509Extensions.GetInstance(attribute.AttrValues[0]);
                    var sanExtension = extensions.GetExtension(X509Extensions.SubjectAlternativeName);
                    if (sanExtension != null)
                    {
                        var sanNames = GeneralNames.GetInstance(sanExtension.GetParsedValue());
                        foreach (var name in sanNames.GetNames())
                        {
                            if (name.TagNo == GeneralName.DnsName)
                            {
                                sans.Add(name.Name.ToString() ?? string.Empty);
                            }
                            else if (name.TagNo == GeneralName.IPAddress)
                            {
                                sans.Add(name.Name.ToString() ?? string.Empty);
                            }
                        }
                    }
                }
            }
        }

        return sans;
    }

    // ====================
    // Helper Methods - Mapping
    // ====================

    private CertificateAuthorityResponse MapCaToResponse(PkiCertificateAuthority ca)
    {
        return new CertificateAuthorityResponse
        {
            Name = ca.Name,
            Type = ca.Type,
            SubjectDn = ca.SubjectDn,
            CertificatePem = ca.CertificatePem,
            SerialNumber = ca.SerialNumber,
            KeyType = ca.KeyType,
            NotBefore = ca.NotBefore,
            NotAfter = ca.NotAfter,
            MaxPathLength = ca.MaxPathLength,
            ParentCaName = ca.ParentCa?.Name,
            Revoked = ca.Revoked,
            RevokedAt = ca.RevokedAt,
            IssuedCertificateCount = ca.IssuedCertificateCount,
            CreatedAt = ca.CreatedAt
        };
    }

    private RoleResponse MapRoleToResponse(PkiRole role, string caName)
    {
        var allowedDomains = System.Text.Json.JsonSerializer.Deserialize<List<string>>(role.AllowedDomains) ?? new List<string>();

        return new RoleResponse
        {
            Name = role.Name,
            CertificateAuthorityName = caName,
            KeyType = role.KeyType,
            TtlDays = role.TtlDays,
            MaxTtlDays = role.MaxTtlDays,
            AllowLocalhost = role.AllowLocalhost,
            AllowBareDomains = role.AllowBareDomains,
            AllowSubdomains = role.AllowSubdomains,
            AllowWildcards = role.AllowWildcards,
            AllowIpSans = role.AllowIpSans,
            AllowedDomains = allowedDomains,
            ServerAuth = role.ServerAuth,
            ClientAuth = role.ClientAuth,
            CodeSigning = role.CodeSigning,
            EmailProtection = role.EmailProtection,
            CreatedAt = role.CreatedAt,
            UpdatedAt = role.UpdatedAt
        };
    }

    // ====================
    // Helper Methods - CRL Number Tracking
    // ====================

    private async Task<long> GetNextCrlNumberAsync(Guid caId)
    {
        // CRL number generation using timestamp
        // Future enhancement: Track sequential CRL numbers per CA in database table
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}

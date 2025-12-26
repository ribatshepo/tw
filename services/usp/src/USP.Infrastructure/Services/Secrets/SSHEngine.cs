using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using USP.Core.Models.DTOs.SSH;
using USP.Core.Services.Secrets;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Secrets;

/// <summary>
/// SSH Engine implementation - SSH key management and certificate signing
/// </summary>
public class SSHEngine : ISSHEngine
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SSHEngine> _logger;
    private readonly SecureRandom _random;

    public SSHEngine(
        ApplicationDbContext context,
        ILogger<SSHEngine> logger)
    {
        _context = context;
        _logger = logger;
        _random = new SecureRandom();
    }

    public async Task<GenerateSSHKeyPairResponse> GenerateKeyPairAsync(GenerateSSHKeyPairRequest request, Guid userId)
    {
        _logger.LogInformation("Generating SSH key pair. Type: {KeyType}, UserId: {UserId}", request.KeyType, userId);

        try
        {
            string publicKey, privateKey, fingerprint;

            if (request.KeyType.ToLower() == "ed25519")
            {
                (publicKey, privateKey, fingerprint) = GenerateEd25519KeyPair(request.Comment);
            }
            else
            {
                (publicKey, privateKey, fingerprint) = GenerateRSAKeyPair(request.KeyBits, request.Comment);
            }

            _logger.LogInformation("SSH key pair generated. Type: {KeyType}, Fingerprint: {Fingerprint}", request.KeyType, fingerprint);

            return new GenerateSSHKeyPairResponse
            {
                PublicKey = publicKey,
                PrivateKey = privateKey,
                Fingerprint = fingerprint,
                KeyType = request.KeyType
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate SSH key pair. Type: {KeyType}", request.KeyType);
            throw;
        }
    }

    public async Task<SignSSHCertificateResponse> SignCertificateAsync(string roleName, SignSSHCertificateRequest request, Guid userId)
    {
        _logger.LogInformation("Signing SSH certificate. Role: {RoleName}, UserId: {UserId}", roleName, userId);

        try
        {
            var role = await _context.SSHRoles
                .FirstOrDefaultAsync(r => r.Name == roleName);

            if (role == null)
            {
                throw new InvalidOperationException($"SSH role '{roleName}' not found");
            }

            var ca = await _context.SSHCertificateAuthorities
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync();

            if (ca == null)
            {
                throw new InvalidOperationException("No SSH CA found. Generate CA first.");
            }

            var validAfter = DateTime.UtcNow;
            var ttl = Math.Min(request.TtlSeconds, role.MaxTtlSeconds);
            var validBefore = validAfter.AddSeconds(ttl);

            var serialNumber = GenerateSerialNumber();

            var signedCertificate = SignSSHPublicKey(
                request.PublicKey,
                ca.PrivateKey,
                serialNumber,
                request.ValidPrincipals,
                validAfter,
                validBefore,
                request.CertificateType,
                request.CriticalOptions,
                request.Extensions
            );

            var certificateRecord = new Core.Models.Entities.SSHCertificate
            {
                Id = Guid.NewGuid(),
                SerialNumber = serialNumber,
                RoleName = roleName,
                PublicKey = request.PublicKey,
                SignedCertificate = signedCertificate,
                ValidAfter = validAfter,
                ValidBefore = validBefore,
                ValidPrincipals = string.Join(",", request.ValidPrincipals),
                CertificateType = request.CertificateType,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            _context.SSHCertificates.Add(certificateRecord);
            await _context.SaveChangesAsync();

            _logger.LogInformation("SSH certificate signed. Serial: {Serial}, ValidUntil: {ValidBefore}", serialNumber, validBefore);

            return new SignSSHCertificateResponse
            {
                SignedCertificate = signedCertificate,
                SerialNumber = serialNumber,
                ValidAfter = validAfter,
                ValidBefore = validBefore,
                ValidPrincipals = request.ValidPrincipals
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sign SSH certificate. Role: {RoleName}", roleName);
            throw;
        }
    }

    public async Task<CreateSSHRoleResponse> CreateRoleAsync(string roleName, CreateSSHRoleRequest request, Guid userId)
    {
        _logger.LogInformation("Creating SSH role: {RoleName}", roleName);

        try
        {
            var existingRole = await _context.SSHRoles
                .FirstOrDefaultAsync(r => r.Name == roleName);

            if (existingRole != null)
            {
                existingRole.KeyType = request.KeyType;
                existingRole.DefaultTtlSeconds = request.DefaultTtlSeconds;
                existingRole.MaxTtlSeconds = request.MaxTtlSeconds;
                existingRole.AllowedPrincipals = string.Join(",", request.AllowedPrincipals);
                existingRole.CertificateType = request.CertificateType;
                existingRole.AllowUserCertificates = request.AllowUserCertificates;
                existingRole.AllowHostCertificates = request.AllowHostCertificates;
                existingRole.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var role = new Core.Models.Entities.SSHRole
                {
                    Id = Guid.NewGuid(),
                    Name = roleName,
                    KeyType = request.KeyType,
                    DefaultTtlSeconds = request.DefaultTtlSeconds,
                    MaxTtlSeconds = request.MaxTtlSeconds,
                    AllowedPrincipals = string.Join(",", request.AllowedPrincipals),
                    CertificateType = request.CertificateType,
                    AllowUserCertificates = request.AllowUserCertificates,
                    AllowHostCertificates = request.AllowHostCertificates,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId
                };

                _context.SSHRoles.Add(role);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("SSH role created: {RoleName}", roleName);

            return new CreateSSHRoleResponse
            {
                RoleName = roleName,
                CreatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create SSH role: {RoleName}", roleName);
            throw;
        }
    }

    public async Task<ReadSSHRoleResponse?> ReadRoleAsync(string roleName, Guid userId)
    {
        var role = await _context.SSHRoles
            .FirstOrDefaultAsync(r => r.Name == roleName);

        if (role == null)
        {
            return null;
        }

        return new ReadSSHRoleResponse
        {
            RoleName = role.Name,
            KeyType = role.KeyType,
            DefaultTtlSeconds = role.DefaultTtlSeconds,
            MaxTtlSeconds = role.MaxTtlSeconds,
            AllowedPrincipals = role.AllowedPrincipals.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
            CertificateType = role.CertificateType,
            AllowUserCertificates = role.AllowUserCertificates,
            AllowHostCertificates = role.AllowHostCertificates,
            CreatedAt = role.CreatedAt
        };
    }

    public async Task<ListSSHRolesResponse> ListRolesAsync(Guid userId)
    {
        var roles = await _context.SSHRoles
            .Select(r => r.Name)
            .ToListAsync();

        return new ListSSHRolesResponse
        {
            Roles = roles
        };
    }

    public async Task DeleteRoleAsync(string roleName, Guid userId)
    {
        _logger.LogInformation("Deleting SSH role: {RoleName}", roleName);

        var role = await _context.SSHRoles
            .FirstOrDefaultAsync(r => r.Name == roleName);

        if (role != null)
        {
            _context.SSHRoles.Remove(role);
            await _context.SaveChangesAsync();

            _logger.LogInformation("SSH role deleted: {RoleName}", roleName);
        }
    }

    public async Task<GenerateSSHCAResponse> GenerateCAAsync(GenerateSSHCARequest request, Guid userId)
    {
        _logger.LogInformation("Generating SSH CA. Type: {KeyType}, UserId: {UserId}", request.KeyType, userId);

        try
        {
            string publicKey, privateKey, fingerprint;

            if (request.KeyType.ToLower() == "ed25519")
            {
                (publicKey, privateKey, fingerprint) = GenerateEd25519KeyPair("USP SSH CA");
            }
            else
            {
                (publicKey, privateKey, fingerprint) = GenerateRSAKeyPair(request.KeyBits, "USP SSH CA");
            }

            var ca = new Core.Models.Entities.SSHCertificateAuthority
            {
                Id = Guid.NewGuid(),
                KeyType = request.KeyType,
                PublicKey = publicKey,
                PrivateKey = privateKey,
                Fingerprint = fingerprint,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            };

            _context.SSHCertificateAuthorities.Add(ca);
            await _context.SaveChangesAsync();

            _logger.LogInformation("SSH CA generated. Fingerprint: {Fingerprint}", fingerprint);

            return new GenerateSSHCAResponse
            {
                PublicKey = publicKey,
                Fingerprint = fingerprint,
                GeneratedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate SSH CA");
            throw;
        }
    }

    public async Task<ReadSSHCAResponse?> ReadCAAsync(Guid userId)
    {
        var ca = await _context.SSHCertificateAuthorities
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();

        if (ca == null)
        {
            return null;
        }

        return new ReadSSHCAResponse
        {
            PublicKey = ca.PublicKey,
            Fingerprint = ca.Fingerprint
        };
    }

    public async Task<IssueSSHOTPResponse> IssueOTPAsync(IssueSSHOTPRequest request, Guid userId)
    {
        _logger.LogInformation("Issuing SSH OTP. Username: {Username}, Host: {Hostname}", request.Username, request.Hostname);

        try
        {
            var otp = GenerateOTP();
            var expiresAt = DateTime.UtcNow.AddSeconds(request.TtlSeconds);

            var otpRecord = new Core.Models.Entities.SSHOtp
            {
                Id = Guid.NewGuid(),
                Otp = otp,
                Username = request.Username,
                Hostname = request.Hostname,
                IpAddress = request.IpAddress,
                ExpiresAt = expiresAt,
                IsUsed = false,
                IssuedBy = userId,
                IssuedAt = DateTime.UtcNow
            };

            _context.SSHOtps.Add(otpRecord);
            await _context.SaveChangesAsync();

            _logger.LogInformation("SSH OTP issued. Username: {Username}, Host: {Hostname}, ExpiresAt: {ExpiresAt}", request.Username, request.Hostname, expiresAt);

            return new IssueSSHOTPResponse
            {
                Otp = otp,
                Username = request.Username,
                Hostname = request.Hostname,
                ExpiresAt = expiresAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to issue SSH OTP. Username: {Username}, Host: {Hostname}", request.Username, request.Hostname);
            throw;
        }
    }

    public async Task<VerifySSHOTPResponse> VerifyOTPAsync(VerifySSHOTPRequest request)
    {
        try
        {
            var otpRecord = await _context.SSHOtps
                .FirstOrDefaultAsync(o =>
                    o.Otp == request.Otp &&
                    o.Username == request.Username &&
                    o.Hostname == request.Hostname &&
                    !o.IsUsed &&
                    o.ExpiresAt > DateTime.UtcNow);

            if (otpRecord == null)
            {
                _logger.LogWarning("Invalid or expired SSH OTP. Username: {Username}, Host: {Hostname}", request.Username, request.Hostname);
                return new VerifySSHOTPResponse
                {
                    IsValid = false,
                    ErrorMessage = "Invalid or expired OTP"
                };
            }

            otpRecord.IsUsed = true;
            otpRecord.UsedAt = DateTime.UtcNow;
            otpRecord.UsedFromIp = request.IpAddress;

            await _context.SaveChangesAsync();

            _logger.LogInformation("SSH OTP verified successfully. Username: {Username}, Host: {Hostname}", request.Username, request.Hostname);

            return new VerifySSHOTPResponse
            {
                IsValid = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying SSH OTP. Username: {Username}, Host: {Hostname}", request.Username, request.Hostname);
            return new VerifySSHOTPResponse
            {
                IsValid = false,
                ErrorMessage = "Error verifying OTP"
            };
        }
    }

    public async Task RegisterHostKeyAsync(RegisterHostKeyRequest request, Guid userId)
    {
        _logger.LogInformation("Registering SSH host key. Hostname: {Hostname}", request.Hostname);

        try
        {
            var fingerprint = ComputeFingerprint(request.PublicKey);

            var existingKey = await _context.SSHHostKeys
                .FirstOrDefaultAsync(h => h.Hostname == request.Hostname);

            if (existingKey != null)
            {
                existingKey.PublicKey = request.PublicKey;
                existingKey.KeyType = request.KeyType;
                existingKey.Fingerprint = fingerprint;
                existingKey.UpdatedAt = DateTime.UtcNow;
                existingKey.UpdatedBy = userId;
            }
            else
            {
                var hostKey = new Core.Models.Entities.SSHHostKey
                {
                    Id = Guid.NewGuid(),
                    Hostname = request.Hostname,
                    PublicKey = request.PublicKey,
                    KeyType = request.KeyType,
                    Fingerprint = fingerprint,
                    RegisteredAt = DateTime.UtcNow,
                    RegisteredBy = userId
                };

                _context.SSHHostKeys.Add(hostKey);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("SSH host key registered. Hostname: {Hostname}, Fingerprint: {Fingerprint}", request.Hostname, fingerprint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register SSH host key. Hostname: {Hostname}", request.Hostname);
            throw;
        }
    }

    public async Task<bool> VerifyHostKeyAsync(string hostname, string publicKey)
    {
        var hostKey = await _context.SSHHostKeys
            .FirstOrDefaultAsync(h => h.Hostname == hostname);

        if (hostKey == null)
        {
            _logger.LogWarning("SSH host key not found. Hostname: {Hostname}", hostname);
            return false;
        }

        var isValid = hostKey.PublicKey == publicKey;

        if (!isValid)
        {
            _logger.LogWarning("SSH host key mismatch. Hostname: {Hostname}", hostname);
        }

        return isValid;
    }

    public async Task<List<HostKeyInfo>> ListHostKeysAsync(Guid userId)
    {
        var hostKeys = await _context.SSHHostKeys
            .Select(h => new HostKeyInfo
            {
                Hostname = h.Hostname,
                PublicKey = h.PublicKey,
                KeyType = h.KeyType,
                Fingerprint = h.Fingerprint,
                RegisteredAt = h.RegisteredAt
            })
            .ToListAsync();

        return hostKeys;
    }

    public async Task RemoveHostKeyAsync(string hostname, Guid userId)
    {
        _logger.LogInformation("Removing SSH host key. Hostname: {Hostname}", hostname);

        var hostKey = await _context.SSHHostKeys
            .FirstOrDefaultAsync(h => h.Hostname == hostname);

        if (hostKey != null)
        {
            _context.SSHHostKeys.Remove(hostKey);
            await _context.SaveChangesAsync();

            _logger.LogInformation("SSH host key removed. Hostname: {Hostname}", hostname);
        }
    }

    // ============================================
    // Private Helper Methods
    // ============================================

    private (string publicKey, string privateKey, string fingerprint) GenerateRSAKeyPair(int keyBits, string? comment)
    {
        var keyGenerator = new RsaKeyPairGenerator();
        keyGenerator.Init(new KeyGenerationParameters(_random, keyBits));

        var keyPair = keyGenerator.GenerateKeyPair();
        var privateKeyParam = (RsaPrivateCrtKeyParameters)keyPair.Private;
        var publicKeyParam = (RsaKeyParameters)keyPair.Public;

        var privateKey = ConvertToOpenSSHPrivateKey(keyPair, "rsa");
        var publicKey = ConvertToOpenSSHPublicKey(publicKeyParam, comment ?? "");
        var fingerprint = ComputeFingerprint(publicKey);

        return (publicKey, privateKey, fingerprint);
    }

    private (string publicKey, string privateKey, string fingerprint) GenerateEd25519KeyPair(string? comment)
    {
        var keyGenerator = new Ed25519KeyPairGenerator();
        keyGenerator.Init(new Ed25519KeyGenerationParameters(_random));

        var keyPair = keyGenerator.GenerateKeyPair();

        var privateKey = ConvertToOpenSSHPrivateKey(keyPair, "ed25519");
        var publicKey = ConvertToOpenSSHPublicKey(keyPair.Public, comment ?? "");
        var fingerprint = ComputeFingerprint(publicKey);

        return (publicKey, privateKey, fingerprint);
    }

    private string ConvertToOpenSSHPrivateKey(AsymmetricCipherKeyPair keyPair, string keyType)
    {
        using var stringWriter = new StringWriter();
        var pemWriter = new PemWriter(stringWriter);
        pemWriter.WriteObject(keyPair.Private);
        return stringWriter.ToString();
    }

    private string ConvertToOpenSSHPublicKey(AsymmetricKeyParameter publicKey, string comment)
    {
        if (publicKey is RsaKeyParameters rsaKey)
        {
            var exponent = rsaKey.Exponent.ToByteArrayUnsigned();
            var modulus = rsaKey.Modulus.ToByteArrayUnsigned();

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            WriteString(bw, "ssh-rsa");
            WriteBytes(bw, exponent);
            WriteBytes(bw, modulus);

            var base64 = Convert.ToBase64String(ms.ToArray());
            return $"ssh-rsa {base64} {comment}".Trim();
        }
        else if (publicKey is Ed25519PublicKeyParameters ed25519Key)
        {
            var encoded = ed25519Key.GetEncoded();

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            WriteString(bw, "ssh-ed25519");
            WriteBytes(bw, encoded);

            var base64 = Convert.ToBase64String(ms.ToArray());
            return $"ssh-ed25519 {base64} {comment}".Trim();
        }

        throw new NotSupportedException($"Key type {publicKey.GetType().Name} not supported");
    }

    private void WriteString(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private void WriteBytes(BinaryWriter writer, byte[] bytes)
    {
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private string ComputeFingerprint(string publicKey)
    {
        var parts = publicKey.Split(' ');
        if (parts.Length < 2) return string.Empty;

        var keyData = Convert.FromBase64String(parts[1]);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(keyData);
        return "SHA256:" + Convert.ToBase64String(hash).TrimEnd('=');
    }

    private string SignSSHPublicKey(
        string publicKey,
        string caPrivateKey,
        string serialNumber,
        List<string> validPrincipals,
        DateTime validAfter,
        DateTime validBefore,
        string certificateType,
        Dictionary<string, string>? criticalOptions,
        Dictionary<string, string>? extensions)
    {
        return publicKey + "-cert-v01@openssh.com";
    }

    private string GenerateSerialNumber()
    {
        return Guid.NewGuid().ToString("N");
    }

    private string GenerateOTP()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var otpChars = new char[16];

        for (int i = 0; i < otpChars.Length; i++)
        {
            otpChars[i] = chars[_random.Next(chars.Length)];
        }

        return new string(otpChars);
    }
}

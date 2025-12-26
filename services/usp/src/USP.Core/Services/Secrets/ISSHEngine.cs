using USP.Core.Models.DTOs.SSH;

namespace USP.Core.Services.Secrets;

/// <summary>
/// SSH Engine - SSH key management and certificate signing
/// Vault-compatible SSH secret engine
/// </summary>
public interface ISSHEngine
{
    // ============================================
    // SSH Key Pair Generation
    // ============================================

    /// <summary>
    /// Generate SSH key pair (RSA or Ed25519)
    /// </summary>
    Task<GenerateSSHKeyPairResponse> GenerateKeyPairAsync(GenerateSSHKeyPairRequest request, Guid userId);

    // ============================================
    // SSH Certificate Signing
    // ============================================

    /// <summary>
    /// Sign SSH public key to create SSH certificate
    /// </summary>
    Task<SignSSHCertificateResponse> SignCertificateAsync(string roleName, SignSSHCertificateRequest request, Guid userId);

    /// <summary>
    /// Create or update SSH role
    /// </summary>
    Task<CreateSSHRoleResponse> CreateRoleAsync(string roleName, CreateSSHRoleRequest request, Guid userId);

    /// <summary>
    /// Read SSH role
    /// </summary>
    Task<ReadSSHRoleResponse?> ReadRoleAsync(string roleName, Guid userId);

    /// <summary>
    /// List SSH roles
    /// </summary>
    Task<ListSSHRolesResponse> ListRolesAsync(Guid userId);

    /// <summary>
    /// Delete SSH role
    /// </summary>
    Task DeleteRoleAsync(string roleName, Guid userId);

    // ============================================
    // SSH CA Management
    // ============================================

    /// <summary>
    /// Generate SSH Certificate Authority key pair
    /// </summary>
    Task<GenerateSSHCAResponse> GenerateCAAsync(GenerateSSHCARequest request, Guid userId);

    /// <summary>
    /// Read SSH CA public key
    /// </summary>
    Task<ReadSSHCAResponse?> ReadCAAsync(Guid userId);

    // ============================================
    // SSH OTP (One-Time Password)
    // ============================================

    /// <summary>
    /// Issue SSH One-Time Password for temporary access
    /// </summary>
    Task<IssueSSHOTPResponse> IssueOTPAsync(IssueSSHOTPRequest request, Guid userId);

    /// <summary>
    /// Verify SSH OTP
    /// </summary>
    Task<VerifySSHOTPResponse> VerifyOTPAsync(VerifySSHOTPRequest request);

    // ============================================
    // Host Key Verification
    // ============================================

    /// <summary>
    /// Register trusted SSH host key
    /// </summary>
    Task RegisterHostKeyAsync(RegisterHostKeyRequest request, Guid userId);

    /// <summary>
    /// Verify SSH host key
    /// </summary>
    Task<bool> VerifyHostKeyAsync(string hostname, string publicKey);

    /// <summary>
    /// List registered host keys
    /// </summary>
    Task<List<HostKeyInfo>> ListHostKeysAsync(Guid userId);

    /// <summary>
    /// Remove host key
    /// </summary>
    Task RemoveHostKeyAsync(string hostname, Guid userId);
}

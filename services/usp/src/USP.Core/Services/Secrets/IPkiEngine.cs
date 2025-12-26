using USP.Core.Models.DTOs.Pki;

namespace USP.Core.Services.Secrets;

/// <summary>
/// PKI Engine for Certificate Authority management and X.509 certificate lifecycle operations
/// Provides HashiCorp Vault-compatible PKI secrets engine functionality
/// </summary>
public interface IPkiEngine
{
    // ====================
    // CA Management (6 methods)
    // ====================

    /// <summary>
    /// Creates a new root Certificate Authority
    /// </summary>
    /// <param name="request">Root CA creation parameters</param>
    /// <param name="userId">User creating the CA</param>
    /// <returns>Created CA information</returns>
    Task<CertificateAuthorityResponse> CreateRootCaAsync(CreateRootCaRequest request, Guid userId);

    /// <summary>
    /// Creates a new intermediate Certificate Authority signed by a parent CA
    /// </summary>
    /// <param name="request">Intermediate CA creation parameters</param>
    /// <param name="userId">User creating the CA</param>
    /// <returns>Created CA information</returns>
    Task<CertificateAuthorityResponse> CreateIntermediateCaAsync(CreateIntermediateCaRequest request, Guid userId);

    /// <summary>
    /// Reads Certificate Authority information by name
    /// </summary>
    /// <param name="caName">CA name</param>
    /// <param name="userId">Requesting user</param>
    /// <returns>CA information</returns>
    Task<CertificateAuthorityResponse> ReadCaAsync(string caName, Guid userId);

    /// <summary>
    /// Lists all Certificate Authority names
    /// </summary>
    /// <param name="userId">Requesting user</param>
    /// <returns>List of CA names</returns>
    Task<List<string>> ListCasAsync(Guid userId);

    /// <summary>
    /// Deletes a Certificate Authority and all its issued certificates
    /// </summary>
    /// <param name="caName">CA name to delete</param>
    /// <param name="userId">User deleting the CA</param>
    Task DeleteCaAsync(string caName, Guid userId);

    /// <summary>
    /// Revokes a Certificate Authority
    /// </summary>
    /// <param name="caName">CA name to revoke</param>
    /// <param name="userId">User revoking the CA</param>
    Task RevokeCaAsync(string caName, Guid userId);

    // ====================
    // Role Management (4 methods)
    // ====================

    /// <summary>
    /// Creates a new certificate role/template
    /// </summary>
    /// <param name="request">Role creation parameters</param>
    /// <param name="userId">User creating the role</param>
    /// <returns>Created role information</returns>
    Task<RoleResponse> CreateRoleAsync(CreateRoleRequest request, Guid userId);

    /// <summary>
    /// Reads certificate role information by name
    /// </summary>
    /// <param name="roleName">Role name</param>
    /// <param name="userId">Requesting user</param>
    /// <returns>Role information</returns>
    Task<RoleResponse> ReadRoleAsync(string roleName, Guid userId);

    /// <summary>
    /// Lists all certificate role names
    /// </summary>
    /// <param name="userId">Requesting user</param>
    /// <returns>List of role names</returns>
    Task<List<string>> ListRolesAsync(Guid userId);

    /// <summary>
    /// Deletes a certificate role
    /// </summary>
    /// <param name="roleName">Role name to delete</param>
    /// <param name="userId">User deleting the role</param>
    Task DeleteRoleAsync(string roleName, Guid userId);

    // ====================
    // Certificate Issuance (2 methods)
    // ====================

    /// <summary>
    /// Issues a new certificate using a role
    /// Generates both certificate and private key
    /// </summary>
    /// <param name="roleName">Role to use for certificate issuance</param>
    /// <param name="request">Certificate issuance parameters</param>
    /// <param name="userId">User requesting certificate</param>
    /// <returns>Issued certificate with private key</returns>
    Task<IssueCertificateResponse> IssueCertificateAsync(string roleName, IssueCertificateRequest request, Guid userId);

    /// <summary>
    /// Signs a Certificate Signing Request using a role
    /// User provides the CSR with their own private key
    /// </summary>
    /// <param name="roleName">Role to use for signing</param>
    /// <param name="request">CSR signing parameters</param>
    /// <param name="userId">User requesting signing</param>
    /// <returns>Signed certificate</returns>
    Task<IssueCertificateResponse> SignCsrAsync(string roleName, SignCsrRequest request, Guid userId);

    // ====================
    // Certificate Operations (3 methods)
    // ====================

    /// <summary>
    /// Revokes a certificate by serial number
    /// </summary>
    /// <param name="request">Revocation parameters</param>
    /// <param name="userId">User revoking the certificate</param>
    /// <returns>Revocation status</returns>
    Task<RevokeCertificateResponse> RevokeCertificateAsync(RevokeCertificateRequest request, Guid userId);

    /// <summary>
    /// Lists certificates issued by a CA or all certificates
    /// </summary>
    /// <param name="caName">Optional CA name filter</param>
    /// <param name="userId">Requesting user</param>
    /// <returns>List of certificates</returns>
    Task<ListCertificatesResponse> ListCertificatesAsync(string? caName, Guid userId);

    /// <summary>
    /// Reads certificate information by serial number
    /// </summary>
    /// <param name="serialNumber">Certificate serial number</param>
    /// <param name="userId">Requesting user</param>
    /// <returns>Certificate information</returns>
    Task<CertificateInfo> ReadCertificateAsync(string serialNumber, Guid userId);

    // ====================
    // CRL Management (1 method)
    // ====================

    /// <summary>
    /// Generates a Certificate Revocation List for a CA
    /// </summary>
    /// <param name="caName">CA name</param>
    /// <param name="userId">Requesting user</param>
    /// <returns>CRL in PEM format</returns>
    Task<GetCrlResponse> GenerateCrlAsync(string caName, Guid userId);
}

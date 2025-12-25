using Grpc.Core;
using USP.Core.Services.Cryptography;
using USP.Core.Services.Secrets;
using USP.Core.Models.DTOs.Secrets;
using USP.Grpc.Secrets;

namespace USP.Api.Grpc;

/// <summary>
/// gRPC Secrets Service implementation
/// </summary>
public class GrpcSecretsService : SecretsService.SecretsServiceBase
{
    private readonly IKvEngine _kvEngine;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<GrpcSecretsService> _logger;

    public GrpcSecretsService(
        IKvEngine kvEngine,
        IEncryptionService encryptionService,
        ILogger<GrpcSecretsService> logger)
    {
        _kvEngine = kvEngine;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public override async Task<USP.Grpc.Secrets.ReadSecretResponse> ReadSecret(
        USP.Grpc.Secrets.ReadSecretRequest request,
        ServerCallContext context)
    {
        try
        {
            var userId = GetUserIdFromContext(context);

            var serviceResponse = await _kvEngine.ReadSecretAsync(
                request.Path,
                request.Version > 0 ? (int?)request.Version : null,
                userId);

            if (serviceResponse == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Secret not found"));
            }

            var grpcResponse = new USP.Grpc.Secrets.ReadSecretResponse
            {
                Metadata = new USP.Grpc.Secrets.SecretMetadata
                {
                    CurrentVersion = serviceResponse.Metadata.Version,
                    CreatedTime = serviceResponse.Metadata.CreatedTime.ToString("O"),
                    UpdatedTime = serviceResponse.Metadata.CreatedTime.ToString("O")
                }
            };

            foreach (var kvp in serviceResponse.Data.Data)
            {
                grpcResponse.Data[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
            }

            _logger.LogInformation("Secret read via gRPC: {Path}, version {Version}",
                request.Path, serviceResponse.Metadata.Version);

            return grpcResponse;
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading secret at {Path}", request.Path);
            throw new RpcException(new Status(StatusCode.Internal, "Failed to read secret"));
        }
    }

    public override async Task<WriteSecretResponse> WriteSecret(
        WriteSecretRequest request,
        ServerCallContext context)
    {
        try
        {
            var userId = GetUserIdFromContext(context);

            var createRequest = new CreateSecretRequest
            {
                Data = request.Data.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value)
            };

            var serviceResponse = await _kvEngine.CreateSecretAsync(request.Path, createRequest, userId);

            var grpcResponse = new WriteSecretResponse
            {
                Success = true,
                Version = serviceResponse.Data.Version,
                CreatedTime = serviceResponse.Data.CreatedTime.ToString("O")
            };

            _logger.LogInformation("Secret written via gRPC: {Path}, version {Version}",
                request.Path, serviceResponse.Data.Version);

            return grpcResponse;
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing secret at {Path}", request.Path);
            throw new RpcException(new Status(StatusCode.Internal, "Failed to write secret"));
        }
    }

    public override async Task<DeleteSecretResponse> DeleteSecret(
        DeleteSecretRequest request,
        ServerCallContext context)
    {
        try
        {
            var userId = GetUserIdFromContext(context);

            var deleteRequest = new DeleteSecretVersionsRequest
            {
                Versions = request.Versions.ToList()
            };

            await _kvEngine.DeleteSecretVersionsAsync(request.Path, deleteRequest, userId);

            var response = new DeleteSecretResponse
            {
                Success = true,
                Message = $"Deleted versions: {string.Join(", ", request.Versions)}"
            };

            _logger.LogInformation("Secret deleted via gRPC: {Path}, versions {Versions}",
                request.Path, string.Join(", ", request.Versions));

            return response;
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting secret at {Path}", request.Path);
            throw new RpcException(new Status(StatusCode.Internal, "Failed to delete secret"));
        }
    }

    public override async Task<USP.Grpc.Secrets.ListSecretsResponse> ListSecrets(
        USP.Grpc.Secrets.ListSecretsRequest request,
        ServerCallContext context)
    {
        try
        {
            var userId = GetUserIdFromContext(context);

            var serviceResponse = await _kvEngine.ListSecretsAsync(request.Path, userId);

            var grpcResponse = new USP.Grpc.Secrets.ListSecretsResponse();
            grpcResponse.Keys.AddRange(serviceResponse.Data.Keys);

            _logger.LogInformation("Secrets listed via gRPC: {Path}, found {Count} secrets",
                request.Path, serviceResponse.Data.Keys.Count);

            return grpcResponse;
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing secrets at {Path}", request.Path);
            throw new RpcException(new Status(StatusCode.Internal, "Failed to list secrets"));
        }
    }

    public override Task<EncryptResponse> Encrypt(
        EncryptRequest request,
        ServerCallContext context)
    {
        try
        {
            var plaintextString = System.Text.Encoding.UTF8.GetString(request.Plaintext.ToByteArray());
            var encryptedData = _encryptionService.Encrypt(plaintextString);

            var response = new EncryptResponse
            {
                Ciphertext = encryptedData,
                KeyVersion = "1"
            };

            _logger.LogInformation("Data encrypted via gRPC with key {KeyName}", request.KeyName);

            return Task.FromResult(response);
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error encrypting with key {KeyName}", request.KeyName);
            throw new RpcException(new Status(StatusCode.Internal, "Failed to encrypt"));
        }
    }

    public override Task<DecryptResponse> Decrypt(
        DecryptRequest request,
        ServerCallContext context)
    {
        try
        {
            var decryptedString = _encryptionService.Decrypt(request.Ciphertext);
            var decryptedBytes = System.Text.Encoding.UTF8.GetBytes(decryptedString);

            var response = new DecryptResponse
            {
                Plaintext = Google.Protobuf.ByteString.CopyFrom(decryptedBytes),
                KeyVersion = "1"
            };

            _logger.LogInformation("Data decrypted via gRPC with key {KeyName}", request.KeyName);

            return Task.FromResult(response);
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decrypting with key {KeyName}", request.KeyName);
            throw new RpcException(new Status(StatusCode.Internal, "Failed to decrypt"));
        }
    }

    public override async Task<RotateSecretResponse> RotateSecret(
        RotateSecretRequest request,
        ServerCallContext context)
    {
        try
        {
            _logger.LogWarning("Secret rotation requested but rotation service not yet implemented: {Path}", request.Path);

            throw new RpcException(new Status(StatusCode.Unimplemented, "Secret rotation service will be implemented in Phase 9"));
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rotating secret at {Path}", request.Path);
            throw new RpcException(new Status(StatusCode.Internal, "Failed to rotate secret"));
        }
    }

    public override async Task<GetSecretMetadataResponse> GetSecretMetadata(
        GetSecretMetadataRequest request,
        ServerCallContext context)
    {
        try
        {
            var userId = GetUserIdFromContext(context);

            var serviceMetadata = await _kvEngine.ReadSecretMetadataAsync(request.Path, userId);

            if (serviceMetadata == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Secret metadata not found"));
            }

            var grpcMetadata = new USP.Grpc.Secrets.SecretMetadata
            {
                CurrentVersion = serviceMetadata.CurrentVersion,
                OldestVersion = serviceMetadata.OldestVersion,
                CreatedTime = serviceMetadata.CreatedTime.ToString("O"),
                UpdatedTime = serviceMetadata.UpdatedTime.ToString("O"),
                MaxVersions = serviceMetadata.MaxVersions,
                DeleteVersionAfter = serviceMetadata.DeleteVersionAfter != null
            };

            foreach (var version in serviceMetadata.Versions)
            {
                grpcMetadata.Versions.Add(version.Key.ToString(), new USP.Grpc.Secrets.VersionMetadata
                {
                    CreatedTime = version.Value.CreatedTime.ToString("O"),
                    DeletionTime = version.Value.DeletionTime?.ToString("O") ?? string.Empty,
                    Destroyed = version.Value.Destroyed
                });
            }

            var response = new GetSecretMetadataResponse
            {
                Metadata = grpcMetadata
            };

            _logger.LogInformation("Secret metadata retrieved via gRPC: {Path}", request.Path);

            return response;
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting secret metadata at {Path}", request.Path);
            throw new RpcException(new Status(StatusCode.Internal, "Failed to get secret metadata"));
        }
    }

    private Guid GetUserIdFromContext(ServerCallContext context)
    {
        var userIdClaim = context.GetHttpContext().User.FindFirst("sub")
            ?? context.GetHttpContext().User.FindFirst("userId");

        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid or missing user ID"));
        }

        return userId;
    }
}

using Amazon;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Logging;
using USP.Core.Services.PAM;

namespace USP.Infrastructure.Services.PAM.Connectors;

/// <summary>
/// AWS IAM access key rotation connector
/// </summary>
public class AwsConnector : BaseConnector
{
    private readonly ILogger<AwsConnector> _logger;

    public override string Platform => "AWS";

    public AwsConnector(ILogger<AwsConnector> logger)
    {
        _logger = logger;
    }

    public override async Task<PasswordRotationResult> RotatePasswordAsync(
        string hostAddress,
        int? port,
        string username,
        string currentPassword,
        string newPassword,
        string? databaseName = null,
        string? connectionDetails = null)
    {
        var result = new PasswordRotationResult
        {
            Success = false,
            RotatedAt = DateTime.UtcNow
        };

        try
        {
            // Parse connection details to get AWS region and current access key ID
            var region = ParseRegionFromConnectionDetails(connectionDetails) ?? RegionEndpoint.USEast1;
            var currentAccessKeyId = ParseAccessKeyIdFromConnectionDetails(connectionDetails);

            if (string.IsNullOrEmpty(currentAccessKeyId))
            {
                result.ErrorMessage = "Current access key ID not found in connection details";
                return result;
            }

            // Create IAM client with current credentials
            var credentials = new BasicAWSCredentials(currentAccessKeyId, currentPassword);
            using var iamClient = new AmazonIdentityManagementServiceClient(credentials, region);

            // Step 1: Create new access key
            var createKeyRequest = new CreateAccessKeyRequest
            {
                UserName = username
            };

            var createKeyResponse = await iamClient.CreateAccessKeyAsync(createKeyRequest);
            var newAccessKey = createKeyResponse.AccessKey;

            if (newAccessKey == null)
            {
                result.ErrorMessage = "Failed to create new access key";
                return result;
            }

            _logger.LogInformation(
                "Created new access key {AccessKeyId} for AWS user {Username}",
                newAccessKey.AccessKeyId,
                username);

            try
            {
                // Step 2: Verify new credentials work
                var newCredentials = new BasicAWSCredentials(newAccessKey.AccessKeyId, newAccessKey.SecretAccessKey);
                using var verifyClient = new AmazonIdentityManagementServiceClient(newCredentials, region);

                var getUserRequest = new GetUserRequest { UserName = username };
                await verifyClient.GetUserAsync(getUserRequest);

                _logger.LogInformation(
                    "Verified new access key {AccessKeyId} works for AWS user {Username}",
                    newAccessKey.AccessKeyId,
                    username);

                // Step 3: Deactivate old access key (keep it for rollback if needed)
                var updateOldKeyRequest = new UpdateAccessKeyRequest
                {
                    AccessKeyId = currentAccessKeyId,
                    Status = StatusType.Inactive,
                    UserName = username
                };

                await iamClient.UpdateAccessKeyAsync(updateOldKeyRequest);

                _logger.LogInformation(
                    "Deactivated old access key {AccessKeyId} for AWS user {Username}",
                    currentAccessKeyId,
                    username);

                // Step 4: Optionally delete old access key after grace period
                // For now, we keep it inactive for rollback capability
                // In production, you might want to delete it after verification

                result.Success = true;
                result.Details = $"AWS access key rotated successfully for user {username}. New Access Key ID: {newAccessKey.AccessKeyId}, Secret: {newAccessKey.SecretAccessKey}";

                _logger.LogInformation(
                    "AWS access key rotated successfully for user {Username}",
                    username);
            }
            catch (Exception verifyEx)
            {
                // Rollback: Delete the new key if verification fails
                _logger.LogError(verifyEx,
                    "Failed to verify new access key, rolling back for AWS user {Username}",
                    username);

                try
                {
                    var deleteKeyRequest = new DeleteAccessKeyRequest
                    {
                        AccessKeyId = newAccessKey.AccessKeyId,
                        UserName = username
                    };

                    await iamClient.DeleteAccessKeyAsync(deleteKeyRequest);

                    _logger.LogInformation(
                        "Rolled back new access key {AccessKeyId} for AWS user {Username}",
                        newAccessKey.AccessKeyId,
                        username);
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx,
                        "Failed to rollback new access key for AWS user {Username}",
                        username);
                }

                throw;
            }
        }
        catch (AmazonIdentityManagementServiceException ex) when (ex.ErrorCode == "InvalidClientTokenId")
        {
            result.ErrorMessage = "Invalid AWS credentials";
            result.Details = $"Authentication failed: {ex.Message}";

            _logger.LogError(ex,
                "Authentication failed for AWS user {Username}",
                username);
        }
        catch (AmazonIdentityManagementServiceException ex) when (ex.ErrorCode == "LimitExceeded")
        {
            result.ErrorMessage = "Access key limit exceeded";
            result.Details = $"Cannot create more access keys: {ex.Message}";

            _logger.LogError(ex,
                "Access key limit exceeded for AWS user {Username}",
                username);
        }
        catch (AmazonIdentityManagementServiceException ex)
        {
            result.ErrorMessage = "AWS IAM error occurred";
            result.Details = $"AWS Error {ex.ErrorCode}: {ex.Message}";

            _logger.LogError(ex,
                "AWS IAM error rotating access key for user {Username}",
                username);
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            result.Details = $"Failed to rotate AWS access key: {ex.Message}";

            _logger.LogError(ex,
                "Unexpected error rotating AWS access key for user {Username}",
                username);
        }

        return result;
    }

    public override async Task<bool> VerifyCredentialsAsync(
        string hostAddress,
        int? port,
        string username,
        string password,
        string? databaseName = null,
        string? connectionDetails = null)
    {
        try
        {
            var region = ParseRegionFromConnectionDetails(connectionDetails) ?? RegionEndpoint.USEast1;
            var accessKeyId = ParseAccessKeyIdFromConnectionDetails(connectionDetails);

            if (string.IsNullOrEmpty(accessKeyId))
            {
                _logger.LogWarning("Access key ID not found in connection details for AWS user {Username}", username);
                return false;
            }

            var credentials = new BasicAWSCredentials(accessKeyId, password);
            using var iamClient = new AmazonIdentityManagementServiceClient(credentials, region);

            // Try to get user information to verify credentials
            var getUserRequest = new GetUserRequest { UserName = username };
            await iamClient.GetUserAsync(getUserRequest);

            _logger.LogDebug(
                "Credentials verified successfully for AWS user {Username}",
                username);

            return true;
        }
        catch (AmazonIdentityManagementServiceException ex)
        {
            _logger.LogWarning(ex,
                "Failed to verify credentials for AWS user {Username}",
                username);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Error verifying credentials for AWS user {Username}",
                username);

            return false;
        }
    }

    /// <summary>
    /// List access keys for a user
    /// </summary>
    public async Task<List<AccessKeyInfo>> ListAccessKeysAsync(
        string username,
        string accessKeyId,
        string secretAccessKey,
        string? region = null)
    {
        var accessKeys = new List<AccessKeyInfo>();

        try
        {
            var awsRegion = region != null ? RegionEndpoint.GetBySystemName(region) : RegionEndpoint.USEast1;
            var credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);
            using var iamClient = new AmazonIdentityManagementServiceClient(credentials, awsRegion);

            var request = new ListAccessKeysRequest { UserName = username };
            var response = await iamClient.ListAccessKeysAsync(request);

            foreach (var key in response.AccessKeyMetadata)
            {
                accessKeys.Add(new AccessKeyInfo
                {
                    AccessKeyId = key.AccessKeyId,
                    UserName = key.UserName,
                    Status = key.Status.Value,
                    CreateDate = key.CreateDate
                });
            }

            _logger.LogInformation(
                "Listed {Count} access keys for AWS user {Username}",
                accessKeys.Count,
                username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to list access keys for AWS user {Username}",
                username);
        }

        return accessKeys;
    }

    /// <summary>
    /// Delete an access key
    /// </summary>
    public async Task<bool> DeleteAccessKeyAsync(
        string username,
        string accessKeyIdToDelete,
        string currentAccessKeyId,
        string currentSecretKey,
        string? region = null)
    {
        try
        {
            var awsRegion = region != null ? RegionEndpoint.GetBySystemName(region) : RegionEndpoint.USEast1;
            var credentials = new BasicAWSCredentials(currentAccessKeyId, currentSecretKey);
            using var iamClient = new AmazonIdentityManagementServiceClient(credentials, awsRegion);

            var request = new DeleteAccessKeyRequest
            {
                AccessKeyId = accessKeyIdToDelete,
                UserName = username
            };

            await iamClient.DeleteAccessKeyAsync(request);

            _logger.LogInformation(
                "Deleted access key {AccessKeyId} for AWS user {Username}",
                accessKeyIdToDelete,
                username);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to delete access key {AccessKeyId} for AWS user {Username}",
                accessKeyIdToDelete,
                username);

            return false;
        }
    }

    /// <summary>
    /// Get user details and attached policies
    /// </summary>
    public async Task<AwsUserDetails?> GetUserDetailsAsync(
        string username,
        string accessKeyId,
        string secretAccessKey,
        string? region = null)
    {
        try
        {
            var awsRegion = region != null ? RegionEndpoint.GetBySystemName(region) : RegionEndpoint.USEast1;
            var credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);
            using var iamClient = new AmazonIdentityManagementServiceClient(credentials, awsRegion);

            // Get user info
            var getUserResponse = await iamClient.GetUserAsync(new GetUserRequest { UserName = username });

            // Get attached policies
            var listPoliciesResponse = await iamClient.ListAttachedUserPoliciesAsync(
                new ListAttachedUserPoliciesRequest { UserName = username });

            // Get groups
            var listGroupsResponse = await iamClient.ListGroupsForUserAsync(
                new ListGroupsForUserRequest { UserName = username });

            var userDetails = new AwsUserDetails
            {
                UserName = getUserResponse.User.UserName,
                UserId = getUserResponse.User.UserId,
                Arn = getUserResponse.User.Arn,
                CreateDate = getUserResponse.User.CreateDate,
                AttachedPolicies = listPoliciesResponse.AttachedPolicies.Select(p => p.PolicyName).ToList(),
                Groups = listGroupsResponse.Groups.Select(g => g.GroupName).ToList()
            };

            _logger.LogDebug(
                "Retrieved details for AWS user {Username}",
                username);

            return userDetails;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to get details for AWS user {Username}",
                username);

            return null;
        }
    }

    private static RegionEndpoint? ParseRegionFromConnectionDetails(string? connectionDetails)
    {
        if (string.IsNullOrEmpty(connectionDetails))
            return null;

        try
        {
            var details = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(connectionDetails);
            if (details != null && details.TryGetValue("region", out var region))
            {
                return RegionEndpoint.GetBySystemName(region);
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return null;
    }

    private static string? ParseAccessKeyIdFromConnectionDetails(string? connectionDetails)
    {
        if (string.IsNullOrEmpty(connectionDetails))
            return null;

        try
        {
            var details = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(connectionDetails);
            if (details != null && details.TryGetValue("accessKeyId", out var accessKeyId))
            {
                return accessKeyId;
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return null;
    }
}

public class AccessKeyInfo
{
    public string AccessKeyId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreateDate { get; set; }
}

public class AwsUserDetails
{
    public string UserName { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Arn { get; set; } = string.Empty;
    public DateTime CreateDate { get; set; }
    public List<string> AttachedPolicies { get; set; } = new();
    public List<string> Groups { get; set; } = new();
}

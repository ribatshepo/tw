namespace USP.Core.Models.DTOs.CloudSync;

public class CloudSyncConfigurationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string SyncMode { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string? PathFilter { get; set; }
    public string? TagFilter { get; set; }
    public string? SyncSchedule { get; set; }
    public string ConflictResolution { get; set; } = string.Empty;
    public bool EnableRealTimeSync { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public DateTime? NextSyncAt { get; set; }
    public string? LastSyncError { get; set; }
    public int ConsecutiveFailures { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateCloudSyncConfigurationRequest
{
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string SyncMode { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;

    // AWS Configuration
    public string? AwsRegion { get; set; }
    public string? AwsAccessKeyId { get; set; }
    public string? AwsSecretAccessKey { get; set; }
    public string? AwsIamRoleArn { get; set; }

    // Azure Configuration
    public string? AzureKeyVaultUri { get; set; }
    public string? AzureTenantId { get; set; }
    public string? AzureClientId { get; set; }
    public string? AzureClientSecret { get; set; }

    // GCP Configuration
    public string? GcpProjectId { get; set; }
    public string? GcpServiceAccountJson { get; set; }

    // Sync Configuration
    public string? PathFilter { get; set; }
    public string? TagFilter { get; set; }
    public string? SyncSchedule { get; set; }
    public string ConflictResolution { get; set; } = "LastWriteWins";
    public bool EnableRealTimeSync { get; set; }
}

public class UpdateCloudSyncConfigurationRequest
{
    public string? Name { get; set; }
    public bool? IsEnabled { get; set; }
    public string? PathFilter { get; set; }
    public string? TagFilter { get; set; }
    public string? SyncSchedule { get; set; }
    public string? ConflictResolution { get; set; }
    public bool? EnableRealTimeSync { get; set; }
}

public class TriggerSyncRequest
{
    public string? Direction { get; set; }
    public bool Force { get; set; }
}

public class CloudSyncHistoryDto
{
    public Guid Id { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long DurationMs { get; set; }
    public int SecretsPushed { get; set; }
    public int SecretsPulled { get; set; }
    public int SecretsUpdated { get; set; }
    public int SecretsDeleted { get; set; }
    public int ConflictsDetected { get; set; }
    public int ConflictsResolved { get; set; }
    public int ErrorsCount { get; set; }
    public string? ErrorMessage { get; set; }
}

public class CloudSyncConflictDto
{
    public Guid Id { get; set; }
    public string SecretPath { get; set; } = string.Empty;
    public string ConflictType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int UspVersion { get; set; }
    public DateTime UspLastModified { get; set; }
    public string? CloudVersion { get; set; }
    public DateTime CloudLastModified { get; set; }
    public string? ResolutionStrategy { get; set; }
    public string? ResolvedValue { get; set; }
    public DateTime DetectedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public class ResolveConflictRequest
{
    public string Resolution { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class SyncResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? ExternalId { get; set; }
    public DateTime Timestamp { get; set; }

    public static SyncResult SuccessResult(string? externalId = null, string? message = null) =>
        new()
        {
            Success = true,
            ExternalId = externalId,
            Message = message,
            Timestamp = DateTime.UtcNow
        };

    public static SyncResult FailureResult(string message) =>
        new()
        {
            Success = false,
            Message = message,
            Timestamp = DateTime.UtcNow
        };
}

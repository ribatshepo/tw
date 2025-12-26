namespace USP.Core.Domain.Enums;

/// <summary>
/// Represents the status of a credential rotation job
/// </summary>
public enum RotationStatus
{
    /// <summary>
    /// Rotation job is pending execution
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Rotation is currently in progress
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// Rotation completed successfully
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Rotation failed with errors
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Rotation was rolled back due to failure
    /// </summary>
    RolledBack = 4,

    /// <summary>
    /// Rotation was cancelled by user
    /// </summary>
    Cancelled = 5
}

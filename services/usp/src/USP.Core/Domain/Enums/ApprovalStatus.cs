namespace USP.Core.Domain.Enums;

/// <summary>
/// Represents the status of an approval request for dual control
/// </summary>
public enum ApprovalStatus
{
    /// <summary>
    /// Approval request is pending review
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Approval request has been approved
    /// </summary>
    Approved = 1,

    /// <summary>
    /// Approval request has been denied
    /// </summary>
    Denied = 2,

    /// <summary>
    /// Approval request has expired
    /// </summary>
    Expired = 3,

    /// <summary>
    /// Approval request was cancelled
    /// </summary>
    Cancelled = 4
}

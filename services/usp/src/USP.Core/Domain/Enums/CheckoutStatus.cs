namespace USP.Core.Domain.Enums;

/// <summary>
/// Represents the status of a privileged account checkout request
/// </summary>
public enum CheckoutStatus
{
    /// <summary>
    /// Checkout request has been submitted and is pending approval
    /// </summary>
    Requested = 0,

    /// <summary>
    /// Checkout request has been approved (awaiting activation)
    /// </summary>
    Approved = 1,

    /// <summary>
    /// Checkout is active (account is currently checked out)
    /// </summary>
    Active = 2,

    /// <summary>
    /// Account has been checked in (checkout completed)
    /// </summary>
    CheckedIn = 3,

    /// <summary>
    /// Checkout has expired (time limit exceeded)
    /// </summary>
    Expired = 4,

    /// <summary>
    /// Checkout request was denied
    /// </summary>
    Denied = 5,

    /// <summary>
    /// Checkout was cancelled by user
    /// </summary>
    Cancelled = 6
}

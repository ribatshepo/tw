namespace USP.Core.Exceptions;

/// <summary>
/// Base exception for all USP-specific exceptions
/// </summary>
public class USPException : Exception
{
    /// <summary>
    /// Error code for programmatic handling
    /// </summary>
    public string ErrorCode { get; set; }

    /// <summary>
    /// Technical details for logging (not exposed to user)
    /// </summary>
    public string? TechnicalDetails { get; set; }

    public USPException(string errorCode, string message, string? technicalDetails = null)
        : base(message)
    {
        ErrorCode = errorCode;
        TechnicalDetails = technicalDetails;
    }

    public USPException(string errorCode, string message, Exception innerException, string? technicalDetails = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        TechnicalDetails = technicalDetails;
    }
}

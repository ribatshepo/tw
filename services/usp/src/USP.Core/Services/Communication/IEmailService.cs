namespace USP.Core.Services.Communication;

/// <summary>
/// Email service for sending transactional emails
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Send a magic link login email to the user
    /// </summary>
    Task<bool> SendMagicLinkAsync(string toEmail, string magicLinkUrl, string userName);

    /// <summary>
    /// Send an email OTP verification code
    /// </summary>
    Task<bool> SendEmailOtpAsync(string toEmail, string otpCode, string userName);

    /// <summary>
    /// Send a security alert notification
    /// </summary>
    Task<bool> SendSecurityAlertAsync(string toEmail, string alertMessage, string userName);
}

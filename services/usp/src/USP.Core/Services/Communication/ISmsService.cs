namespace USP.Core.Services.Communication;

/// <summary>
/// Service for sending SMS and voice OTP codes
/// </summary>
public interface ISmsService
{
    /// <summary>
    /// Send SMS message
    /// </summary>
    /// <param name="phoneNumber">Phone number in E.164 format (e.g., +1234567890)</param>
    /// <param name="message">Message content</param>
    /// <returns>True if sent successfully</returns>
    Task<bool> SendSmsAsync(string phoneNumber, string message);

    /// <summary>
    /// Send voice call with message
    /// </summary>
    /// <param name="phoneNumber">Phone number in E.164 format (e.g., +1234567890)</param>
    /// <param name="message">Message to speak</param>
    /// <returns>True if initiated successfully</returns>
    Task<bool> SendVoiceCallAsync(string phoneNumber, string message);

    /// <summary>
    /// Send SMS OTP code
    /// </summary>
    /// <param name="phoneNumber">Phone number in E.164 format</param>
    /// <param name="code">OTP code</param>
    /// <param name="expirationMinutes">Code expiration in minutes</param>
    /// <returns>True if sent successfully</returns>
    Task<bool> SendOtpSmsAsync(string phoneNumber, string code, int expirationMinutes = 5);

    /// <summary>
    /// Send voice OTP code
    /// </summary>
    /// <param name="phoneNumber">Phone number in E.164 format</param>
    /// <param name="code">OTP code</param>
    /// <returns>True if initiated successfully</returns>
    Task<bool> SendOtpVoiceAsync(string phoneNumber, string code);

    /// <summary>
    /// Verify phone number by sending test code
    /// </summary>
    /// <param name="phoneNumber">Phone number in E.164 format</param>
    /// <returns>Verification code sent</returns>
    Task<string> SendVerificationCodeAsync(string phoneNumber);
}

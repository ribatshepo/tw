using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using USP.Core.Interfaces.Services;
using USP.Shared.Configuration.Options;

namespace USP.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IOptions<EmailOptions> emailOptions,
        ILogger<EmailService> logger)
    {
        _emailOptions = emailOptions.Value;
        _logger = logger;
    }

    public async Task SendEmailVerificationAsync(
        string email,
        string verificationToken,
        CancellationToken cancellationToken = default)
    {
        var subject = "Verify Your Email - USP Security Platform";
        var body = $@"
<html>
<body>
    <h2>Email Verification</h2>
    <p>Thank you for registering with USP Security Platform.</p>
    <p>Please verify your email address by entering this verification code:</p>
    <h3 style='background-color: #f0f0f0; padding: 10px; font-family: monospace;'>{verificationToken}</h3>
    <p>This code will expire in 24 hours.</p>
    <p>If you did not create an account, please ignore this email.</p>
</body>
</html>";

        await SendEmailAsync(email, subject, body, cancellationToken);
    }

    public async Task SendPasswordResetAsync(
        string email,
        string resetToken,
        CancellationToken cancellationToken = default)
    {
        var subject = "Password Reset Request - USP Security Platform";
        var body = $@"
<html>
<body>
    <h2>Password Reset</h2>
    <p>You have requested to reset your password for: <strong>{email}</strong></p>
    <p>Use this reset code:</p>
    <h3 style='background-color: #f0f0f0; padding: 10px; font-family: monospace;'>{resetToken}</h3>
    <p>This code will expire in 1 hour.</p>
    <p><strong>Important:</strong> You will need to enter both your email address and this code when resetting your password.</p>
    <p>If you did not request a password reset, please ignore this email and ensure your account is secure.</p>
</body>
</html>";

        await SendEmailAsync(email, subject, body, cancellationToken);
    }

    public async Task SendMfaCodeAsync(
        string email,
        string code,
        int expirationMinutes,
        CancellationToken cancellationToken = default)
    {
        var subject = "Your MFA Verification Code - USP";
        var body = $@"
<html>
<body>
    <h2>Multi-Factor Authentication</h2>
    <p>Your verification code is:</p>
    <h3 style='background-color: #f0f0f0; padding: 10px; font-family: monospace; font-size: 24px;'>{code}</h3>
    <p>This code will expire in {expirationMinutes} minutes.</p>
    <p>If you did not request this code, please contact security immediately.</p>
</body>
</html>";

        await SendEmailAsync(email, subject, body, cancellationToken);
    }

    public async Task SendNotificationAsync(
        string email,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        await SendEmailAsync(email, subject, body, cancellationToken);
    }

    private async Task SendEmailAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        using var smtpClient = new SmtpClient(_emailOptions.SmtpHost, _emailOptions.SmtpPort)
        {
            EnableSsl = _emailOptions.EnableSsl,
            Timeout = _emailOptions.TimeoutSeconds * 1000,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };

        if (!string.IsNullOrEmpty(_emailOptions.SmtpUsername))
        {
            smtpClient.Credentials = new NetworkCredential(
                _emailOptions.SmtpUsername,
                _emailOptions.SmtpPassword);
        }

        using var mailMessage = new MailMessage
        {
            From = new MailAddress(_emailOptions.FromEmail, _emailOptions.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };

        mailMessage.To.Add(toEmail);

        var retryCount = 0;
        while (retryCount <= _emailOptions.MaxRetries)
        {
            try
            {
                await smtpClient.SendMailAsync(mailMessage, cancellationToken);
                _logger.LogInformation("Email sent successfully to {Email} with subject '{Subject}'",
                    toEmail, subject);
                return;
            }
            catch (Exception ex) when (retryCount < _emailOptions.MaxRetries)
            {
                retryCount++;
                _logger.LogWarning(ex,
                    "Failed to send email to {Email}, attempt {Attempt}/{MaxAttempts}",
                    toEmail, retryCount, _emailOptions.MaxRetries);

                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to send email to {Email} after {MaxAttempts} attempts",
                    toEmail, _emailOptions.MaxRetries);
                throw;
            }
        }
    }
}

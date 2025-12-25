using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using USP.Core.Models.Configuration;
using USP.Core.Services.Communication;

namespace USP.Infrastructure.Services.Communication;

/// <summary>
/// Production email service using MailKit/SMTP
/// </summary>
public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IOptions<EmailSettings> settings,
        ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<bool> SendMagicLinkAsync(string toEmail, string magicLinkUrl, string userName)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
            message.To.Add(new MailboxAddress(userName, toEmail));
            message.Subject = "Your Magic Link Login";

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif; padding: 20px; max-width: 600px; margin: 0 auto;'>
                        <div style='background-color: #007bff; padding: 20px; text-align: center;'>
                            <h1 style='color: white; margin: 0;'>USP Security Platform</h1>
                        </div>
                        <div style='padding: 30px; background-color: #f9f9f9; border: 1px solid #ddd;'>
                            <h2 style='color: #333;'>Login Request</h2>
                            <p style='color: #666; font-size: 16px;'>Hello <strong>{userName}</strong>,</p>
                            <p style='color: #666; font-size: 16px;'>
                                Click the button below to sign in to your account. This link expires in
                                <strong>{_settings.MagicLinkExpirationMinutes} minutes</strong>:
                            </p>
                            <div style='text-align: center; margin: 30px 0;'>
                                <a href='{magicLinkUrl}'
                                   style='display: inline-block; padding: 15px 30px; background-color: #007bff;
                                          color: white; text-decoration: none; border-radius: 5px; font-size: 16px; font-weight: bold;'>
                                    Sign In Now
                                </a>
                            </div>
                            <p style='color: #999; font-size: 14px;'>
                                Or copy and paste this URL into your browser:<br/>
                                <span style='color: #007bff; word-break: break-all;'>{magicLinkUrl}</span>
                            </p>
                            <hr style='border: none; border-top: 1px solid #ddd; margin: 30px 0;'/>
                            <p style='color: #999; font-size: 13px;'>
                                If you didn't request this login link, please ignore this email and secure your account.
                            </p>
                        </div>
                        <div style='text-align: center; padding: 20px; color: #999; font-size: 12px;'>
                            <p>USP Security Platform &copy; 2025 GBMM</p>
                        </div>
                    </body>
                    </html>
                ",
                TextBody = $@"
USP Security Platform - Login Request

Hello {userName},

Click the link below to sign in to your account. This link expires in {_settings.MagicLinkExpirationMinutes} minutes:

{magicLinkUrl}

If you didn't request this login link, please ignore this email and secure your account.

---
USP Security Platform
GBMM © 2025
                "
            };
            message.Body = bodyBuilder.ToMessageBody();

            await SendEmailAsync(message);

            _logger.LogInformation("Magic link email sent to {Email}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send magic link email to {Email}", toEmail);
            return false;
        }
    }

    public async Task<bool> SendEmailOtpAsync(string toEmail, string otpCode, string userName)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
            message.To.Add(new MailboxAddress(userName, toEmail));
            message.Subject = "Your Email Verification Code";

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif; padding: 20px; max-width: 600px; margin: 0 auto;'>
                        <div style='background-color: #28a745; padding: 20px; text-align: center;'>
                            <h1 style='color: white; margin: 0;'>USP Security Platform</h1>
                        </div>
                        <div style='padding: 30px; background-color: #f9f9f9; border: 1px solid #ddd;'>
                            <h2 style='color: #333;'>Email Verification</h2>
                            <p style='color: #666; font-size: 16px;'>Hello <strong>{userName}</strong>,</p>
                            <p style='color: #666; font-size: 16px;'>
                                Use the verification code below to complete your login:
                            </p>
                            <div style='text-align: center; margin: 30px 0; padding: 20px; background-color: #fff; border: 2px dashed #28a745;'>
                                <span style='font-size: 32px; font-weight: bold; letter-spacing: 10px; color: #28a745;'>
                                    {otpCode}
                                </span>
                            </div>
                            <p style='color: #999; font-size: 14px; text-align: center;'>
                                This code expires in <strong>10 minutes</strong>
                            </p>
                            <hr style='border: none; border-top: 1px solid #ddd; margin: 30px 0;'/>
                            <p style='color: #999; font-size: 13px;'>
                                If you didn't request this code, please ignore this email and secure your account.
                            </p>
                        </div>
                        <div style='text-align: center; padding: 20px; color: #999; font-size: 12px;'>
                            <p>USP Security Platform &copy; 2025 GBMM</p>
                        </div>
                    </body>
                    </html>
                ",
                TextBody = $@"
USP Security Platform - Email Verification

Hello {userName},

Use the verification code below to complete your login:

{otpCode}

This code expires in 10 minutes.

If you didn't request this code, please ignore this email and secure your account.

---
USP Security Platform
GBMM © 2025
                "
            };
            message.Body = bodyBuilder.ToMessageBody();

            await SendEmailAsync(message);

            _logger.LogInformation("OTP email sent to {Email}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send OTP email to {Email}", toEmail);
            return false;
        }
    }

    public async Task<bool> SendSecurityAlertAsync(string toEmail, string alertMessage, string userName)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
            message.To.Add(new MailboxAddress(userName, toEmail));
            message.Subject = "Security Alert - Action Required";

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif; padding: 20px; max-width: 600px; margin: 0 auto;'>
                        <div style='background-color: #dc3545; padding: 20px; text-align: center;'>
                            <h1 style='color: white; margin: 0;'>⚠️ Security Alert</h1>
                        </div>
                        <div style='padding: 30px; background-color: #fff3cd; border: 2px solid #dc3545;'>
                            <h2 style='color: #721c24;'>Attention Required</h2>
                            <p style='color: #856404; font-size: 16px;'>Hello <strong>{userName}</strong>,</p>
                            <p style='color: #856404; font-size: 16px;'>
                                We detected suspicious activity on your account:
                            </p>
                            <div style='padding: 20px; background-color: #fff; border-left: 4px solid #dc3545; margin: 20px 0;'>
                                <p style='color: #333; font-size: 15px; margin: 0;'>{alertMessage}</p>
                            </div>
                            <h3 style='color: #721c24;'>Recommended Actions:</h3>
                            <ul style='color: #856404;'>
                                <li>Review your recent account activity</li>
                                <li>Change your password if you don't recognize this activity</li>
                                <li>Enable multi-factor authentication if not already enabled</li>
                                <li>Contact support if you need assistance</li>
                            </ul>
                            <hr style='border: none; border-top: 1px solid #ddd; margin: 30px 0;'/>
                            <p style='color: #999; font-size: 13px;'>
                                If this wasn't you, <strong>secure your account immediately</strong> by changing your password.
                            </p>
                        </div>
                        <div style='text-align: center; padding: 20px; color: #999; font-size: 12px;'>
                            <p>USP Security Platform &copy; 2025 GBMM</p>
                        </div>
                    </body>
                    </html>
                ",
                TextBody = $@"
USP Security Platform - SECURITY ALERT

Hello {userName},

We detected suspicious activity on your account:

{alertMessage}

RECOMMENDED ACTIONS:
- Review your recent account activity
- Change your password if you don't recognize this activity
- Enable multi-factor authentication if not already enabled
- Contact support if you need assistance

If this wasn't you, secure your account immediately by changing your password.

---
USP Security Platform
GBMM © 2025
                "
            };
            message.Body = bodyBuilder.ToMessageBody();

            await SendEmailAsync(message);

            _logger.LogInformation("Security alert email sent to {Email}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send security alert email to {Email}", toEmail);
            return false;
        }
    }

    private async Task SendEmailAsync(MimeMessage message)
    {
        using var client = new SmtpClient();

        await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, _settings.EnableSsl);

        if (!string.IsNullOrEmpty(_settings.SmtpUsername) && !string.IsNullOrEmpty(_settings.SmtpPassword))
        {
            await client.AuthenticateAsync(_settings.SmtpUsername, _settings.SmtpPassword);
        }

        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}

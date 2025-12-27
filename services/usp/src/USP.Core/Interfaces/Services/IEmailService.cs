namespace USP.Core.Interfaces.Services;

public interface IEmailService
{
    Task SendEmailVerificationAsync(
        string email,
        string verificationToken,
        CancellationToken cancellationToken = default);

    Task SendPasswordResetAsync(
        string email,
        string resetToken,
        CancellationToken cancellationToken = default);

    Task SendMfaCodeAsync(
        string email,
        string code,
        int expirationMinutes,
        CancellationToken cancellationToken = default);

    Task SendNotificationAsync(
        string email,
        string subject,
        string body,
        CancellationToken cancellationToken = default);
}

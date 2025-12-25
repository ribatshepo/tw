namespace USP.Core.Models.Configuration;

/// <summary>
/// Email service SMTP configuration settings
/// </summary>
public class EmailSettings
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; }
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = true;
    public int MagicLinkExpirationMinutes { get; set; } = 15;
}

using System.ComponentModel.DataAnnotations;

namespace USP.Shared.Configuration.Options;

public class EmailOptions
{
    [Required(ErrorMessage = "SMTP Host is required")]
    public string SmtpHost { get; set; } = null!;

    [Range(1, 65535)]
    public int SmtpPort { get; set; } = 587;

    public bool EnableSsl { get; set; } = true;

    public string? SmtpUsername { get; set; }

    public string? SmtpPassword { get; set; }

    [Required(ErrorMessage = "From email address is required")]
    [EmailAddress]
    public string FromEmail { get; set; } = null!;

    public string FromName { get; set; } = "USP Security Platform";

    public int TimeoutSeconds { get; set; } = 30;

    public int MaxRetries { get; set; } = 3;
}

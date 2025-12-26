using System.ComponentModel.DataAnnotations;

namespace USP.Shared.Configuration.Options;

public class DatabaseOptions
{
    [Required]
    public string Host { get; set; } = "localhost";

    [Range(1, 65535)]
    public int Port { get; set; } = 5432;

    [Required]
    public string Database { get; set; } = "usp_db";

    [Required]
    public string Username { get; set; } = "usp_user";

    [Required]
    public string Password { get; set; } = null!;

    public bool EnableSsl { get; set; } = true;

    public int MaxPoolSize { get; set; } = 100;

    public int MinPoolSize { get; set; } = 10;

    public int CommandTimeout { get; set; } = 30;

    public string GetConnectionString() =>
        $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password};SSL Mode={(EnableSsl ? "Require" : "Disable")};Maximum Pool Size={MaxPoolSize};Minimum Pool Size={MinPoolSize};";
}

using System.ComponentModel.DataAnnotations;

namespace USP.Shared.Configuration.Options;

public class RedisOptions
{
    [Required]
    public string Host { get; set; } = "localhost";

    [Range(1, 65535)]
    public int Port { get; set; } = 6379;

    public string? Password { get; set; }

    public bool EnableSsl { get; set; } = false;

    public string InstanceName { get; set; } = "USP:";

    public int Database { get; set; } = 0;

    public int ConnectTimeout { get; set; } = 5000;

    public int SyncTimeout { get; set; } = 5000;

    public string GetConnectionString() =>
        $"{Host}:{Port},password={Password},ssl={EnableSsl},connectTimeout={ConnectTimeout},syncTimeout={SyncTimeout},abortConnect=false";
}

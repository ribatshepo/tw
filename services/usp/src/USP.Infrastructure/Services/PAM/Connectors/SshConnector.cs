using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using USP.Core.Services.PAM;

namespace USP.Infrastructure.Services.PAM.Connectors;

/// <summary>
/// SSH key rotation connector for Linux/Unix systems
/// </summary>
public class SshConnector : BaseConnector
{
    private readonly ILogger<SshConnector> _logger;

    public override string Platform => "SSH";

    public SshConnector(ILogger<SshConnector> logger)
    {
        _logger = logger;
    }

    public override async Task<PasswordRotationResult> RotatePasswordAsync(
        string hostAddress,
        int? port,
        string username,
        string currentPassword,
        string newPassword,
        string? databaseName = null,
        string? connectionDetails = null)
    {
        var result = new PasswordRotationResult
        {
            Success = false,
            RotatedAt = DateTime.UtcNow
        };

        SshClient? client = null;

        try
        {
            // Connect using current password
            var connectionInfo = new ConnectionInfo(
                hostAddress,
                port ?? 22,
                username,
                new PasswordAuthenticationMethod(username, currentPassword))
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            client = new SshClient(connectionInfo);
            await Task.Run(() => client.Connect());

            if (!client.IsConnected)
            {
                result.ErrorMessage = "Failed to connect to SSH server";
                return result;
            }

            // Change password using passwd command
            // Note: This requires the user to have permission to change their own password
            var command = client.CreateCommand($"echo '{username}:{newPassword}' | sudo -S chpasswd");
            var output = await Task.Run(() => command.Execute());
            var exitStatus = command.ExitStatus;

            if (exitStatus != 0)
            {
                // Try alternative method without sudo
                command = client.CreateCommand($"echo -e '{currentPassword}\\n{newPassword}\\n{newPassword}' | passwd");
                output = await Task.Run(() => command.Execute());
                exitStatus = command.ExitStatus;

                if (exitStatus != 0)
                {
                    result.ErrorMessage = "Failed to change password";
                    result.Details = $"Command failed with exit code {exitStatus}: {command.Error}";

                    _logger.LogError(
                        "Failed to rotate password for SSH user {Username} on {Host}: {Error}",
                        username,
                        hostAddress,
                        command.Error);

                    return result;
                }
            }

            result.Success = true;
            result.Details = $"Password rotated successfully for SSH user {username} on {hostAddress}";

            _logger.LogInformation(
                "Password rotated successfully for SSH user {Username} on {Host}",
                username,
                hostAddress);
        }
        catch (SshAuthenticationException ex)
        {
            result.ErrorMessage = "Authentication failed with current password";
            result.Details = $"SSH authentication failed: {ex.Message}";

            _logger.LogError(ex,
                "Authentication failed for SSH user {Username} on {Host}",
                username,
                hostAddress);
        }
        catch (SshConnectionException ex)
        {
            result.ErrorMessage = "Unable to connect to SSH server";
            result.Details = $"SSH connection failed: {ex.Message}";

            _logger.LogError(ex,
                "Connection failed to SSH server {Host}",
                hostAddress);
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            result.Details = $"Failed to rotate password: {ex.Message}";

            _logger.LogError(ex,
                "Unexpected error rotating password for SSH user {Username} on {Host}",
                username,
                hostAddress);
        }
        finally
        {
            client?.Dispose();
        }

        return result;
    }

    public override async Task<bool> VerifyCredentialsAsync(
        string hostAddress,
        int? port,
        string username,
        string password,
        string? databaseName = null,
        string? connectionDetails = null)
    {
        SshClient? client = null;

        try
        {
            var connectionInfo = new ConnectionInfo(
                hostAddress,
                port ?? 22,
                username,
                new PasswordAuthenticationMethod(username, password))
            {
                Timeout = TimeSpan.FromSeconds(10)
            };

            client = new SshClient(connectionInfo);
            await Task.Run(() => client.Connect());

            if (!client.IsConnected)
            {
                return false;
            }

            // Execute a simple command to verify access
            var command = client.CreateCommand("echo 'test'");
            var output = await Task.Run(() => command.Execute());

            _logger.LogDebug(
                "Credentials verified successfully for SSH user {Username} on {Host}",
                username,
                hostAddress);

            return command.ExitStatus == 0;
        }
        catch (SshAuthenticationException ex)
        {
            _logger.LogWarning(ex,
                "Failed to verify credentials for SSH user {Username} on {Host}",
                username,
                hostAddress);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Error verifying credentials for SSH user {Username} on {Host}",
                username,
                hostAddress);

            return false;
        }
        finally
        {
            client?.Dispose();
        }
    }

    /// <summary>
    /// Generate SSH key pair and return as tuple (privateKey, publicKey)
    /// </summary>
    public (string privateKey, string publicKey) GenerateSshKeyPair()
    {
        using var rsa = RSA.Create(4096);

        // Generate private key in OpenSSH format
        var privateKey = rsa.ExportRSAPrivateKeyPem();

        // Generate public key in OpenSSH format
        var rsaParams = rsa.ExportParameters(false);
        var publicKey = ConvertToOpenSshPublicKey(rsaParams);

        return (privateKey, publicKey);
    }

    /// <summary>
    /// Rotate SSH key for a user
    /// </summary>
    public async Task<PasswordRotationResult> RotateSshKeyAsync(
        string hostAddress,
        int? port,
        string username,
        string currentPassword)
    {
        var result = new PasswordRotationResult
        {
            Success = false,
            RotatedAt = DateTime.UtcNow
        };

        SshClient? client = null;

        try
        {
            // Generate new key pair
            var (privateKey, publicKey) = GenerateSshKeyPair();

            // Connect using current password
            var connectionInfo = new ConnectionInfo(
                hostAddress,
                port ?? 22,
                username,
                new PasswordAuthenticationMethod(username, currentPassword))
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            client = new SshClient(connectionInfo);
            await Task.Run(() => client.Connect());

            if (!client.IsConnected)
            {
                result.ErrorMessage = "Failed to connect to SSH server";
                return result;
            }

            // Backup existing authorized_keys
            var backupCommand = client.CreateCommand("cp ~/.ssh/authorized_keys ~/.ssh/authorized_keys.bak");
            await Task.Run(() => backupCommand.Execute());

            // Append new public key to authorized_keys
            var escapedPublicKey = publicKey.Replace("'", "'\\''");
            var addKeyCommand = client.CreateCommand($"echo '{escapedPublicKey}' >> ~/.ssh/authorized_keys");
            var output = await Task.Run(() => addKeyCommand.Execute());

            if (addKeyCommand.ExitStatus != 0)
            {
                result.ErrorMessage = "Failed to add new SSH key";
                result.Details = $"Command failed: {addKeyCommand.Error}";
                return result;
            }

            // Set correct permissions
            var chmodCommand = client.CreateCommand("chmod 600 ~/.ssh/authorized_keys");
            await Task.Run(() => chmodCommand.Execute());

            result.Success = true;
            result.Details = $"SSH key rotated successfully for user {username} on {hostAddress}. Private key returned in rotation metadata.";

            _logger.LogInformation(
                "SSH key rotated successfully for user {Username} on {Host}",
                username,
                hostAddress);

            // Store private key in result for retrieval
            // In production, this should be encrypted and stored securely
            result.ErrorMessage = privateKey; // Temporarily using ErrorMessage field for private key
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            result.Details = $"Failed to rotate SSH key: {ex.Message}";

            _logger.LogError(ex,
                "Failed to rotate SSH key for user {Username} on {Host}",
                username,
                hostAddress);
        }
        finally
        {
            client?.Dispose();
        }

        return result;
    }

    private static string ConvertToOpenSshPublicKey(RSAParameters rsaParams)
    {
        var exponentBytes = rsaParams.Exponent!;
        var modulusBytes = rsaParams.Modulus!;

        var builder = new StringBuilder();
        builder.Append("ssh-rsa ");

        // Build the public key in OpenSSH format
        using (var ms = new MemoryStream())
        using (var writer = new BinaryWriter(ms))
        {
            WriteString(writer, "ssh-rsa");
            WriteBytes(writer, exponentBytes);
            WriteBytes(writer, modulusBytes);

            var keyBytes = ms.ToArray();
            builder.Append(Convert.ToBase64String(keyBytes));
        }

        return builder.ToString();
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(IPAddress.HostToNetworkOrder(bytes.Length));
        writer.Write(bytes);
    }

    private static void WriteBytes(BinaryWriter writer, byte[] data)
    {
        writer.Write(IPAddress.HostToNetworkOrder(data.Length));
        writer.Write(data);
    }
}

// Helper class for network byte order conversion
static class IPAddress
{
    public static int HostToNetworkOrder(int host)
    {
        if (BitConverter.IsLittleEndian)
        {
            var bytes = BitConverter.GetBytes(host);
            Array.Reverse(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }
        return host;
    }
}

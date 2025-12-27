namespace USP.Core.Exceptions;

/// <summary>
/// Exception thrown when attempting to perform cryptographic operations
/// on a sealed vault.
/// </summary>
public class VaultSealedException : InvalidOperationException
{
    public VaultSealedException()
        : base("Vault is sealed. Please unseal the vault to perform this operation.")
    {
    }

    public VaultSealedException(string message)
        : base(message)
    {
    }

    public VaultSealedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

namespace USP.Core.Exceptions;

public class AuthenticationException : USPException
{
    public AuthenticationException(string message, string? technicalDetails = null)
        : base("AUTH_FAILED", message, technicalDetails)
    {
    }
}

public class AuthorizationException : USPException
{
    public AuthorizationException(string message, string? technicalDetails = null)
        : base("AUTHZ_DENIED", message, technicalDetails)
    {
    }
}

public class ValidationException : USPException
{
    public ValidationException(string message, string? technicalDetails = null)
        : base("VALIDATION_ERROR", message, technicalDetails)
    {
    }
}

public class NotFoundException : USPException
{
    public NotFoundException(string resourceType, string resourceId)
        : base("NOT_FOUND", $"{resourceType} with ID '{resourceId}' was not found")
    {
    }
}

public class ConflictException : USPException
{
    public ConflictException(string message, string? technicalDetails = null)
        : base("CONFLICT", message, technicalDetails)
    {
    }
}

public class SecretNotFoundException : USPException
{
    public SecretNotFoundException(string path)
        : base("SECRET_NOT_FOUND", $"Secret at path '{path}' was not found")
    {
    }
}

public class SealedException : USPException
{
    public SealedException()
        : base("VAULT_SEALED", "Vault is sealed and cannot process requests")
    {
    }
}

public class RotationException : USPException
{
    public RotationException(string message, Exception? innerException = null)
        : base("ROTATION_FAILED", message, innerException)
    {
    }
}

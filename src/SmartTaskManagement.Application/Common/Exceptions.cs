namespace SmartTaskManagement.Application.Common;

public abstract class AppException : Exception
{
    protected AppException(string message) : base(message) { }
}

public sealed class NotFoundException : AppException
{
    public NotFoundException(string entity, object key)
        : base($"{entity} '{key}' was not found.") { }
}

public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message = "You do not have permission to perform this action.")
        : base(message) { }
}

public sealed class ConflictException : AppException
{
    public ConflictException(string message) : base(message) { }
}

public sealed class AuthenticationFailedException : AppException
{
    public AuthenticationFailedException(string message = "Invalid credentials.") : base(message) { }
}

public sealed class ExternalServiceException : AppException
{
    public ExternalServiceException(string message) : base(message) { }
}

namespace ECommerce.Core.Exceptions;

public class ForbiddenException : AppException
{
    public ForbiddenException(string message) : base(message, 403, "FORBIDDEN")
    {
    }
}

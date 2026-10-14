namespace ECommerce.Core.Exceptions;

// 401: we don't know who you are (bad credentials). Compare ForbiddenException (403): we know, but you may not.
public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message) : base(message, 401, "UNAUTHORIZED")
    {
    }
}

namespace ECommerce.Core.Exceptions;

public class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message, 404, "NOT_FOUND")
    {
    }
}

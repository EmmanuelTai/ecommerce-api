namespace ECommerce.Core.Exceptions;

// Base class for expected errors that services throw on purpose ("not found", "out of stock").
// ExceptionHandlingMiddleware turns these into the standard error response. The status code is a
// plain int because Core has no ASP.NET reference.
public abstract class AppException : Exception
{
    protected AppException(string message, int statusCode, string code) : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public int StatusCode { get; }
    public string Code { get; }
}

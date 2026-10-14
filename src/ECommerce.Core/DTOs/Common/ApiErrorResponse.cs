namespace ECommerce.Core.DTOs.Common;

// Every error response: { "success": false, "error": { "code": "...", "message": "..." } }.
public class ApiErrorResponse
{
    public ApiErrorResponse(string code, string message)
    {
        Error = new ApiError(code, message);
    }

    public bool Success => false;
    public ApiError Error { get; }
}

public record ApiError(string Code, string Message);

using ECommerce.Core.DTOs.Common;
using ECommerce.Core.Exceptions;

namespace ECommerce.Api.Middleware;

// One place that turns exceptions into HTTP responses, so controllers and services need no try/catch.
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            // Expected errors thrown on purpose by services: the message is safe to show the client.
            _logger.LogWarning("{ExceptionType}: {Message}", ex.GetType().Name, ex.Message);
            await WriteErrorAsync(context, ex.StatusCode, ex.Code, ex.Message);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client disconnected and the CancellationToken stopped the work. Nobody is
            // waiting for a response, and it isn't a server fault, so don't log it as an error.
            _logger.LogInformation("Request was cancelled by the client.");
        }
        catch (Exception ex)
        {
            // Unexpected: log everything for us, but give the client a generic message so
            // stack traces, SQL or connection details never leak.
            _logger.LogError(ex, "Unhandled exception");
            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError,
                "INTERNAL_ERROR", "An unexpected error occurred.");
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string code, string message)
    {
        // If part of the response was already sent, the status code can't be changed any more.
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new ApiErrorResponse(code, message));
    }
}

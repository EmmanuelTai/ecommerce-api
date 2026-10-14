using System.Diagnostics;
using System.Security.Claims;

namespace ECommerce.Api.Middleware;

// One log line per request: method, path, status, duration and user.
// Deliberately never logs the query string, headers or body, where passwords and tokens travel.
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            // Read after _next: authentication runs later in the pipeline, so the user is only
            // known once the request has come back out through this middleware.
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";

            _logger.LogInformation("HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs} ms (user {UserId})",
                context.Request.Method, context.Request.Path, context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds, userId);
        }
    }
}

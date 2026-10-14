using ECommerce.Api.Middleware;
using ECommerce.Core.DTOs.Common;
using ECommerce.Core.Settings;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Data.Seeding;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Options pattern: bind each config section to a typed class.
// ValidateDataAnnotations checks [Required] etc. the first time the options are used,
// so a missing user-secret fails loudly instead of becoming an empty string at runtime.
builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection(JwtSettings.SectionName))
    .ValidateDataAnnotations();
builder.Services.AddOptions<SmtpSettings>()
    .Bind(builder.Configuration.GetSection(SmtpSettings.SectionName))
    .ValidateDataAnnotations();
builder.Services.AddOptions<PaystackSettings>()
    .Bind(builder.Configuration.GetSection(PaystackSettings.SectionName))
    .ValidateDataAnnotations();
builder.Services.AddOptions<SeedSettings>()
    .Bind(builder.Configuration.GetSection(SeedSettings.SectionName))
    .ValidateDataAnnotations();

// DbContext, UnitOfWork and seeder. Lifetimes are explained in DependencyInjection.cs.
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // [ApiController] rejects invalid DTOs (DataAnnotations) before the action runs.
        // Replace its default ProblemDetails body with our standard error format.
        options.InvalidModelStateResponseFactory = context =>
        {
            var messages = context.ModelState
                .Where(entry => entry.Value!.Errors.Count > 0)
                .SelectMany(entry => entry.Value!.Errors.Select(error => $"{entry.Key}: {error.ErrorMessage}"));
            return new BadRequestObjectResult(new ApiErrorResponse("VALIDATION_ERROR", string.Join(" ", messages)));
        };

        // Leave empty error responses (e.g. return NotFound()) empty, so UseStatusCodePages below fills them in.
        options.SuppressMapClientErrors = true;
    });
builder.Services.AddOpenApi();

var app = builder.Build();

// Seed once at startup. There is no HTTP request here, so we create a scope by hand
// to get scoped services (DbContext). Migrations are applied separately with `dotnet ef database update`.
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
    await seeder.SeedAsync(includeTestCustomer: app.Environment.IsDevelopment());
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();             // JSON document at /openapi/v1.json
    app.MapScalarApiReference();  // UI at /scalar
}

// Middleware order matters: each one wraps everything registered after it.
// 1. Request logging is outermost, so it sees the final status code, including a 500 set by the handler below.
// 2. The exception handler comes before auth and controllers, so it catches errors from all of them.
// 3. Status code pages give empty error responses (401/403 from auth, unknown routes) the standard JSON body.
// 4. Authentication (added in T5) must run before authorization: who you are, then what you may do.
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseStatusCodePages(async statusCodeContext =>
{
    var response = statusCodeContext.HttpContext.Response;
    var (code, message) = response.StatusCode switch
    {
        StatusCodes.Status401Unauthorized => ("UNAUTHORIZED", "Authentication is required."),
        StatusCodes.Status403Forbidden => ("FORBIDDEN", "You do not have permission to do this."),
        StatusCodes.Status404NotFound => ("NOT_FOUND", "The requested resource was not found."),
        StatusCodes.Status405MethodNotAllowed => ("METHOD_NOT_ALLOWED", "This HTTP method is not allowed here."),
        _ => ("ERROR", "The request could not be completed.")
    };
    await response.WriteAsJsonAsync(new ApiErrorResponse(code, message));
});

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

await app.RunAsync();

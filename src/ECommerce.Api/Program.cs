using System.Text;
using System.Text.Json.Serialization;
using ECommerce.Api.Middleware;
using ECommerce.Api.Services;
using ECommerce.Core.DTOs.Common;
using ECommerce.Core.Interfaces;
using ECommerce.Core.Services;
using ECommerce.Core.Settings;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Data.Seeding;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Options pattern: bind each config section to a typed class.
// ValidateDataAnnotations checks [Required] etc.; ValidateOnStart runs that check when the app starts,
// so a missing user-secret stops startup with a clear message instead of failing on some later request.
builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection(JwtSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<SmtpSettings>()
    .Bind(builder.Configuration.GetSection(SmtpSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
// No ValidateOnStart yet: Paystack:SecretKey is only set up in T10. Add it then.
builder.Services.AddOptions<PaystackSettings>()
    .Bind(builder.Configuration.GetSection(PaystackSettings.SectionName))
    .ValidateDataAnnotations();
builder.Services.AddOptions<SeedSettings>()
    .Bind(builder.Configuration.GetSection(SeedSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// DbContext, UnitOfWork and seeder. Lifetimes are explained in DependencyInjection.cs.
builder.Services.AddInfrastructure(builder.Configuration);

// Scoped: one per request, like the UnitOfWork they use. CurrentUserService reads the current request's user.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAuthService, AuthService>();
// Singleton: it only holds the settings and keeps no per-request state.
builder.Services.AddSingleton<ITokenService, JwtTokenService>();

// JWT bearer authentication: reads "Authorization: Bearer <token>" and checks it on every request.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
// Configured from the validated JwtSettings rather than raw config, so the key is guaranteed to be set.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtSettings>>((options, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            // The default allows 5 minutes past expiry for clock differences between servers; we only have one.
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // [ApiController] rejects invalid DTOs (DataAnnotations) before the action runs.
        // Replace its default ProblemDetails body with our standard error format.
        options.InvalidModelStateResponseFactory = context =>
        {
            // System.Text.Json reports body errors under JSON paths ("$", "$.price"). Its messages mention
            // line numbers and .NET types, so give the client one plain message instead.
            if (context.ModelState.Keys.Any(key => key.StartsWith('$')))
            {
                return new BadRequestObjectResult(
                    new ApiErrorResponse("INVALID_JSON", "The request body is not valid JSON."));
            }

            var messages = context.ModelState
                .Where(entry => entry.Value!.Errors.Count > 0)
                .SelectMany(entry => entry.Value!.Errors.Select(error => $"{entry.Key}: {error.ErrorMessage}"));
            return new BadRequestObjectResult(new ApiErrorResponse("VALIDATION_ERROR", string.Join(" ", messages)));
        };

        // Leave empty error responses (e.g. return NotFound()) empty, so UseStatusCodePages below fills them in.
        options.SuppressMapClientErrors = true;
    })
    // Enums as names ("Customer") instead of numbers, in both responses and requests.
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi(options =>
{
    // Describe the Bearer scheme in the OpenAPI document, so Scalar shows a box to paste a token into.
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[JwtBearerDefaults.AuthenticationScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Paste the token from /api/auth/login (without the word Bearer)."
        };
        return Task.CompletedTask;
    });

    // Mark only [Authorize] endpoints as needing the token, so the docs show which ones are protected.
    options.AddOperationTransformer((operation, context, _) =>
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any())
        {
            operation.Security =
            [
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, context.Document)] = []
                }
            ];
        }
        return Task.CompletedTask;
    });
});

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
// 4. Authentication must run before authorization: who you are, then what you may do.
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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.RunAsync();

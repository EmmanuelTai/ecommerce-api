using ECommerce.Core.Settings;
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

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();             // JSON document at /openapi/v1.json
    app.MapScalarApiReference();  // UI at /scalar
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

using ECommerce.Core.Settings;
using ECommerce.Infrastructure.Data;
using ECommerce.Infrastructure.Data.Seeding;
using Microsoft.EntityFrameworkCore;
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

// Scoped (the AddDbContext default): one DbContext per HTTP request. Moves into AddInfrastructure() in T3.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<DbSeeder>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Seed once at startup. There is no HTTP request here, so we create a scope by hand
// to get scoped services (DbContext). Migrations are applied separately with `dotnet ef database update`.
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
    await seeder.SeedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();             // JSON document at /openapi/v1.json
    app.MapScalarApiReference();  // UI at /scalar
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

await app.RunAsync();

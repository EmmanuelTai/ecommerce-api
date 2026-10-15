using ECommerce.Core.Interfaces;
using ECommerce.Infrastructure.Data;
using ECommerce.Infrastructure.Data.Seeding;
using ECommerce.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Infrastructure;

public static class DependencyInjection
{
    // Program.cs only knows "add infrastructure"; which classes implement Core's interfaces stays in here.
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not set.");

        // Scoped (the AddDbContext default): one DbContext per HTTP request. DbContext isn't thread-safe
        // and its change tracker should only hold one request's entities.
        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

        // Scoped, to match the DbContext it wraps: every service in a request gets the same UnitOfWork,
        // and so the same DbContext and transaction. A singleton would hold on to the first request's
        // DbContext forever (a "captive dependency").
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<DbSeeder>();

        // Transient: a new instance each time one is asked for. It keeps no state (each send opens its own
        // SMTP connection), so there is nothing to share; singleton would work too, scoped would add nothing.
        services.AddTransient<IEmailService, SmtpEmailService>();

        return services;
    }
}

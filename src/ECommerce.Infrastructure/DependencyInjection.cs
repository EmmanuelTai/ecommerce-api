using System.Net.Http.Headers;
using ECommerce.Core.Interfaces;
using ECommerce.Core.Settings;
using ECommerce.Infrastructure.Data;
using ECommerce.Infrastructure.Data.Seeding;
using ECommerce.Infrastructure.Email;
using ECommerce.Infrastructure.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

        // Typed HttpClient: PaystackClient is created per use (transient) with an HttpClient from IHttpClientFactory.
        // The factory pools and recycles the underlying connections. Calling `new HttpClient()` per request can run
        // out of sockets under load, and a single static one never notices DNS changes.
        services.AddHttpClient<IPaystackClient, PaystackClient>((serviceProvider, client) =>
            {
                var settings = serviceProvider.GetRequiredService<IOptions<PaystackSettings>>().Value;
                client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.SecretKey);
                // The default is 100 seconds; the customer is waiting on this request.
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            // The factory's logging can write request headers; never let the secret key reach the logs.
            .RedactLoggedHeaders(["Authorization"]);

        return services;
    }
}

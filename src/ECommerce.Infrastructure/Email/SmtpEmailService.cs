using ECommerce.Core.DTOs.Email;
using ECommerce.Core.Interfaces;
using ECommerce.Core.Settings;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ECommerce.Infrastructure.Email;

// Sends email with MailKit (the System.Net.Mail SmtpClient is marked obsolete; Microsoft points to MailKit).
public class SmtpEmailService(IOptions<SmtpSettings> options, ILogger<SmtpEmailService> logger) : IEmailService
{
    // MailKit's default is 2 minutes. Sending happens inside the request, so a dead mail server
    // shouldn't keep the customer waiting that long for a response.
    private const int TimeoutMilliseconds = 10_000;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;

        // Everything in the try, so this method keeps its promise never to throw (see IEmailService).
        try
        {
            var mime = new MimeMessage();
            mime.From.Add(new MailboxAddress(settings.FromName, settings.FromEmail));
            mime.To.Add(new MailboxAddress(message.ToName, message.ToEmail));
            mime.Subject = message.Subject;
            mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody }.ToMessageBody();

            // A new client per email: MailKit's SmtpClient isn't thread-safe, and at this volume
            // the cost of connecting each time doesn't matter.
            using var client = new SmtpClient { Timeout = TimeoutMilliseconds };

            // Fake inboxes (smtp4dev, Mailpit) speak plain SMTP. A real provider sets UseSsl, and Auto picks
            // the right TLS mode from the port: TLS straight away on 465, STARTTLS on 587.
            var socketOptions = settings.UseSsl ? SecureSocketOptions.Auto : SecureSocketOptions.None;
            await client.ConnectAsync(settings.Host, settings.Port, socketOptions, cancellationToken);

            // A username with no password is a config mistake: the server rejects it, and the catch below logs it.
            if (!string.IsNullOrEmpty(settings.Username))
            {
                await client.AuthenticateAsync(settings.Username, settings.Password ?? string.Empty, cancellationToken);
            }

            await client.SendAsync(mime, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);

            logger.LogInformation("Email sent: {Subject}", message.Subject);
        }
        catch (Exception ex)
        {
            // Logged and swallowed. The subject says which email failed (it includes the order number);
            // the recipient address is left out to keep personal data out of the logs.
            logger.LogError(ex, "Email failed to send: {Subject}", message.Subject);
        }
    }
}

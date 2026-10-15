using ECommerce.Core.DTOs.Email;

namespace ECommerce.Core.Interfaces;

// Defined in Core, implemented in Infrastructure (SmtpEmailService), so services don't know about MailKit or SMTP.
public interface IEmailService
{
    // Never throws: a failed send is logged and swallowed. Emails are sent after the database commit,
    // so by then the order (or account) already exists, and a mail server problem must not turn that into an error.
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

namespace ECommerce.Core.DTOs.Email;

// One email, ready to send. Built by EmailTemplates; the sender (SMTP) only delivers it.
public record EmailMessage(string ToEmail, string ToName, string Subject, string HtmlBody);

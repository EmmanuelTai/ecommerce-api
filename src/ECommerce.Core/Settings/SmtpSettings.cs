using System.ComponentModel.DataAnnotations;

namespace ECommerce.Core.Settings;

public class SmtpSettings
{
    public const string SectionName = "Smtp";

    [Required]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; }

    public bool UseSsl { get; set; }

    // smtp4dev and Mailpit need no credentials, so these are optional. Real SMTP: set them with user-secrets.
    public string? Username { get; set; }
    public string? Password { get; set; }

    [Required, EmailAddress]
    public string FromEmail { get; set; } = string.Empty;

    [Required]
    public string FromName { get; set; } = string.Empty;
}

using System.ComponentModel.DataAnnotations;

namespace ECommerce.Core.Settings;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    // Secret: set with user-secrets ("Jwt:Key"), at least 32 characters for HMAC-SHA256.
    [Required, MinLength(32)]
    public string Key { get; set; } = string.Empty;

    [Range(1, 1440)]
    public int ExpiryMinutes { get; set; } = 60;
}

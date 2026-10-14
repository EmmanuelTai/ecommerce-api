using System.ComponentModel.DataAnnotations;

namespace ECommerce.Core.Settings;

public class SeedSettings
{
    public const string SectionName = "Seed";

    [Required, EmailAddress]
    public string AdminEmail { get; set; } = string.Empty;

    // Secret: set with user-secrets ("Seed:AdminPassword").
    [Required, MinLength(8)]
    public string AdminPassword { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string CustomerEmail { get; set; } = string.Empty;

    // Secret: set with user-secrets ("Seed:CustomerPassword").
    [Required, MinLength(8)]
    public string CustomerPassword { get; set; } = string.Empty;
}

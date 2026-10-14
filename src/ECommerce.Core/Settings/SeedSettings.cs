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

    // The test customer is only seeded in Development, so these are optional (production doesn't need them).
    // The seeder checks they are set when it actually uses them.
    [EmailAddress]
    public string? CustomerEmail { get; set; }

    // Secret: set with user-secrets ("Seed:CustomerPassword").
    [MinLength(8)]
    public string? CustomerPassword { get; set; }
}

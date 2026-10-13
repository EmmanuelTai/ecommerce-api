using System.ComponentModel.DataAnnotations;

namespace ECommerce.Core.Settings;

public class PaystackSettings
{
    public const string SectionName = "Paystack";

    [Required, Url]
    public string BaseUrl { get; set; } = string.Empty;

    // Secret: set with user-secrets ("Paystack:SecretKey"). Test keys only (sk_test_...).
    [Required]
    public string SecretKey { get; set; } = string.Empty;

    // Where Paystack sends the customer after paying.
    public string? CallbackUrl { get; set; }
}

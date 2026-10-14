using System.ComponentModel.DataAnnotations;

namespace ECommerce.Core.DTOs.Auth;

// No Role property: the client can't choose one. Everyone who registers is a Customer.
public class RegisterRequest
{
    [Required, StringLength(100)]
    public string FullName { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; init; } = string.Empty;

    // BCrypt only uses the first 72 bytes of a password, so longer ones are rejected rather than silently cut.
    [Required, StringLength(72, MinimumLength = 8)]
    public string Password { get; init; } = string.Empty;

    [StringLength(20)]
    public string? Phone { get; init; }
}

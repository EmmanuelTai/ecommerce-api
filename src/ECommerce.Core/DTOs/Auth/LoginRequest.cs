using System.ComponentModel.DataAnnotations;

namespace ECommerce.Core.DTOs.Auth;

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

using System.ComponentModel.DataAnnotations;

namespace ECommerce.Core.DTOs.Auth;

// Only the fields a user may change about themselves. Email, role and password are not here.
public class UpdateProfileRequest
{
    [Required, StringLength(100)]
    public string FullName { get; init; } = string.Empty;

    [StringLength(20)]
    public string? Phone { get; init; }
}

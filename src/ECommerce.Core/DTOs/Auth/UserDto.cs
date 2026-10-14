using ECommerce.Core.Enums;

namespace ECommerce.Core.DTOs.Auth;

// What the API shows about a user. Never includes the password hash.
public record UserDto(int Id, string FullName, string Email, string? Phone, Role Role, DateTime CreatedAt);

using ECommerce.Core.DTOs.Auth;

namespace ECommerce.Core.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<UserDto> GetCurrentUserAsync(CancellationToken cancellationToken = default);
    Task<UserDto> UpdateCurrentUserAsync(UpdateProfileRequest request, CancellationToken cancellationToken = default);
}

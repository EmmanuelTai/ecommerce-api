using ECommerce.Core.DTOs.Auth;
using ECommerce.Core.Entities;
using ECommerce.Core.Enums;
using ECommerce.Core.Exceptions;
using ECommerce.Core.Interfaces;

namespace ECommerce.Core.Services;

public class AuthService(IUnitOfWork unitOfWork, ITokenService tokenService, ICurrentUserService currentUser) : IAuthService
{
    // Same as the seeder. Each +1 doubles the hashing time; 12 is roughly 250 ms, slow enough to hurt brute force.
    private const int BCryptWorkFactor = 12;

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);

        // Friendly 409 for the normal case. Two simultaneous sign-ups with the same email could both pass
        // this check; the unique index on Email still stops the second one (it surfaces as a 500).
        if (await unitOfWork.Users.EmailExistsAsync(email, cancellationToken))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, BCryptWorkFactor),
            Phone = NormalizePhone(request.Phone),
            Role = Role.Customer, // Always; admins are seeded, never registered.
            Cart = new Cart()     // Saved with the user in the same SaveChanges.
        };

        unitOfWork.Users.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return CreateAuthResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await unitOfWork.Users.GetByEmailAsync(NormalizeEmail(request.Email), cancellationToken);

        // One message for both cases, so the response doesn't reveal which emails have accounts.
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedException("Invalid email or password.");
        }

        return CreateAuthResponse(user);
    }

    public async Task<UserDto> GetCurrentUserAsync(CancellationToken cancellationToken = default) =>
        await unitOfWork.Users.GetDtoByIdAsync(currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException("User not found.");

    public async Task<UserDto> UpdateCurrentUserAsync(UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        // Tracked, because we're changing it: SaveChanges detects the edits and updates only those columns.
        var user = await unitOfWork.Users.GetByIdAsync(currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        user.FullName = request.FullName.Trim();
        user.Phone = NormalizePhone(request.Phone);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(user);
    }

    private AuthResponse CreateAuthResponse(User user)
    {
        var (token, expiresAt) = tokenService.CreateToken(user);
        return new AuthResponse(token, expiresAt, ToDto(user));
    }

    private static UserDto ToDto(User user) =>
        new(user.Id, user.FullName, user.Email, user.Phone, user.Role, user.CreatedAt);

    // Stored lower-case so "Ada@Mail.com" and "ada@mail.com" are the same account.
    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string? NormalizePhone(string? phone) =>
        string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
}

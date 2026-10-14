using ECommerce.Core.DTOs.Auth;
using ECommerce.Core.Entities;

namespace ECommerce.Core.Interfaces;

public interface IUserRepository : IGenericRepository<User>
{
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    // Read-only (not tracked): login only needs to check the password hash.
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    // Read-only, projected straight to the DTO.
    Task<UserDto?> GetDtoByIdAsync(int id, CancellationToken cancellationToken = default);
}

using ECommerce.Core.DTOs.Auth;
using ECommerce.Core.Entities;
using ECommerce.Core.Interfaces;
using ECommerce.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Repositories;

public class UserRepository(AppDbContext db) : GenericRepository<User>(db), IUserRepository
{
    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
        Db.Users.AnyAsync(u => u.Email == email, cancellationToken);

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        Db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    // Select makes SQL fetch only the DTO's columns, so the password hash never leaves the database.
    public Task<UserDto?> GetDtoByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Db.Users
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UserDto(u.Id, u.FullName, u.Email, u.Phone, u.Role, u.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);
}

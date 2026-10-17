using ECommerce.Core.DTOs.Admin;
using ECommerce.Core.DTOs.Auth;
using ECommerce.Core.DTOs.Common;
using ECommerce.Core.Entities;
using ECommerce.Core.Enums;
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

    public async Task<PagedResult<CustomerDto>> GetCustomerDtosAsync(CustomerQuery query, CancellationToken cancellationToken = default)
    {
        var customers = Db.Users.AsNoTracking().Where(u => u.Role == Role.Customer);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            customers = customers.Where(u => u.FullName.Contains(search) || u.Email.Contains(search));
        }

        var totalCount = await customers.CountAsync(cancellationToken);
        var items = await customers
            .OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            // Same as GetDtoByIdAsync: only the DTO's columns are selected, so PasswordHash is never read.
            .Select(u => new CustomerDto(u.Id, u.FullName, u.Email, u.Phone, u.Orders.Count(), u.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<CustomerDto>(items, new PaginationMeta(query.Page, query.PageSize, totalCount));
    }
}

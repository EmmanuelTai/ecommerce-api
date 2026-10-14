using ECommerce.Core.Entities;
using ECommerce.Core.Interfaces;
using ECommerce.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Repositories;

public class CartRepository(AppDbContext db) : GenericRepository<Cart>(db), ICartRepository
{
    // Eager loading: lazy loading is off, so without Include the Items and Products would be empty.
    public Task<Cart?> GetByUserIdWithItemsAsync(int userId, CancellationToken cancellationToken = default) =>
        Db.Carts
            .Include(c => c.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
}

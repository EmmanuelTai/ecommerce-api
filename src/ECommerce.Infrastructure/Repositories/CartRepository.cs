using ECommerce.Core.DTOs.Cart;
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

    // One SQL query joining CartItems, Carts and Products; no Include needed because Select names the columns.
    // Price, stock and IsActive come straight from the Products table, so they are always current.
    public async Task<IReadOnlyList<CartItemDto>> GetItemDtosByUserIdAsync(int userId, CancellationToken cancellationToken = default) =>
        await Db.CartItems
            .AsNoTracking()
            .Where(i => i.Cart.UserId == userId)
            .OrderBy(i => i.Id) // The order the lines were added.
            .Select(i => new CartItemDto(
                i.Id,
                i.ProductId,
                i.Product.Name,
                i.Product.ImageUrl,
                i.Product.Price,
                i.Quantity,
                i.Product.StockQuantity,
                i.Product.IsActive && i.Product.StockQuantity >= i.Quantity))
            .ToListAsync(cancellationToken);
}

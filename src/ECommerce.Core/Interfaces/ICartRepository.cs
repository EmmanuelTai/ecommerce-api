using ECommerce.Core.DTOs.Cart;
using ECommerce.Core.Entities;

namespace ECommerce.Core.Interfaces;

public interface ICartRepository : IGenericRepository<Cart>
{
    // Tracked, with Items and each item's Product loaded, ready to be changed.
    Task<Cart?> GetByUserIdWithItemsAsync(int userId, CancellationToken cancellationToken = default);

    // Read-only: the user's cart lines with current prices and stock, projected to DTOs.
    Task<IReadOnlyList<CartItemDto>> GetItemDtosByUserIdAsync(int userId, CancellationToken cancellationToken = default);
}

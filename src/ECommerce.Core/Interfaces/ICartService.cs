using ECommerce.Core.DTOs.Cart;

namespace ECommerce.Core.Interfaces;

// Always works on the current user's cart; there is no cart ID in any route.
public interface ICartService
{
    Task<CartDto> GetCartAsync(CancellationToken cancellationToken = default);
    Task<CartDto> AddItemAsync(AddCartItemRequest request, CancellationToken cancellationToken = default);
    Task<CartDto> UpdateItemAsync(int itemId, UpdateCartItemRequest request, CancellationToken cancellationToken = default);
    Task RemoveItemAsync(int itemId, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

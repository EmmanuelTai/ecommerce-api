using ECommerce.Core.DTOs.Common;
using ECommerce.Core.DTOs.Orders;
using ECommerce.Core.Entities;
using ECommerce.Core.Enums;

namespace ECommerce.Core.Interfaces;

public interface IOrderRepository : IGenericRepository<Order>
{
    // Tracked, with Items loaded: used when changing an order (cancel, status update).
    Task<Order?> GetByIdWithItemsAsync(int orderId, CancellationToken cancellationToken = default);

    // Read-only: one page of the user's orders, newest first.
    Task<PagedResult<OrderSummaryDto>> GetSummaryDtosByUserIdAsync(int userId, OrderQuery query, CancellationToken cancellationToken = default);

    // Read-only: null if the order doesn't exist or belongs to someone else.
    Task<OrderDto?> GetDtoByIdForUserAsync(int orderId, int userId, CancellationToken cancellationToken = default);

    // Changes the status only if it is still "from". Returns false if another request changed it first.
    Task<bool> TryChangeStatusAsync(int orderId, OrderStatus from, OrderStatus to, CancellationToken cancellationToken = default);
}

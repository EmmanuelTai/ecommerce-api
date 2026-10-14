using ECommerce.Core.DTOs.Common;
using ECommerce.Core.DTOs.Orders;

namespace ECommerce.Core.Interfaces;

// Always the current user's orders; someone else's order ID gives 404.
public interface IOrderService
{
    Task<OrderDto> PlaceOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<OrderSummaryDto>> GetMyOrdersAsync(OrderQuery query, CancellationToken cancellationToken = default);
    Task<OrderDto> GetMyOrderAsync(int orderId, CancellationToken cancellationToken = default);
    Task<OrderDto> CancelAsync(int orderId, CancellationToken cancellationToken = default);
}

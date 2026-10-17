using ECommerce.Core.DTOs.Admin;
using ECommerce.Core.DTOs.Common;

namespace ECommerce.Core.Interfaces;

// Admin-only: every customer's orders, not just the caller's.
public interface IAdminOrderService
{
    Task<PagedResult<AdminOrderSummaryDto>> GetOrdersAsync(AdminOrderQuery query, CancellationToken cancellationToken = default);
    Task<AdminOrderDto> GetOrderAsync(int orderId, CancellationToken cancellationToken = default);
    Task<AdminOrderDto> UpdateStatusAsync(int orderId, UpdateOrderStatusRequest request, CancellationToken cancellationToken = default);
}

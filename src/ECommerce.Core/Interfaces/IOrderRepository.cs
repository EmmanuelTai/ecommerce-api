using ECommerce.Core.Entities;

namespace ECommerce.Core.Interfaces;

public interface IOrderRepository : IGenericRepository<Order>
{
    // Tracked, with Items loaded: used when changing an order (cancel, status update).
    Task<Order?> GetByIdWithItemsAsync(int orderId, CancellationToken cancellationToken = default);
}

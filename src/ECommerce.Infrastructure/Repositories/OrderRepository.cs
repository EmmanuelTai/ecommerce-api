using ECommerce.Core.Entities;
using ECommerce.Core.Interfaces;
using ECommerce.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Repositories;

public class OrderRepository(AppDbContext db) : GenericRepository<Order>(db), IOrderRepository
{
    public Task<Order?> GetByIdWithItemsAsync(int orderId, CancellationToken cancellationToken = default) =>
        Db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
}

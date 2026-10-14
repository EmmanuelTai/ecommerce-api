using ECommerce.Core.DTOs.Common;
using ECommerce.Core.DTOs.Orders;
using ECommerce.Core.Entities;
using ECommerce.Core.Enums;
using ECommerce.Core.Interfaces;
using ECommerce.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Repositories;

public class OrderRepository(AppDbContext db) : GenericRepository<Order>(db), IOrderRepository
{
    // Eager loading: lazy loading is off, so without Include the Items would be empty.
    public Task<Order?> GetByIdWithItemsAsync(int orderId, CancellationToken cancellationToken = default) =>
        Db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

    public async Task<PagedResult<OrderSummaryDto>> GetSummaryDtosByUserIdAsync(int userId, OrderQuery query, CancellationToken cancellationToken = default)
    {
        var orders = Db.Orders.AsNoTracking().Where(o => o.UserId == userId);

        var totalCount = await orders.CountAsync(cancellationToken);
        var items = await orders
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id) // Id breaks ties, as in products.
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            // Items.Count() becomes a COUNT subquery in the same SQL statement; the lines themselves aren't loaded.
            .Select(o => new OrderSummaryDto(o.Id, o.OrderNumber, o.Status, o.TotalAmount, o.Items.Count(), o.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<OrderSummaryDto>(items, new PaginationMeta(query.Page, query.PageSize, totalCount));
    }

    // The user ID is part of the WHERE, so someone else's order looks exactly like a missing one.
    // No Include: Select names the columns it needs, and EF loads the lines in the same query.
    public Task<OrderDto?> GetDtoByIdForUserAsync(int orderId, int userId, CancellationToken cancellationToken = default) =>
        Db.Orders
            .AsNoTracking()
            .Where(o => o.Id == orderId && o.UserId == userId)
            .Select(o => new OrderDto(
                o.Id, o.OrderNumber, o.Status, o.TotalAmount,
                o.ShippingFullName, o.ShippingPhone, o.ShippingStreet, o.ShippingCity, o.ShippingState,
                o.CreatedAt, o.UpdatedAt,
                o.Items
                    .OrderBy(i => i.Id)
                    .Select(i => new OrderItemDto(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity, i.Subtotal))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> TryChangeStatusAsync(int orderId, OrderStatus from, OrderStatus to, CancellationToken cancellationToken = default)
    {
        // Same idea as the stock update: "is it still Pending?" and "set it to Cancelled" happen in one statement,
        // so two cancel requests at once can't both pass the check and return the stock twice.
        var rows = await Db.Orders
            .Where(o => o.Id == orderId && o.Status == from)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, to)
                .SetProperty(o => o.UpdatedAt, DateTime.UtcNow),
                cancellationToken);

        return rows == 1;
    }
}

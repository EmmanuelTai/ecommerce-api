using System.Security.Cryptography;
using ECommerce.Core.DTOs.Common;
using ECommerce.Core.DTOs.Orders;
using ECommerce.Core.Entities;
using ECommerce.Core.Enums;
using ECommerce.Core.Exceptions;
using ECommerce.Core.Interfaces;

namespace ECommerce.Core.Services;

public class OrderService(IUnitOfWork unitOfWork, ICurrentUserService currentUser) : IOrderService
{
    private const string OrderNumberChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // No 0/O or 1/I, easy to read out.

    public async Task<OrderDto> PlaceOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        // One transaction around the whole checkout: the stock updates, the new order and the emptied cart
        // are saved together or not at all. Without it, a failure on the third line would leave stock taken
        // for the first two with no order to show for it.
        int orderId;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Eager loading: Items and each item's Product in one query. Tracked, because the cart is emptied below.
            var cart = await unitOfWork.Carts.GetByUserIdWithItemsAsync(currentUser.UserId, cancellationToken)
                ?? throw new NotFoundException("Cart not found.");

            if (cart.Items.Count == 0)
            {
                throw new BadRequestException("Your cart is empty.");
            }

            // Sorted by product ID so every checkout locks product rows in the same order. Each UPDATE keeps
            // its row lock until commit; if one checkout locked A then B while another locked B then A,
            // each would wait for the other forever (a deadlock) and SQL Server would kill one of them.
            var lines = cart.Items.OrderBy(i => i.ProductId).ToList();

            foreach (var line in lines)
            {
                // The conditional UPDATE is the real stock check: WHERE StockQuantity >= qty is evaluated
                // under the row lock, so a second buyer waits for the first to commit, then sees the new stock.
                // 0 rows means sold out or deactivated since the cart was loaded.
                if (!await unitOfWork.Products.TryDecreaseStockAsync(line.ProductId, line.Quantity, cancellationToken))
                {
                    throw new ConflictException(
                        $"Not enough stock for {line.Product.Name}. Update your cart and try again.");
                }
            }

            // Snapshots: name and price are copied from the Products rows loaded above, never from the client
            // or the cache, so the order keeps showing what was paid even if the product changes later.
            // Subtotals are worked out in C# so they stay at two decimal places.
            var items = lines.Select(line => new OrderItem
            {
                ProductId = line.ProductId,
                ProductName = line.Product.Name,
                UnitPrice = line.Product.Price,
                Quantity = line.Quantity,
                Subtotal = line.Product.Price * line.Quantity
            }).ToList();

            var order = new Order
            {
                UserId = currentUser.UserId,
                OrderNumber = CreateOrderNumber(),
                Status = OrderStatus.Pending,
                TotalAmount = items.Sum(i => i.Subtotal),
                ShippingFullName = request.ShippingFullName.Trim(),
                ShippingPhone = request.ShippingPhone.Trim(),
                ShippingStreet = request.Street.Trim(),
                ShippingCity = request.City.Trim(),
                ShippingState = request.State.Trim(),
                Items = items
            };

            unitOfWork.Orders.Add(order);
            cart.Items.Clear();

            // Saves the order, its lines and the cart deletes, then commits together with the stock updates.
            await unitOfWork.CommitAsync(cancellationToken);
            orderId = order.Id; // Filled in by SaveChanges.
        }
        catch
        {
            // Undoes any stock already taken. CancellationToken.None: if the client disconnected,
            // we still want the rollback to run rather than be cancelled too.
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }

        return await GetMyOrderAsync(orderId, cancellationToken);
    }

    public Task<PagedResult<OrderSummaryDto>> GetMyOrdersAsync(OrderQuery query, CancellationToken cancellationToken = default) =>
        unitOfWork.Orders.GetSummaryDtosByUserIdAsync(currentUser.UserId, query, cancellationToken);

    // 404 for someone else's order, not 403: a 403 would confirm that the order ID exists.
    public async Task<OrderDto> GetMyOrderAsync(int orderId, CancellationToken cancellationToken = default) =>
        await unitOfWork.Orders.GetDtoByIdForUserAsync(orderId, currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException("Order not found.");

    public async Task<OrderDto> CancelAsync(int orderId, CancellationToken cancellationToken = default)
    {
        // Status change and stock return in one transaction: either the order is cancelled and its stock is back,
        // or neither happened.
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var order = await unitOfWork.Orders.GetByIdWithItemsAsync(orderId, cancellationToken);
            if (order is null || order.UserId != currentUser.UserId)
            {
                throw new NotFoundException("Order not found.");
            }

            // Conditional, like the stock update: two cancel requests at once (or a cancel racing the payment
            // webhook in T10) can't both see Pending, so stock is returned at most once.
            if (!await unitOfWork.Orders.TryChangeStatusAsync(order.Id, OrderStatus.Pending, OrderStatus.Cancelled, cancellationToken))
            {
                throw new ConflictException("Only pending orders can be cancelled.");
            }

            // Same product-ID order as checkout, for the same deadlock reason.
            foreach (var item in order.Items.OrderBy(i => i.ProductId))
            {
                await unitOfWork.Products.IncreaseStockAsync(item.ProductId, item.Quantity, cancellationToken);
            }

            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }

        // Read back fresh: the tracked order above still says Pending, because ExecuteUpdate bypasses the change tracker.
        return await GetMyOrderAsync(orderId, cancellationToken);
    }

    // ORD-20261014-7KQ2MX. Random rather than sequential, so order numbers don't reveal how many orders
    // the store gets. 32^6 ≈ 1 billion per day; the unique index catches the unlikely clash (409, retry).
    private static string CreateOrderNumber() =>
        $"ORD-{DateTime.UtcNow:yyyyMMdd}-{RandomNumberGenerator.GetString(OrderNumberChars, 6)}";
}

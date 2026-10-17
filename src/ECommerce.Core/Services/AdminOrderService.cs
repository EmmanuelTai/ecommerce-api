using ECommerce.Core.DTOs.Admin;
using ECommerce.Core.DTOs.Common;
using ECommerce.Core.Enums;
using ECommerce.Core.Exceptions;
using ECommerce.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ECommerce.Core.Services;

public class AdminOrderService(
    IUnitOfWork unitOfWork,
    IEmailService emailService,
    IMemoryCache cache) : IAdminOrderService
{
    // The order status flow, as data: each status and the statuses an admin may move it to.
    //   Pending → Cancelled (Pending → Paid only happens through a confirmed Paystack payment, never by hand)
    //   Paid → Processing → Shipped → Delivered
    // Delivered and Cancelled are final. Anything not listed is a 409.
    private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedChanges = new()
    {
        [OrderStatus.Pending] = [OrderStatus.Cancelled],
        [OrderStatus.Paid] = [OrderStatus.Processing],
        [OrderStatus.Processing] = [OrderStatus.Shipped],
        [OrderStatus.Shipped] = [OrderStatus.Delivered]
    };

    public Task<PagedResult<AdminOrderSummaryDto>> GetOrdersAsync(AdminOrderQuery query, CancellationToken cancellationToken = default) =>
        unitOfWork.Orders.GetAdminSummaryDtosAsync(query, cancellationToken);

    // No owner check here: admins may see any order. The controller's [Authorize(Roles = "Admin")] is the guard.
    public async Task<AdminOrderDto> GetOrderAsync(int orderId, CancellationToken cancellationToken = default) =>
        await unitOfWork.Orders.GetAdminDtoByIdAsync(orderId, cancellationToken)
            ?? throw new NotFoundException("Order not found.");

    public async Task<AdminOrderDto> UpdateStatusAsync(int orderId, UpdateOrderStatusRequest request, CancellationToken cancellationToken = default)
    {
        var newStatus = request.Status!.Value; // [Required] has already rejected a missing status.

        var order = await unitOfWork.Orders.GetByIdWithItemsAsync(orderId, cancellationToken)
            ?? throw new NotFoundException("Order not found.");
        var currentStatus = order.Status;

        if (!AllowedChanges.TryGetValue(currentStatus, out var allowed) || !allowed.Contains(newStatus))
        {
            throw new ConflictException($"An order can't go from {currentStatus} to {newStatus}.");
        }

        // One transaction, so a cancel changes the status and returns the stock together or not at all.
        // (For the other moves it only wraps the single status UPDATE, which keeps one code path.)
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Conditional on the status read above: if the webhook marked the order Paid, or another admin changed it,
            // since we loaded it, 0 rows change and we stop. Without this, an admin's cancel could race a payment and
            // return the stock of an order that was just paid for.
            if (!await unitOfWork.Orders.TryChangeStatusAsync(order.Id, currentStatus, newStatus, cancellationToken))
            {
                throw new ConflictException("The order was changed by another request. Reload it and try again.");
            }

            if (newStatus == OrderStatus.Cancelled)
            {
                // Same product-ID order as checkout and the customer's cancel, so they can't deadlock each other.
                foreach (var item in order.Items.OrderBy(i => i.ProductId))
                {
                    await unitOfWork.Products.IncreaseStockAsync(item.ProductId, item.Quantity, cancellationToken);
                }
            }

            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }

        if (newStatus == OrderStatus.Cancelled)
        {
            // Stock went back up, so the cached product pages show too little. Same as OrderService.
            foreach (var item in order.Items)
            {
                cache.Remove(CacheKeys.Product(item.ProductId));
            }
        }

        // Read back fresh (ExecuteUpdate bypasses the tracked order). It also has the customer's name and email.
        var updated = await GetOrderAsync(orderId, cancellationToken);

        // After the commit, for the same reasons as the other order emails. SendAsync never throws.
        await emailService.SendAsync(
            EmailTemplates.OrderStatusChanged(updated.CustomerEmail, updated.CustomerName, updated.OrderNumber, updated.Status),
            CancellationToken.None);

        return updated;
    }
}

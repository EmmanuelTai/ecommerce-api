using ECommerce.Core.DTOs.Admin;
using ECommerce.Core.Enums;
using ECommerce.Core.Interfaces;

namespace ECommerce.Core.Services;

// Not cached: only admins call it, rarely, and they expect up-to-date numbers.
public class AdminDashboardService(IUnitOfWork unitOfWork) : IAdminDashboardService
{
    private const int LowStockThreshold = 5;
    private const int LowStockCount = 10;
    private const int LatestOrderCount = 5;

    // Money actually received: Pending isn't paid yet, and Cancelled orders were never paid.
    private static readonly OrderStatus[] RevenueStatuses =
        [OrderStatus.Paid, OrderStatus.Processing, OrderStatus.Shipped, OrderStatus.Delivered];

    public async Task<DashboardDto> GetAsync(CancellationToken cancellationToken = default)
    {
        // One query after another, not Task.WhenAll: they all share the request's DbContext,
        // and a DbContext can only run one query at a time.
        var counts = await unitOfWork.Orders.GetCountsByStatusAsync(cancellationToken);
        var revenue = await unitOfWork.Orders.GetTotalAmountAsync(RevenueStatuses, cancellationToken);
        var lowStock = await unitOfWork.Products.GetLowStockAsync(LowStockThreshold, LowStockCount, cancellationToken);
        var latest = await unitOfWork.Orders.GetAdminSummaryDtosAsync(
            new AdminOrderQuery { Page = 1, PageSize = LatestOrderCount }, cancellationToken);

        // Every status appears, with 0 if there are no orders in it, so the client always gets the same keys.
        var ordersByStatus = Enum.GetValues<OrderStatus>()
            .ToDictionary(status => status, status => counts.GetValueOrDefault(status));

        return new DashboardDto(ordersByStatus, revenue, lowStock, latest.Items);
    }
}

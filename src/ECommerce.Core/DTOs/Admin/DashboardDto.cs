using ECommerce.Core.Enums;

namespace ECommerce.Core.DTOs.Admin;

public record DashboardDto(
    IReadOnlyDictionary<OrderStatus, int> OrdersByStatus,
    decimal TotalRevenue,
    IReadOnlyList<LowStockProductDto> LowStockProducts,
    IReadOnlyList<AdminOrderSummaryDto> LatestOrders);

public record LowStockProductDto(int Id, string Name, int StockQuantity);

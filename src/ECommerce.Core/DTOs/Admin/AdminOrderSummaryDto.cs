using ECommerce.Core.Enums;

namespace ECommerce.Core.DTOs.Admin;

// One row in the admin order list: like the customer's summary, plus who placed it.
public record AdminOrderSummaryDto(
    int Id,
    string OrderNumber,
    OrderStatus Status,
    decimal TotalAmount,
    int ItemCount,
    int CustomerId,
    string CustomerName,
    string CustomerEmail,
    DateTime CreatedAt);

using ECommerce.Core.Enums;

namespace ECommerce.Core.DTOs.Orders;

// One row in "my orders": enough to list them without loading every line.
public record OrderSummaryDto(int Id, string OrderNumber, OrderStatus Status, decimal TotalAmount, int ItemCount, DateTime CreatedAt);

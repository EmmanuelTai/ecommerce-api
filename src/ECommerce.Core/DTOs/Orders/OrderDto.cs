using ECommerce.Core.Enums;

namespace ECommerce.Core.DTOs.Orders;

public record OrderDto(
    int Id,
    string OrderNumber,
    OrderStatus Status,
    decimal TotalAmount,
    string ShippingFullName,
    string ShippingPhone,
    string ShippingStreet,
    string ShippingCity,
    string ShippingState,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<OrderItemDto> Items);

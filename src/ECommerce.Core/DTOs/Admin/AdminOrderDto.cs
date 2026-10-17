using ECommerce.Core.DTOs.Orders;
using ECommerce.Core.Enums;

namespace ECommerce.Core.DTOs.Admin;

// The full order as the admin sees it: the customer's details plus everything in OrderDto.
public record AdminOrderDto(
    int Id,
    string OrderNumber,
    OrderStatus Status,
    decimal TotalAmount,
    int CustomerId,
    string CustomerName,
    string CustomerEmail,
    string ShippingFullName,
    string ShippingPhone,
    string ShippingStreet,
    string ShippingCity,
    string ShippingState,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<OrderItemDto> Items);

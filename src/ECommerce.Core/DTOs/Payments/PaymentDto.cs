using ECommerce.Core.Enums;

namespace ECommerce.Core.DTOs.Payments;

public record PaymentDto(
    string Reference,
    PaymentStatus Status,
    decimal Amount,
    DateTime? PaidAt,
    int OrderId,
    string OrderNumber,
    OrderStatus OrderStatus);

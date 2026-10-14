using System.ComponentModel.DataAnnotations;

namespace ECommerce.Core.DTOs.Orders;

// Only the delivery details. Items, prices and the total come from the cart and the database,
// and the user from the token, so the client can't change what they pay or who the order belongs to.
public class CreateOrderRequest
{
    [Required, StringLength(100)]
    public string ShippingFullName { get; init; } = string.Empty;

    [Required, StringLength(20)]
    public string ShippingPhone { get; init; } = string.Empty;

    [Required, StringLength(200)]
    public string Street { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string City { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string State { get; init; } = string.Empty;
}

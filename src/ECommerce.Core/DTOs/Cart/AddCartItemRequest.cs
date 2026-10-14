using System.ComponentModel.DataAnnotations;

namespace ECommerce.Core.DTOs.Cart;

// Only the product and quantity. The price always comes from the database, never from the client.
public class AddCartItemRequest
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; init; }

    // Upper bound is a sanity cap; it also keeps "quantity in cart + this quantity" well clear of int overflow.
    [Range(1, 1000)]
    public int Quantity { get; init; }
}

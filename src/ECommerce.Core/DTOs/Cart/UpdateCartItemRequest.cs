using System.ComponentModel.DataAnnotations;

namespace ECommerce.Core.DTOs.Cart;

public class UpdateCartItemRequest
{
    // The new quantity, not a change. 0 removes the line.
    [Range(0, 1000)]
    public int Quantity { get; init; }
}

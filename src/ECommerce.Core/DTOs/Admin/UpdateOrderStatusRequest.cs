using System.ComponentModel.DataAnnotations;
using ECommerce.Core.Enums;

namespace ECommerce.Core.DTOs.Admin;

// The only place the client may send an order status, and only an admin can reach it.
// The service still checks that the move is allowed (Paid → Processing, not Delivered → Pending).
public class UpdateOrderStatusRequest
{
    // Nullable + [Required]: a missing "status" is a 400, instead of silently becoming Pending (the enum's 0).
    [Required]
    public OrderStatus? Status { get; init; }
}

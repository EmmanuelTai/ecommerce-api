namespace ECommerce.Core.DTOs.Cart;

// One cart line with the product's current price (not a snapshot: prices are only frozen when an order is placed).
// IsAvailable is false when the product was deactivated or no longer has enough stock for this quantity,
// so the client can flag the line before checkout fails on it.
public record CartItemDto(
    int Id,
    int ProductId,
    string ProductName,
    string? ImageUrl,
    decimal UnitPrice,
    int Quantity,
    int StockQuantity,
    bool IsAvailable)
{
    // Worked out in C#, not SQL: SQL Server widens decimal(18,2) × int to four decimal places (50000.0000).
    public decimal Subtotal => UnitPrice * Quantity;
}

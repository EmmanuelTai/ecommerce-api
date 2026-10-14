namespace ECommerce.Core.DTOs.Orders;

// Snapshot values stored on the order line, not the product's current name and price.
public record OrderItemDto(int ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal Subtotal);

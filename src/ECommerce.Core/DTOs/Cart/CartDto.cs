namespace ECommerce.Core.DTOs.Cart;

// Total only counts available lines: those are the ones the customer can actually check out.
public record CartDto(IReadOnlyList<CartItemDto> Items, decimal Total);

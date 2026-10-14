namespace ECommerce.Core.DTOs.Catalog;

// What customers see. No IsActive (they only ever see active products) and no UpdatedAt.
public record ProductDto(
    int Id,
    string Name,
    string Slug,
    string Description,
    decimal Price,
    int StockQuantity,
    string? ImageUrl,
    int CategoryId,
    string CategoryName,
    DateTime CreatedAt);

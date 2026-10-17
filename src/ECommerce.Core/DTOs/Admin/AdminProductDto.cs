namespace ECommerce.Core.DTOs.Admin;

// The admin view of a product: unlike ProductDto it includes IsActive and UpdatedAt, because admins see inactive ones too.
public record AdminProductDto(
    int Id,
    string Name,
    string Slug,
    string Description,
    decimal Price,
    int StockQuantity,
    string? ImageUrl,
    int CategoryId,
    string CategoryName,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

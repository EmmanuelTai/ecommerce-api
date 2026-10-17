using System.ComponentModel.DataAnnotations;

namespace ECommerce.Core.DTOs.Admin;

// Used for both create (POST) and full update (PUT). No slug (made from the name on the server),
// no ID (from the route) and no dates (set in SaveChangesAsync).
public class SaveProductRequest
{
    [Range(1, int.MaxValue)]
    public int CategoryId { get; init; }

    [Required, StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(2000)]
    public string Description { get; init; } = string.Empty;

    // Matches the CK_Products_Price check constraint, so a bad price is a 400 here rather than a database error.
    [Range(typeof(decimal), "0.01", "100000000")]
    public decimal Price { get; init; }

    [Range(0, 1_000_000)]
    public int StockQuantity { get; init; }

    [StringLength(500), Url]
    public string? ImageUrl { get; init; }

    // Lets PUT reactivate a soft-deleted product. New products are active unless the admin says otherwise.
    public bool IsActive { get; init; } = true;
}

using System.ComponentModel.DataAnnotations;

namespace ECommerce.Core.DTOs.Catalog;

// Bound from the query string: /api/products?page=2&pageSize=20&search=shoe&sortBy=price_asc.
// [ApiController] validates these before the action runs, so invalid values get our standard 400.
public class ProductQuery
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    // Capped so one request can't ask for the whole table.
    [Range(1, 50)]
    public int PageSize { get; set; } = 10;

    [MaxLength(100)]
    public string? Search { get; set; }

    public int? CategoryId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MinPrice { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MaxPrice { get; set; }

    [AllowedValues(SortNewest, SortPriceAsc, SortPriceDesc,
        ErrorMessage = "SortBy must be one of: newest, price_asc, price_desc.")]
    public string SortBy { get; set; } = SortNewest;

    public const string SortNewest = "newest";
    public const string SortPriceAsc = "price_asc";
    public const string SortPriceDesc = "price_desc";
}

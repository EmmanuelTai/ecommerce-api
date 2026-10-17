using System.ComponentModel.DataAnnotations;

namespace ECommerce.Core.DTOs.Admin;

// /api/admin/products?page=1&pageSize=20&search=shoe&categoryId=2&isActive=false
// isActive left out = active and inactive together.
public class AdminProductQuery
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 50)]
    public int PageSize { get; set; } = 10;

    [MaxLength(100)]
    public string? Search { get; set; }

    public int? CategoryId { get; set; }

    public bool? IsActive { get; set; }
}

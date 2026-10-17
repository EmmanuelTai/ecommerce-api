using System.ComponentModel.DataAnnotations;

namespace ECommerce.Core.DTOs.Admin;

// /api/admin/customers?page=1&pageSize=20&search=ada (matches name or email).
public class CustomerQuery
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 50)]
    public int PageSize { get; set; } = 10;

    [MaxLength(100)]
    public string? Search { get; set; }
}

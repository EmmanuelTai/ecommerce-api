using System.ComponentModel.DataAnnotations;

namespace ECommerce.Core.DTOs.Orders;

// Bound from the query string: /api/orders?page=2&pageSize=10.
public class OrderQuery
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 50)]
    public int PageSize { get; set; } = 10;
}

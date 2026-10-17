using System.ComponentModel.DataAnnotations;
using ECommerce.Core.Enums;

namespace ECommerce.Core.DTOs.Admin;

// /api/admin/orders?status=Paid&page=1&pageSize=20. Status left out = all orders.
public class AdminOrderQuery
{
    public OrderStatus? Status { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 50)]
    public int PageSize { get; set; } = 10;
}

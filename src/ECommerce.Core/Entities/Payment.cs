using ECommerce.Core.Enums;

namespace ECommerce.Core.Entities;

public class Payment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTime? PaidAt { get; set; }

    public Order Order { get; set; } = null!;
}

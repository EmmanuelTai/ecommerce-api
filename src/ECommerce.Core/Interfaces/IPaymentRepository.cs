using ECommerce.Core.DTOs.Payments;
using ECommerce.Core.Entities;
using ECommerce.Core.Enums;

namespace ECommerce.Core.Interfaces;

public interface IPaymentRepository : IGenericRepository<Payment>
{
    // With its Order loaded, for the owner check and the order status change.
    Task<Payment?> GetByReferenceWithOrderAsync(string reference, CancellationToken cancellationToken = default);

    // Read-only: null if the payment doesn't exist or belongs to someone else.
    Task<PaymentDto?> GetDtoByReferenceForUserAsync(string reference, int userId, CancellationToken cancellationToken = default);

    // Changes the status only if it is still "from". Returns false if another request changed it first.
    Task<bool> TryChangeStatusAsync(int paymentId, PaymentStatus from, PaymentStatus to, DateTime? paidAt, CancellationToken cancellationToken = default);
}

using ECommerce.Core.DTOs.Payments;
using ECommerce.Core.Entities;
using ECommerce.Core.Enums;
using ECommerce.Core.Interfaces;
using ECommerce.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Repositories;

public class PaymentRepository(AppDbContext db) : GenericRepository<Payment>(db), IPaymentRepository
{
    // Eager loading: the service needs the order's owner, ID and number.
    public Task<Payment?> GetByReferenceWithOrderAsync(string reference, CancellationToken cancellationToken = default) =>
        Db.Payments
            .Include(p => p.Order)
            .FirstOrDefaultAsync(p => p.Reference == reference, cancellationToken);

    // The user ID is part of the WHERE, so someone else's payment looks exactly like a missing one.
    public Task<PaymentDto?> GetDtoByReferenceForUserAsync(string reference, int userId, CancellationToken cancellationToken = default) =>
        Db.Payments
            .AsNoTracking()
            .Where(p => p.Reference == reference && p.Order.UserId == userId)
            .Select(p => new PaymentDto(
                p.Reference, p.Status, p.Amount, p.PaidAt, p.OrderId, p.Order.OrderNumber, p.Order.Status))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> TryChangeStatusAsync(int paymentId, PaymentStatus from, PaymentStatus to, DateTime? paidAt, CancellationToken cancellationToken = default)
    {
        // Check and change in one statement, so two requests confirming the same payment can't both succeed.
        var rows = await Db.Payments
            .Where(p => p.Id == paymentId && p.Status == from)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, to)
                .SetProperty(p => p.PaidAt, paidAt),
                cancellationToken);

        return rows == 1;
    }
}

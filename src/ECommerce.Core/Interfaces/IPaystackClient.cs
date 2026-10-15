using ECommerce.Core.DTOs.Payments;

namespace ECommerce.Core.Interfaces;

// Defined in Core, implemented in Infrastructure (PaystackClient), so services don't know about HTTP,
// kobo or the secret key. Amounts go in and come out in naira as decimal.
public interface IPaystackClient
{
    // Starts a transaction and returns the checkout URL to send the customer to.
    Task<string> InitializeTransactionAsync(string email, decimal amount, string reference, CancellationToken cancellationToken = default);

    // Asks Paystack directly what happened to a transaction. Null if Paystack doesn't know the reference.
    Task<PaystackTransaction?> VerifyTransactionAsync(string reference, CancellationToken cancellationToken = default);

    // True only if the x-paystack-signature header matches the raw request body.
    bool IsValidSignature(byte[] body, string? signature);
}

using ECommerce.Core.DTOs.Payments;

namespace ECommerce.Core.Interfaces;

public interface IPaymentService
{
    // Current user's Pending order only.
    Task<InitializePaymentResponse> InitializeAsync(int orderId, CancellationToken cancellationToken = default);

    // Current user's payment only. Checks with Paystack and confirms the payment if it went through.
    Task<PaymentDto> VerifyAsync(string reference, CancellationToken cancellationToken = default);

    // Called by Paystack. Throws UnauthorizedException if the signature doesn't match the body.
    Task HandleWebhookAsync(byte[] body, string? signature, CancellationToken cancellationToken = default);
}

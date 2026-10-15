using System.Text.Json;
using ECommerce.Core.DTOs.Payments;
using ECommerce.Core.Entities;
using ECommerce.Core.Enums;
using ECommerce.Core.Exceptions;
using ECommerce.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ECommerce.Core.Services;

public class PaymentService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IPaystackClient paystack,
    IEmailService emailService,
    ILogger<PaymentService> logger) : IPaymentService
{
    private const string Currency = "NGN";

    public async Task<InitializePaymentResponse> InitializeAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var order = await unitOfWork.Orders.GetByIdAsync(orderId, cancellationToken);
        if (order is null || order.UserId != currentUser.UserId)
        {
            throw new NotFoundException("Order not found.");
        }

        if (order.Status != OrderStatus.Pending)
        {
            throw new ConflictException("Only pending orders can be paid.");
        }

        // Paystack needs the customer's email for its checkout page and receipt.
        var customer = await unitOfWork.Users.GetDtoByIdAsync(currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        // Our own reference, random so it can't be guessed. A new one per attempt: Paystack rejects a reused reference.
        var reference = $"PAY-{Guid.NewGuid():N}";

        // The amount comes from the order in the database, never from the client.
        // Paystack first, then our database: if Paystack fails, no payment row is left behind. If our save fails
        // instead, the Paystack transaction is never used, because its checkout URL was never returned to anyone.
        var authorizationUrl = await paystack.InitializeTransactionAsync(customer.Email, order.TotalAmount, reference, cancellationToken);

        unitOfWork.Payments.Add(new Payment
        {
            OrderId = order.Id,
            Reference = reference,
            Amount = order.TotalAmount,
            Status = PaymentStatus.Pending
        });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new InitializePaymentResponse(reference, authorizationUrl, order.TotalAmount);
    }

    public async Task<PaymentDto> VerifyAsync(string reference, CancellationToken cancellationToken = default)
    {
        var payment = await unitOfWork.Payments.GetByReferenceWithOrderAsync(reference, cancellationToken);
        if (payment is null || payment.Order.UserId != currentUser.UserId)
        {
            throw new NotFoundException("Payment not found.");
        }

        await ConfirmAsync(payment, cancellationToken);

        // Read back fresh: ConfirmAsync changes statuses with ExecuteUpdate, which the tracked payment doesn't see.
        return await unitOfWork.Payments.GetDtoByReferenceForUserAsync(reference, currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException("Payment not found.");
    }

    public async Task HandleWebhookAsync(byte[] body, string? signature, CancellationToken cancellationToken = default)
    {
        // The endpoint is anonymous, so anyone can post to it. The signature is the only proof the request came
        // from Paystack, and it is checked before the body is even parsed.
        if (!paystack.IsValidSignature(body, signature))
        {
            throw new UnauthorizedException("Invalid webhook signature.");
        }

        string? eventName;
        string? reference;
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            eventName = GetString(root, "event");
            reference = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data)
                ? GetString(data, "reference")
                : null;
        }
        catch (JsonException)
        {
            throw new BadRequestException("The webhook body is not valid JSON.");
        }

        // Paystack sends other events too (transfers, subscriptions, refunds). We only act on successful charges.
        // Returning normally gives 200, which tells Paystack to stop resending the event.
        if (eventName != "charge.success" || string.IsNullOrEmpty(reference))
        {
            logger.LogInformation("Paystack webhook ignored: {Event}", eventName);
            return;
        }

        var payment = await unitOfWork.Payments.GetByReferenceWithOrderAsync(reference, cancellationToken);
        if (payment is null)
        {
            // Genuine (the signature matched) but not ours, e.g. a payment made from the dashboard. 200 so it isn't retried.
            logger.LogWarning("Paystack webhook for unknown payment reference {Reference}", reference);
            return;
        }

        await ConfirmAsync(payment, cancellationToken);
    }

    // Shared by the webhook and the verify endpoint, so it doesn't matter which one arrives first, or how often.
    private async Task ConfirmAsync(Payment payment, CancellationToken cancellationToken)
    {
        // Already settled (a webhook resent by Paystack, or verify called after the webhook): nothing to do.
        if (payment.Status != PaymentStatus.Pending)
        {
            return;
        }

        // Never trust the webhook body or the customer's word: ask Paystack directly, with our secret key,
        // what happened to this reference.
        var transaction = await paystack.VerifyTransactionAsync(payment.Reference, cancellationToken);
        if (transaction is null)
        {
            logger.LogWarning("Paystack has no transaction for payment {Reference}", payment.Reference);
            return;
        }

        if (transaction.Status == "failed")
        {
            await unitOfWork.Payments.TryChangeStatusAsync(payment.Id, PaymentStatus.Pending, PaymentStatus.Failed, null, cancellationToken);
            return;
        }

        // "abandoned", "ongoing", "pending": the customer hasn't finished paying yet. Leave it Pending.
        if (transaction.Status != "success")
        {
            return;
        }

        // A successful charge for the wrong amount (or currency) doesn't pay for this order.
        if (transaction.Amount != payment.Amount || transaction.Currency != Currency)
        {
            logger.LogWarning(
                "Payment {Reference} amount mismatch: expected {Expected} {ExpectedCurrency}, Paystack charged {Actual} {ActualCurrency}",
                payment.Reference, payment.Amount, Currency, transaction.Amount, transaction.Currency);
            await unitOfWork.Payments.TryChangeStatusAsync(payment.Id, PaymentStatus.Pending, PaymentStatus.Failed, null, cancellationToken);
            return;
        }

        // Loaded before the transaction, so nothing after the commit can fail on a database read.
        var customer = await unitOfWork.Users.GetDtoByIdAsync(payment.Order.UserId, cancellationToken);

        // Payment and order change together: never a Successful payment on a Pending order, or the reverse.
        bool orderPaid;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Conditional, like the stock update. The webhook and the verify endpoint can run at the same moment
            // for the same payment: both passed the Pending check above and both heard "success" from Paystack.
            // Only one of them changes the row; the other gets false and stops, so the email goes out once.
            if (!await unitOfWork.Payments.TryChangeStatusAsync(payment.Id, PaymentStatus.Pending, PaymentStatus.Successful,
                    transaction.PaidAt ?? DateTime.UtcNow, cancellationToken))
            {
                await unitOfWork.RollbackAsync(CancellationToken.None);
                return;
            }

            // Also conditional: the customer may have cancelled the order while on Paystack's checkout page.
            orderPaid = await unitOfWork.Orders.TryChangeStatusAsync(payment.OrderId, OrderStatus.Pending, OrderStatus.Paid, cancellationToken);

            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }

        if (!orderPaid)
        {
            // The money really was taken, so the payment stays Successful. But the order was no longer Pending
            // (cancelled, or already paid by another attempt). Refunds are out of scope: log it for a person to handle.
            logger.LogWarning("Payment {Reference} succeeded but order {OrderId} was not Pending. Needs a manual refund.",
                payment.Reference, payment.OrderId);
            return;
        }

        // After the commit, for the same reasons as the order emails. SendAsync never throws.
        if (customer is not null)
        {
            await emailService.SendAsync(
                EmailTemplates.PaymentReceived(customer.Email, customer.FullName, payment.Order.OrderNumber, payment.Amount),
                CancellationToken.None);
        }
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

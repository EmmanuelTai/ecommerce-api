namespace ECommerce.Core.DTOs.Payments;

// What Paystack's Verify Transaction says about a payment. Amount is already converted back from kobo to naira.
// Status is Paystack's own word: "success", "failed", "abandoned", "ongoing", "pending" or "reversed".
public record PaystackTransaction(string Status, decimal Amount, string Currency, DateTime? PaidAt);

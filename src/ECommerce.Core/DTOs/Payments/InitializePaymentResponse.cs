namespace ECommerce.Core.DTOs.Payments;

// The client sends the customer to AuthorizationUrl (Paystack's checkout page) and keeps Reference
// to check the result afterwards with GET /api/payments/verify/{reference}.
public record InitializePaymentResponse(string Reference, string AuthorizationUrl, decimal Amount);

using ECommerce.Core.DTOs.Common;
using ECommerce.Core.DTOs.Payments;
using ECommerce.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Api.Controllers;

// Customers only, except the webhook, which Paystack calls without a token.
[ApiController]
[Route("api/payments")]
[Authorize(Roles = "Customer")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
public class PaymentsController(IPaymentService paymentService) : ControllerBase
{
    // 200 rather than 201: the result the client needs is the checkout URL, not a new resource to fetch.
    [HttpPost("initialize/{orderId:int}")]
    [ProducesResponseType<ApiResponse<InitializePaymentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Initialize(int orderId, CancellationToken cancellationToken)
    {
        var result = await paymentService.InitializeAsync(orderId, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    // Backup for a missed webhook: the client calls this after the customer returns from Paystack.
    [HttpGet("verify/{reference}")]
    [ProducesResponseType<ApiResponse<PaymentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Verify(string reference, CancellationToken cancellationToken)
    {
        var result = await paymentService.VerifyAsync(reference, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    // Anonymous: Paystack has no JWT. The x-paystack-signature header proves the request is from Paystack instead.
    // No [FromBody] DTO on purpose: the signature is computed over the exact bytes Paystack sent, so we read the
    // raw body. Model binding would parse and re-serialise it, and any difference in spacing or key order
    // would break the signature.
    [AllowAnonymous]
    [HttpPost("webhook")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Webhook(
        [FromHeader(Name = "x-paystack-signature")] string? signature, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, cancellationToken);

        await paymentService.HandleWebhookAsync(buffer.ToArray(), signature, cancellationToken);

        // Paystack only looks at the status code: 200 means "received", anything else and it retries later.
        return Ok();
    }
}

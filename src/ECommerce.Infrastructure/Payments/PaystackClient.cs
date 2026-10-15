using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ECommerce.Core.DTOs.Payments;
using ECommerce.Core.Interfaces;
using ECommerce.Core.Settings;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.Payments;

// Typed HttpClient for Paystack. The HttpClient comes from IHttpClientFactory, already set up in
// DependencyInjection.cs with the base URL and "Authorization: Bearer <secret key>".
// The only class that knows about kobo: amounts are decimal naira everywhere else.
public class PaystackClient(HttpClient httpClient, IOptions<PaystackSettings> options) : IPaystackClient
{
    // Paystack's JSON uses snake_case (authorization_url, paid_at). Nulls are left out of requests,
    // so an unset callback_url falls back to the one in the Paystack dashboard.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<string> InitializeTransactionAsync(string email, decimal amount, string reference, CancellationToken cancellationToken = default)
    {
        var request = new InitializeRequest(email, ToKobo(amount), reference, "NGN", options.Value.CallbackUrl);

        using var response = await httpClient.PostAsJsonAsync("transaction/initialize", request, JsonOptions, cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<PaystackResponse<InitializeData>>(JsonOptions, cancellationToken);

        // Paystack's message ("Invalid key", "Duplicate Transaction Reference") goes into the log through the
        // 500 handler; the client only sees the generic error.
        if (!response.IsSuccessStatusCode || body is not { Status: true, Data.AuthorizationUrl: { } authorizationUrl })
        {
            throw new InvalidOperationException(
                $"Paystack initialize failed ({(int)response.StatusCode}): {body?.Message}");
        }

        return authorizationUrl;
    }

    public async Task<PaystackTransaction?> VerifyTransactionAsync(string reference, CancellationToken cancellationToken = default)
    {
        // Escaped because the reference goes into the URL path.
        using var response = await httpClient.GetAsync($"transaction/verify/{Uri.EscapeDataString(reference)}", cancellationToken);

        // Paystack answers an unknown reference with 400 or 404 ("Transaction reference not found").
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
        {
            return null;
        }

        var body = await response.Content.ReadFromJsonAsync<PaystackResponse<VerifyData>>(JsonOptions, cancellationToken);
        if (!response.IsSuccessStatusCode || body is not { Status: true, Data: { } data })
        {
            throw new InvalidOperationException(
                $"Paystack verify failed ({(int)response.StatusCode}): {body?.Message}");
        }

        return new PaystackTransaction(data.Status, FromKobo(data.Amount), data.Currency, data.PaidAt?.UtcDateTime);
    }

    // Paystack signs every webhook: x-paystack-signature = HMAC-SHA512(raw body, secret key), as lowercase hex.
    // We compute the same HMAC over the bytes we received and compare. Only someone holding the secret key
    // can produce a matching signature, and changing even one byte of the body changes the HMAC completely.
    public bool IsValidSignature(byte[] body, string? signature)
    {
        if (string.IsNullOrEmpty(signature))
        {
            return false;
        }

        var expected = HMACSHA512.HashData(Encoding.UTF8.GetBytes(options.Value.SecretKey), body);

        // Compare raw bytes, not hex strings: hex decoding ignores upper/lower case, and a header that isn't
        // valid hex simply fails.
        byte[] received;
        try
        {
            received = Convert.FromHexString(signature);
        }
        catch (FormatException)
        {
            return false;
        }

        // Constant time: an ordinary == stops at the first different byte, so how long it takes would tell an
        // attacker how many leading bytes of their guess were right. FixedTimeEquals always checks every byte.
        return CryptographicOperations.FixedTimeEquals(expected, received);
    }

    // Prices are stored with 2 decimal places, so × 100 is always a whole number of kobo.
    private static long ToKobo(decimal amount) => (long)(amount * 100);

    private static decimal FromKobo(long kobo) => kobo / 100m;

    private record InitializeRequest(string Email, long Amount, string Reference, string Currency, string? CallbackUrl);

    // Every Paystack response has this envelope: { "status": true, "message": "...", "data": { ... } }.
    private record PaystackResponse<T>(bool Status, string? Message, T? Data);

    private record InitializeData(string? AuthorizationUrl, string? AccessCode, string? Reference);

    private record VerifyData(string Status, string Reference, long Amount, string Currency, DateTimeOffset? PaidAt);
}

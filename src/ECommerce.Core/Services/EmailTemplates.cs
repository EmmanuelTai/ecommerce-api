using System.Globalization;
using System.Net;
using System.Text;
using ECommerce.Core.DTOs.Email;
using ECommerce.Core.DTOs.Orders;
using ECommerce.Core.Enums;

namespace ECommerce.Core.Services;

// The text of every email in one place. Plain string building: four short emails don't need a template engine.
// Every value that came from a user (names, addresses) goes through Encode, so a name like "<script>"
// shows up as text instead of becoming HTML in the customer's inbox.
public static class EmailTemplates
{
    private const string StoreName = "ECommerce Store";

    public static EmailMessage Welcome(string toEmail, string fullName) =>
        new(toEmail, fullName, $"Welcome to {StoreName}",
            Layout($"""
                <p>Hi {Encode(fullName)},</p>
                <p>Your account has been created. You can now browse products, fill your cart and place orders.</p>
                """));

    public static EmailMessage OrderPlaced(string toEmail, string fullName, OrderDto order)
    {
        var rows = new StringBuilder();
        foreach (var item in order.Items)
        {
            rows.Append($"""
                <tr><td>{Encode(item.ProductName)}</td><td align="right">{item.Quantity}</td>
                <td align="right">{Money(item.UnitPrice)}</td><td align="right">{Money(item.Subtotal)}</td></tr>
                """);
        }

        return new(toEmail, fullName, $"Order {order.OrderNumber} received",
            Layout($"""
                <p>Hi {Encode(fullName)},</p>
                <p>Thanks for your order <strong>{order.OrderNumber}</strong>, placed {Date(order.CreatedAt)}.
                It is waiting for payment; we'll email you again once the payment is received.</p>
                <table cellpadding="6" cellspacing="0" border="1" style="border-collapse:collapse">
                  <tr><th align="left">Product</th><th>Qty</th><th>Unit price</th><th>Subtotal</th></tr>
                  {rows}
                  <tr><td colspan="3" align="right"><strong>Total</strong></td><td align="right"><strong>{Money(order.TotalAmount)}</strong></td></tr>
                </table>
                <p><strong>Shipping to:</strong><br>
                {Encode(order.ShippingFullName)}, {Encode(order.ShippingPhone)}<br>
                {Encode(order.ShippingStreet)}, {Encode(order.ShippingCity)}, {Encode(order.ShippingState)}</p>
                """));
    }

    public static EmailMessage PaymentReceived(string toEmail, string fullName, string orderNumber, decimal amount) =>
        new(toEmail, fullName, $"Payment received for order {orderNumber}",
            Layout($"""
                <p>Hi {Encode(fullName)},</p>
                <p>We've received your payment of <strong>{Money(amount)}</strong> for order <strong>{orderNumber}</strong>.
                We'll let you know when it ships.</p>
                """));

    public static EmailMessage OrderStatusChanged(string toEmail, string fullName, string orderNumber, OrderStatus status) =>
        new(toEmail, fullName, $"Order {orderNumber} is now {status}",
            Layout($"""
                <p>Hi {Encode(fullName)},</p>
                <p>The status of your order <strong>{orderNumber}</strong> has changed to <strong>{status}</strong>.</p>
                """));

    private static string Layout(string content) => $"""
        <div style="font-family:Arial,sans-serif;font-size:14px;color:#222">
          <h2>{StoreName}</h2>
          {content}
          <p style="color:#888;font-size:12px">This is an automated message. Please don't reply.</p>
        </div>
        """;

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    // Invariant culture so the format doesn't depend on the server's regional settings: ₦12,500.00
    private static string Money(decimal amount) => "₦" + amount.ToString("N2", CultureInfo.InvariantCulture);

    private static string Date(DateTime utc) => utc.ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture) + " UTC";
}

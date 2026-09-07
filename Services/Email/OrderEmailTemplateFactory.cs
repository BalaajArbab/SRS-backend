using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using SRS_backend.Configuration;
using SRS_backend.Models;

namespace SRS_backend.Services.Email;

public sealed class OrderEmailTemplateFactory(IOptions<EmailOptions> emailOptions)
{
    private readonly EmailOptions _emailOptions = emailOptions.Value;

    public EmailDeliveryMessage Create(Order order, OrderEmailNotification notification)
    {
        var (subject, intro, paymentMessage) = notification.Kind switch
        {
            OrderEmailNotificationKind.OrderPlacedAdmin =>
                ($"New order {order.OrderNumber}", "A new order has been placed.", null),
            OrderEmailNotificationKind.OrderPlacedCustomer =>
                ($"We've received your order {order.OrderNumber}",
                    $"Hi {order.Customer.Name}, we've received your order.",
                    GetPaymentMessage(order)),
            OrderEmailNotificationKind.OrderShippedCustomer =>
                ($"Your order {order.OrderNumber} has shipped",
                    $"Hi {order.Customer.Name}, your order has been shipped.", null),
            OrderEmailNotificationKind.OrderDeliveredCustomer =>
                ($"Your order {order.OrderNumber} was delivered",
                    $"Hi {order.Customer.Name}, your order has been marked as delivered.", null),
            OrderEmailNotificationKind.OrderCancelledCustomer =>
                ($"Your order {order.OrderNumber} was cancelled",
                    $"Hi {order.Customer.Name}, your order has been cancelled.", null),
            _ => throw new InvalidOperationException("The order email notification kind is not supported.")
        };

        var includeCustomerDetails = notification.Kind == OrderEmailNotificationKind.OrderPlacedAdmin;
        var plainText = BuildPlainText(order, intro, paymentMessage, includeCustomerDetails);
        var html = BuildHtml(order, intro, paymentMessage, includeCustomerDetails);
        return new EmailDeliveryMessage(
            Guid.ParseExact(notification.Id, "N"),
            notification.RecipientAddress,
            subject,
            plainText,
            html);
    }

    private string? GetPaymentMessage(Order order) => order.PaymentMethod switch
    {
        OrderPaymentMethod.CashOnDelivery =>
            $"Payment of {FormatMoney(order.Total)} is due when your order is delivered.",
        OrderPaymentMethod.BankTransfer => string.IsNullOrWhiteSpace(_emailOptions.BankTransferInstructions)
            ? "We will confirm your bank transfer before your order moves to fulfilment."
            : $"We will confirm your bank transfer before your order moves to fulfilment. {_emailOptions.BankTransferInstructions.Trim()}",
        _ => null
    };

    private static string BuildPlainText(Order order, string intro, string? paymentMessage, bool includeCustomerDetails)
    {
        var content = new StringBuilder()
            .AppendLine(intro)
            .AppendLine()
            .AppendLine($"Order: {order.OrderNumber}")
            .AppendLine("Items:");

        foreach (var item in order.Items)
        {
            content.AppendLine($"- {item.Name} × {item.Quantity}: {FormatMoney(item.LineTotal)}");
        }

        content.AppendLine()
            .AppendLine($"Subtotal: {FormatMoney(order.Subtotal)}")
            .AppendLine($"Delivery fee: {FormatMoney(order.DeliveryFee)}")
            .AppendLine($"Total: {FormatMoney(order.Total)}");

        if (!string.IsNullOrWhiteSpace(paymentMessage))
        {
            content.AppendLine().AppendLine(paymentMessage);
        }

        if (includeCustomerDetails)
        {
            content.AppendLine()
                .AppendLine($"Payment: {GetPaymentSummary(order)}")
                .AppendLine()
                .AppendLine("Customer:")
                .AppendLine(order.Customer.Name)
                .AppendLine(order.Customer.Email)
                .AppendLine(order.Customer.Phone)
                .AppendLine()
                .AppendLine("Shipping address:")
                .AppendLine(order.ShippingAddress.AddressLine1);

            if (!string.IsNullOrWhiteSpace(order.ShippingAddress.AddressLine2))
            {
                content.AppendLine(order.ShippingAddress.AddressLine2);
            }

            content.AppendLine(order.ShippingAddress.City);
            if (!string.IsNullOrWhiteSpace(order.ShippingAddress.PostalCode))
            {
                content.AppendLine(order.ShippingAddress.PostalCode);
            }

            if (!string.IsNullOrWhiteSpace(order.ShippingAddress.Notes))
            {
                content.AppendLine().AppendLine($"Notes: {order.ShippingAddress.Notes}");
            }
        }

        return content.ToString();
    }

    private static string BuildHtml(Order order, string intro, string? paymentMessage, bool includeCustomerDetails)
    {
        var rows = new StringBuilder();
        foreach (var item in order.Items)
        {
            rows.Append("<tr><td style=\"padding:6px 12px 6px 0\">")
                .Append(Encode(item.Name))
                .Append("</td><td style=\"padding:6px 12px;text-align:right\">")
                .Append(item.Quantity)
                .Append("</td><td style=\"padding:6px 0;text-align:right\">")
                .Append(Encode(FormatMoney(item.LineTotal)))
                .Append("</td></tr>");
        }

        var content = new StringBuilder("<html><body style=\"font-family:Arial,sans-serif;line-height:1.5;color:#1f2937\">")
            .Append("<p>").Append(Encode(intro)).Append("</p>")
            .Append("<p><strong>Order: ").Append(Encode(order.OrderNumber)).Append("</strong></p>")
            .Append("<table style=\"border-collapse:collapse;width:100%;max-width:640px\"><thead><tr><th style=\"text-align:left\">Item</th><th style=\"text-align:right\">Qty</th><th style=\"text-align:right\">Total</th></tr></thead><tbody>")
            .Append(rows)
            .Append("</tbody></table>")
            .Append("<p>Subtotal: ").Append(Encode(FormatMoney(order.Subtotal))).Append("<br>")
            .Append("Delivery fee: ").Append(Encode(FormatMoney(order.DeliveryFee))).Append("<br>")
            .Append("<strong>Total: ").Append(Encode(FormatMoney(order.Total))).Append("</strong></p>");

        if (!string.IsNullOrWhiteSpace(paymentMessage))
        {
            content.Append("<p>").Append(Encode(paymentMessage)).Append("</p>");
        }

        if (includeCustomerDetails)
        {
            content.Append("<hr><p><strong>Payment:</strong> ")
                .Append(Encode(GetPaymentSummary(order)))
                .Append("</p><p><strong>Customer</strong><br>")
                .Append(Encode(order.Customer.Name)).Append("<br>")
                .Append(Encode(order.Customer.Email)).Append("<br>")
                .Append(Encode(order.Customer.Phone)).Append("</p><p><strong>Shipping address</strong><br>")
                .Append(Encode(order.ShippingAddress.AddressLine1)).Append("<br>");

            if (!string.IsNullOrWhiteSpace(order.ShippingAddress.AddressLine2))
            {
                content.Append(Encode(order.ShippingAddress.AddressLine2)).Append("<br>");
            }

            content.Append(Encode(order.ShippingAddress.City));
            if (!string.IsNullOrWhiteSpace(order.ShippingAddress.PostalCode))
            {
                content.Append("<br>").Append(Encode(order.ShippingAddress.PostalCode));
            }

            content.Append("</p>");
            if (!string.IsNullOrWhiteSpace(order.ShippingAddress.Notes))
            {
                content.Append("<p><strong>Notes</strong><br>")
                    .Append(Encode(order.ShippingAddress.Notes))
                    .Append("</p>");
            }
        }

        return content.Append("</body></html>").ToString();
    }

    private static string FormatMoney(int amount) => $"PKR {amount.ToString("N0", CultureInfo.InvariantCulture)}";

    private static string GetPaymentSummary(Order order) => order.PaymentMethod switch
    {
        OrderPaymentMethod.CashOnDelivery => "Cash on delivery",
        OrderPaymentMethod.BankTransfer => $"Bank transfer — {order.PaymentStatus}",
        _ => order.PaymentMethod.ToString()
    };

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}

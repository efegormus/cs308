using System.Text.Json.Serialization;
using OnlineStore.Models;

namespace OnlineStore.Purchasing;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CheckoutRequest(long CartId, string? IdempotencyKey, string? QuoteToken, MockCard? Payment);

// Never log or serialize payment input in an error or response.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class MockCard
{
    public string? Number { get; init; }
    public int ExpiryMonth { get; init; }
    public int ExpiryYear { get; init; }
    public string? SecurityCode { get; init; }
    public override string ToString() => "[redacted mock payment input]";
}

public sealed record CheckoutLine(long ProductId, string Name, int Quantity, decimal UnitPrice, decimal Total);
public sealed record CheckoutQuote(long CartId, string QuoteToken, string Currency, string Name, string TaxId,
    string Email, string Address, IReadOnlyList<CheckoutLine> Items, decimal Total);
public sealed record PurchaseReceipt(long OrderId, long? CartId, string Status, string PaymentStatus,
    DateTime? PurchasedAt, DateTime? DeliveredAt, string Currency, string Name, string TaxId, string Address,
    IReadOnlyList<CheckoutLine> Items, decimal Total, string InvoiceStatus, string? InvoiceUrl);

public sealed class PurchaseException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public static class PurchaseViews
{
    public static PurchaseReceipt Receipt(Order order) => new(order.Id, order.SourceCartId,
        order.Status.ToString(), order.PaymentStatus.ToString(), order.PurchasedAt, order.DeliveredAt, "TRY",
        order.CustomerNameSnapshot ?? "", order.TaxIdSnapshot ?? "", order.AddressSnapshot ?? "",
        order.OrderItems.OrderBy(line => line.ProductId).Select(line => new CheckoutLine(line.ProductId,
            line.ProductName, line.Quantity, line.UnitPrice, line.UnitPrice * line.Quantity)).ToList(),
        order.OrderItems.Sum(line => line.UnitPrice * line.Quantity),
        order.Invoice?.ProtectedDocumentKey is null ? "Pending" : "Ready",
        order.Invoice?.ProtectedDocumentKey is null ? null : $"/api/customer/orders/{order.Id}/invoice");
}

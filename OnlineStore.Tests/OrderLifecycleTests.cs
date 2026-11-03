using OnlineStore.Models;

namespace OnlineStore.Tests;

public class OrderLifecycleTests
{
    private static readonly DateTime PurchaseTime = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NewOrder_SeparatesProcessingFromPendingPayment()
    {
        var order = new Order();
        Assert.Equal(OrderStatus.Processing, order.Status);
        Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
        Assert.Null(order.PurchasedAt);
        Assert.False(order.TransitionTo(OrderStatus.InTransit, PurchaseTime));
        Assert.False(order.TransitionTo(OrderStatus.Delivered, PurchaseTime));
    }

    [Fact]
    public void PaymentConfirmation_IsIdempotentAndDoesNotChangePurchaseDate()
    {
        var order = new Order();
        Assert.True(order.ConfirmPayment(PurchaseTime));
        Assert.False(order.ConfirmPayment(PurchaseTime.AddDays(1)));
        Assert.False(order.RecordPaymentFailure());
        Assert.Equal(PurchaseTime, order.PurchasedAt);
        Assert.Equal(PaymentStatus.Confirmed, order.PaymentStatus);
    }

    [Fact]
    public void FailedPayment_CannotShipButCanBeRetried()
    {
        var order = new Order();
        Assert.True(order.RecordPaymentFailure());
        Assert.False(order.TransitionTo(OrderStatus.InTransit, PurchaseTime));
        Assert.Null(order.PurchasedAt);
        Assert.True(order.ConfirmPayment(PurchaseTime));
        Assert.True(order.TransitionTo(OrderStatus.InTransit, PurchaseTime.AddHours(1)));
    }

    [Fact]
    public void Fulfillment_RequiresPaymentAndForwardTransitions()
    {
        var order = new Order();
        order.ConfirmPayment(PurchaseTime);
        Assert.False(order.TransitionTo(OrderStatus.Delivered, PurchaseTime));
        Assert.True(order.TransitionTo(OrderStatus.InTransit, PurchaseTime.AddDays(1)));
        Assert.False(order.TransitionTo(OrderStatus.Cancelled, PurchaseTime.AddDays(1)));
        Assert.False(order.TransitionTo(OrderStatus.Processing, PurchaseTime.AddDays(1)));
        Assert.True(order.TransitionTo(OrderStatus.Delivered, PurchaseTime.AddDays(2)));
        Assert.Equal(PurchaseTime.AddDays(2), order.DeliveredAt);
        Assert.False(order.TransitionTo(OrderStatus.Cancelled, PurchaseTime.AddDays(3)));
        Assert.False(order.TransitionTo(OrderStatus.InTransit, PurchaseTime.AddDays(3)));
    }

    [Fact]
    public void Cancellation_OnlyFromProcessingAndCannotBePaidLater()
    {
        var order = new Order();
        Assert.True(order.TransitionTo(OrderStatus.Cancelled, PurchaseTime));
        Assert.False(order.ConfirmPayment(PurchaseTime));
        Assert.False(order.TransitionTo(OrderStatus.Processing, PurchaseTime));
    }

    [Theory]
    [InlineData(29, true)]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public void ReturnWindow_IsBasedOnPurchaseNotDelivery(int daysAfterPurchase, bool expected)
    {
        var order = new Order();
        order.ConfirmPayment(PurchaseTime);
        order.TransitionTo(OrderStatus.InTransit, PurchaseTime.AddDays(8));
        order.TransitionTo(OrderStatus.Delivered, PurchaseTime.AddDays(10));
        Assert.Equal(expected, order.CanRequestReturn(PurchaseTime.AddDays(daysAfterPurchase)));
    }

    [Fact]
    public void ReturnWindow_RejectsUndeliveredAndPreDeliveryDates()
    {
        var order = new Order();
        order.ConfirmPayment(PurchaseTime);
        Assert.False(order.CanRequestReturn(PurchaseTime.AddDays(1)));
        order.TransitionTo(OrderStatus.InTransit, PurchaseTime.AddDays(2));
        order.TransitionTo(OrderStatus.Delivered, PurchaseTime.AddDays(10));
        Assert.False(order.CanRequestReturn(PurchaseTime.AddDays(9)));
        Assert.False(order.CanRequestReturn(PurchaseTime.AddDays(-1)));
    }

    [Fact]
    public void Timestamps_RejectNonUtcAndFulfillmentBeforePurchase()
    {
        var order = new Order();
        Assert.Throws<ArgumentException>(() => order.ConfirmPayment(DateTime.SpecifyKind(PurchaseTime, DateTimeKind.Unspecified)));
        order.ConfirmPayment(PurchaseTime);
        Assert.False(order.TransitionTo(OrderStatus.InTransit, PurchaseTime.AddMinutes(-1)));
    }

    [Fact]
    public void ItemSnapshot_PreservesPurchaseCostAndPrice()
    {
        var product = new Product { Id = 7, Name = "Phone", Sku = "PHONE", Price = 100, Cost = null };
        var line = OrderItem.Snapshot(product, 2);
        product.Name = "Renamed";
        product.Price = 200;
        product.Cost = 90;
        Assert.Equal("Phone", line.ProductName);
        Assert.Equal(100, line.UnitPrice);
        Assert.Equal(50, line.UnitCost);
        Assert.Equal(2, line.Quantity);
        Assert.Throws<ArgumentOutOfRangeException>(() => OrderItem.Snapshot(product, 0));
    }

    [Fact]
    public void ItemSnapshot_UsesConfirmedDiscountedPriceWithoutMutatingProduct()
    {
        var product = new Product { Name = "Phone", Sku = "PHONE", Price = 100 };
        var discounted = OrderItem.Snapshot(product, 1, paidUnitPrice: 80);
        Assert.Equal(80, discounted.UnitPrice);
        Assert.Equal(40, discounted.UnitCost);
        Assert.Equal(100, product.Price);
        product.Cost = 60;
        Assert.Equal(60, OrderItem.Snapshot(product, 1, paidUnitPrice: 80).UnitCost);
        Assert.Throws<ArgumentOutOfRangeException>(() => OrderItem.Snapshot(product, 1, paidUnitPrice: -1));
    }
}

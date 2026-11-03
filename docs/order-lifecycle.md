# Order and payment lifecycle

Course Project.pdf requires processing, in-transit and delivered fulfillment,
cancellation only during processing, and returns of delivered products within
30 days of purchase. Payment and fulfillment are separate concerns.

```mermaid
stateDiagram-v2
    [*] --> Processing
    Processing --> InTransit: payment confirmed
    InTransit --> Delivered: payment confirmed
    Processing --> Cancelled
```

OrderStatus values are Processing, InTransit, Delivered, Cancelled. PaymentStatus
values are Pending, Confirmed, Failed, Refunded and Unknown (legacy reconciliation).
The database checks allowed values and blocks unpaid shipping/delivery. Refunded
is compatible with an already delivered order, not an in-transit shipment.

## Domain operations

- `Order.ConfirmPayment(utcTime)` confirms a pending/failed payment once and
  preserves PurchasedAt on retry. It cannot confirm a cancelled order.
- `Order.RecordPaymentFailure()` leaves fulfillment Processing and does not set
  a purchase date. Confirmation may be retried through the checkout service.
- `Order.TransitionTo(target, utcTime)` allows only the diagram's transitions;
  it rejects unpaid shipments, skipping delivery stages, reversals and changes
  after cancellation/delivery. DeliveredAt is recorded on delivery.
- `Order.CanRequestReturn(utcTime)` requires delivery, confirmed payment, a known
  PurchasedAt, and an elapsed interval of no more than 30 days from purchase.
  Delivery does not restart the clock. Future partial-return services must also
  validate item ownership, delivered/returned quantities and manager approval.
- `OrderItem.Snapshot(product, quantity, paidUnitPrice)` copies name, final paid
  unit price and unit cost. The server supplies the optional discounted price;
  otherwise Product.Price is used. Cost is the explicit product cost, or 50% of
  the paid price by default. The factory does not modify the tracked product.
  Later product/discount changes do not change the purchase snapshot.

All operation timestamps must be UTC. These methods are domain guards; they do
not call a bank, change inventory, create invoices or implement delivery endpoints.
SCRUM-72/73/79 persist the operations with the required transactions and permissions.
Checkout must conditionally decrement stock in the same finalization transaction,
reject price/owner tampering and deduplicate requests. A failed payment/transaction
must leave no permanent stock decrement or confirmed purchase.

Cancellation after payment still needs the application cancellation/refund service
to reverse financial/inventory effects; changing an enum alone is not a refund.

## Migration compatibility

`AlignOrderLifecycle` upgrades the earlier combined status without deleting orders:

| Old status | Fulfillment | Payment |
| --- | --- | --- |
| Pending | Processing | Pending |
| Paid | Processing | Confirmed |
| Shipped | InTransit | Confirmed |
| Completed | Delivered | Confirmed |
| Cancelled | Cancelled | Unknown |

Old cancellation does not reveal whether money was collected, so the migration
does not invent an answer. Old PurchasedAt/DeliveredAt/UnitCost remain null when
the old schema did not store them. Reconcile these from trustworthy records
before processing time-sensitive returns or claiming historical cost accuracy;
do not substitute the delivery date, CreatedAt or today's product cost.

The rollback maps known legacy fulfillment/payment combinations back to the
old names. New timestamps and cost columns are removed by rollback; retain a
backup if new purchases exist. The migration upgrade/rollback/upgrade is tested
with all five legacy statuses and historical order-item references on MySQL.

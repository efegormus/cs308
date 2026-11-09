> SCRUM-119 storage update: the current queue is `EmailOutbox`, linked to an issued
> invoice. Status is Pending/Sending/Sent/Failed; EmailedAt is derived from SentAt.
> The new baseline supersedes the older migration instructions below. See
> [current mapping and retry flow](db-redesign/migration-notes.md).

# Invoices (SCRUM-74)

Every paid order gets exactly one numbered PDF invoice. The code is in `OnlineStore/Invoicing/`.

## How an invoice is issued

1. `POST /api/customer/checkout` commits the purchase (payment, stock, order) first.
2. It then calls `InvoiceService.EnsureAsync`, which adds one `Invoices` row and writes the PDF.
3. If step 2 fails, the purchase stays committed and the receipt says `invoiceStatus: "Pending"`.
   The next `GET /api/customer/orders/{id}` or invoice download tries again.

`EnsureAsync` is safe to call any number of times, also at the same time from several requests:
the unique index on `Invoices.OrderId` lets only one row in, and the others reuse it. It never
changes stock, payment or order status.

## What is printed

Only purchase-time data: the invoice number (`INV-<purchase year>-<order id, 6 digits>`), order
number, purchase and issue dates, the customer name, tax ID and address snapshots, every line with
the quantity and the unit price actually paid (discounts included), and the total in TRY. Later
changes to the customer profile or to product prices never change an issued invoice.

## Storage

PDFs are files named `<invoice number>.pdf`, kept outside `wwwroot` so the static file middleware
can never serve them. The folder is `App_Data/invoices` under the project by default (ignored by
Git); set `Invoices:StoragePath` to use another one. If a file is lost, it is rebuilt from the
stored snapshots, so its content stays the same. The storage key is never sent to the browser.

## Who can download

| Endpoint | Who |
| --- | --- |
| `GET /api/customer/orders/{id}/invoice` | The customer who owns the order. Anyone else gets 404. |
| `GET /api/sales/invoices/{orderId}` | Sales managers |
| `GET /api/product-management/invoices/{orderId}` | Product managers (delivery department) |

All answers are `application/pdf` with `Cache-Control: no-store`.

## Library

PDFs are made with [QuestPDF](https://www.questpdf.com/) under its free Community license, which
covers this course project. The license is set in `InvoiceDocument`.

## Tests

`InvoiceTests` (no database) and `InvoiceMySqlTests` (MySQL) cover totals with discounted prices,
snapshot stability, idempotent and concurrent generation, a lost file, storage key checks and who
can download.

## Invoice email delivery (SCRUM-75)

After checkout commits, an idempotent enqueue records one `InvoiceEmailDeliveries` row per
confirmed order. Its immutable recipient is `Order.EmailSnapshot`, never the current profile.
The worker also reconciles confirmed orders missing a row, recovering a crash or outage in the
post-commit enqueue gap. This includes pre-existing confirmed purchases when first enabled,
except invoices whose existing `EmailedAt` already proves successful delivery. Reconciliation
isolates failures per order, and dispatch runs separately even when reconciliation fails.
Invoice storage being unavailable does not prevent enqueueing.

The hosted worker polls every 10 seconds, processing up to 50 deliveries per poll. It atomically
claims a due row with a five-minute database lease and unique ownership token, then holds a row
lock during the bounded send operation. Competing workers cannot send the same delivery
simultaneously, even if a slow operation outlives the lease. A crashed worker's lease expires;
the next poll recovers it. Apply the `AddInvoiceEmailDeliveries` migration before starting workers.

The worker obtains the attachment through `InvoiceService.GetPdfAsync`, preserving its filename
and bytes. It sends with stable `Message-ID: <invoice-order-{OrderId}@onlinestore.invalid>`.
Success atomically records delivery `SentAt` and invoice `EmailedAt`; successfully recorded rows
are never sent again. Failures persist attempt count, last attempt, a safe error category and
next attempt, releasing the lease. Backoff is 1, 2, 4, 8, 16, 32, then 60 minutes, capped at
60 minutes for subsequent transient failures. SMTP 4xx responses, network failures, document
I/O failures and send timeouts retry. Permanent recipient rejection, SMTP authentication or
configuration failures and unexpected non-retryable errors set `StoppedAt` and retain a safe
`LastErrorCode`; stopped rows cannot be claimed. Shutdown cancellation leaves a recoverable
lease without recording an ordinary failure. No exception text,
recipient, credentials, content or PDF bytes are logged by delivery code.

SMTP cannot offer exactly-once delivery: a crash or connection loss after the server accepts
mail but before success is recorded can cause a retry. Stable Message-ID helps downstream
deduplication but does not guarantee it. Recorded success is never automatically reset.
Email retries never run checkout, charge payment, or update stock/order state.

### Configuration and local sink

Development defaults to `Email:Transport=Local`. The singleton `LocalInvoiceEmailSender.Messages`
retains only the newest 20 messages in memory for local inspection/tests, evicting the oldest.
It copies each PDF once on ingestion and does not copy PDF bytes on inspection, requires no credentials, and
is cleared on process restart. There is no public mailbox endpoint. Local delivery is recorded
as successful, so use disposable development data when testing. Production defaults to `Smtp`.
An explicit `Email:Transport=Local` can also be used for isolated tests.

Set SMTP values with environment variables (shown below) or .NET User Secrets. Never put
credentials in committed settings files. The built-in .NET SMTP client is used.

| Environment variable | Meaning/default |
| --- | --- |
| `Email__Transport` | `Local` or `Smtp` |
| `Email__From` | Required SMTP sender mailbox |
| `Email__Smtp__Host` | Required SMTP server |
| `Email__Smtp__Port` | 587 |
| `Email__Smtp__EnableSsl` | true (STARTTLS) |
| `Email__Smtp__Username` | Optional username for authenticated SMTP |
| `Email__Smtp__Password` | Password from environment/User Secrets |

For example, `dotnet user-secrets set "Email:Smtp:Host" "smtp.example.com" --project OnlineStore`.
Configure sender and authentication through the same mechanism. Missing or invalid SMTP
settings stop email delivery for manual correction without failing purchases. Sends have a one-minute cancellation
timeout; shutdown leaves a recoverable lease when processing cannot complete.

Apply both `AddInvoiceEmailDeliveries` and the additive `AddInvoiceEmailStoppedState` migration.
Operators can distinguish `Sent` (`SentAt` set), `Stopped` (`StoppedAt` set), `Retrying`
(safe error code set, no terminal timestamp), and `Pending` (neither terminal timestamp nor error).
`LeaseUntil` identifies an active claim. After correcting the underlying problem of a stopped delivery,
an operator may explicitly clear `StoppedAt` and `LastErrorCode` and set `NextAttemptAt` to UTC now,
only where `SentAt IS NULL` and `LeaseUntil IS NULL`. Never reset successful delivery evidence.
There is no public retry/reset endpoint.

`InvoiceEmailTests` covers the credential-free local sink, snapshot enqueue and capped backoff.
`InvoiceEmailMySqlTests` uses the existing guarded local disposable `*_tests` fixture for real
atomic claims, concurrency, retries, crash recovery, PDF identity and purchase isolation.

> SCRUM-119 storage update: the queue is the `EmailOutbox` table, linked to an issued
> invoice. Status is Pending/Sending/Sent/Failed; EmailedAt is derived from SentAt.
> See [current mapping and retry flow](db-redesign/migration-notes.md).

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

After checkout commits, an idempotent enqueue records one `EmailOutbox` row per
confirmed order. Its immutable recipient is `Order.EmailSnapshot`, never the current profile.
The worker also reconciles confirmed orders missing a row, recovering a crash or outage in the
post-commit enqueue gap. This includes pre-existing confirmed purchases when first enabled,
except invoices that already have a successfully sent row (`Status = 'Sent'`, `SentAt` set). Reconciliation
isolates failures per order, and dispatch runs separately even when reconciliation fails.
Invoice storage being unavailable does not prevent enqueueing.

The hosted worker polls every 10 seconds, processing up to 50 deliveries per poll. It atomically
claims a due row with a five-minute database lease and unique ownership token, then holds a row
lock during the bounded send operation. Competing workers cannot send the same delivery
simultaneously, even if a slow operation outlives the lease. A crashed worker's lease expires;
the next poll recovers it. Apply the `RedesignedStore` baseline migration before starting workers.

The worker obtains the attachment through `InvoiceService.GetPdfAsync`, preserving its filename
and bytes. It sends with stable `Message-ID: <invoice-order-{OrderId}@onlinestore.invalid>`.
Success atomically sets `Status = 'Sent'` and `SentAt` (the invoice's `EmailedAt` is derived from it); successfully recorded rows
are never sent again. Failures persist attempt count, last attempt, a safe error category and
next attempt, releasing the lease. Backoff is 1, 2, 4, 8, 16, 32, then 60 minutes, capped at
60 minutes for subsequent transient failures. SMTP 4xx responses, network failures, document
I/O failures and send timeouts retry. Permanent recipient rejection, SMTP authentication or
configuration failures and unexpected non-retryable errors set `Status = 'Failed'` and retain a safe
`LastError` code; failed rows cannot be claimed. Shutdown cancellation leaves a recoverable
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

Operators can distinguish `Sent` (`SentAt` set), `Failed` (stopped, `LastError` holds the safe
error code), `Pending` (waiting for its `NextAttemptAt`, possibly after a retryable error kept in
`LastError`) and `Sending` (claimed by a worker). `LeaseExpiresAt` identifies an active claim; an
expired lease on a `Sending` row is reclaimed by the next poll. After correcting the underlying
problem of a `Failed` delivery, an operator may explicitly set `Status = 'Pending'`, clear
`LastError` and set `NextAttemptAt` to UTC now, only where `Status = 'Failed'`, `SentAt IS NULL` and
`LeaseExpiresAt IS NULL`. `AttemptCount` is kept and only affects the next backoff. Never reset
successful delivery evidence. There is no public retry/reset endpoint.

`InvoiceEmailTests` covers the credential-free local sink, snapshot enqueue and capped backoff.
`InvoiceEmailMySqlTests` uses the existing guarded local disposable `*_tests` fixture for real
atomic claims, concurrency, retries, crash recovery, PDF identity and purchase isolation.

## Invoice displayed on screen (SCRUM-145)

The React order confirmation includes the full invoice, rather than only a PDF
link: invoice number, issue/purchase dates, customer/tax/address snapshots, paid
line prices, quantities and TRY total. `GET /api/customer/orders/{id}/invoice/details`
returns the same `InvoiceData` used by the PDF renderer, with `Cache-Control: no-store`.
It requires Customer and order ownership; another customer gets 404, staff 403,
and anonymous callers 401. Profile/product edits cannot alter these snapshots.
Print and protected PDF download actions remain available.

## Private Gmail SMTP setup

Use a sender account with two-step verification and an available Google app
password. Managed school accounts may restrict this; a personal Gmail account
can be the sender while the buyer's school address receives the invoice. Google
instructions: [app passwords](https://support.google.com/mail/answer/185833?hl=en).
Never paste either the ordinary account password or app password into chat/Jira.

```sh
python3 scripts/configure_smtp.py --sender sender@gmail.com --recipient buyer@example.com
```

This prompts invisibly, verifies STARTTLS/login against `smtp.gmail.com:587`,
then atomically saves the working settings in ignored `.local/smtp.json` (0600
on Unix). Both `scripts/dev.py run` and `scripts/redesign_db.py run` load it;
explicit process variables take precedence. `ONLINESTORE_SMTP_FILE` selects a
private file outside the current checkout. Direct `dotnet` launches still need
User Secrets or SMTP environment settings. The `--recipient` value is a setup
note only: actual invoices go to the purchasing account's immutable email
snapshot. Register/sign in with the intended receiving address before purchasing.
Do not change an existing order's recipient or reset a Sent outbox row to test
SMTP; create a new controlled purchase.

CI uses pinned Mailpit v1.34.0 as an isolated SMTP inbox, without real credentials.
The purchasing browser verifier polls that inbox and compares the decoded PDF
attachment byte-for-byte with the protected customer download. This proves SMTP
and attachment behavior; delivery to a real Gmail/school inbox additionally
requires successful sender authentication and a new purchase with that recipient.

## Demo: send the invoice email to a real inbox with environment variables (SCRUM-144)

This is the quick way to run the demo without a settings file; `scripts/configure_smtp.py` (see Private Gmail SMTP setup above) is the alternative. Progress Demo requirement: a PDF copy of the invoice is emailed to the customer. With the default
`Email:Transport=Local` nothing leaves the machine, so for the demo switch to SMTP on the demo
computer. Nothing in this section is committed: the credentials only live in the terminal that
starts the app.

1. **Mailbox.** Use a demo Gmail account that is not anyone's main mailbox. Turn on 2-Step
   Verification, then create an app password at <https://myaccount.google.com/apppasswords> (16
   letters; remove the spaces when you use it). Revoke it after the demo.
2. **Settings.** In the same PowerShell window that starts the app (repository root), replace the
   two example values:

   ```powershell
   $env:Email__Transport = "Smtp"
   $env:Email__From = "demo.account@gmail.com"
   $env:Email__Smtp__Host = "smtp.gmail.com"
   $env:Email__Smtp__Port = "587"
   $env:Email__Smtp__EnableSsl = "true"
   $env:Email__Smtp__Username = "demo.account@gmail.com"
   $env:Email__Smtp__Password = Read-Host "App password"
   py scripts/dev.py run
   ```

   `Email__From` must be the same Gmail address as the username, Gmail rejects or rewrites other
   senders. The helper passes the terminal's environment on to the app. In a bash shell use
   `export Email__Transport=Smtp` and so on.
3. **Customer.** The invoice goes to the email address stored on the order at checkout
   (`Order.EmailSnapshot`). The seeded demo accounts use `@onlinestore.invalid` addresses, which can
   never receive mail, so register a new customer with an inbox you can open, add a product to the
   cart and pay with the mock test card.
4. **Check.** The worker polls every 10 seconds, so the email arrives within about a minute: subject
   `Invoice for order <id>` with the invoice PDF attached (look in Spam the first time). Seeded
   orders for `@onlinestore.invalid` recipients cannot be delivered and may end up as `Failed`; that
   is expected and does not affect purchases.
5. **If nothing arrives.** Look at the newest outbox rows. `LastError` is a safe error code, never
   the server's message:

   ```powershell
   $pw = (Get-Content .local\mysql-password -Raw).Trim()
   docker exec -e MYSQL_PWD=$pw cs308-mysql-1 mysql -uonlinestore onlinestore -e "SELECT Id, Status, AttemptCount, LastError, NextAttemptAt, SentAt FROM EmailOutbox ORDER BY Id DESC LIMIT 5;"
   ```

   `Sent` means the SMTP server accepted the mail. `Pending` with a `LastError` is a retry in
   progress. `Failed` usually means a wrong password, a wrong `Email__From` or a refused recipient;
   fix the setting, restart the app and, only for a `Failed` row that was never sent, follow the reset
   steps above.
6. **Back to normal.** Close the terminal (or start the app without the `Email__` variables) to
   return to `Local`. Never put the app password in a committed file, a screenshot or a chat.

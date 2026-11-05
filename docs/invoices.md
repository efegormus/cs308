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

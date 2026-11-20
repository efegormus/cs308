import { useCallback, useEffect } from "react";
import { useSessionExpiry } from "../auth/useSessionExpiry.js";
import { fetchInvoice } from "../lib/invoice-api.js";
import { useRequest } from "../lib/useRequest.js";
import { formatPrice } from "../lib/product-display.js";
import { ErrorState, LoadingState } from "./RequestStates.js";
import Button from "./ui/Button.jsx";
import Table from "./ui/Table.js";

export default function InvoicePanel({ orderId }) {
  const read = useRequest(); useSessionExpiry(read.error);
  const load = useCallback(() => read.run(({ signal }) => fetchInvoice(orderId, { signal })), [orderId, read.run]);
  useEffect(() => { void load(); return () => read.cancel(); }, [load, read.cancel]);
  const invoice = read.status === "success" ? read.data : null;
  return <section id="invoice-details" className="purchase-card" aria-label="Invoice">
    <h2>Invoice</h2>
    {read.status === "loading" && <LoadingState label="Loading your invoice…" />}
    <ErrorState error={read.error} onRetry={load} />
    {invoice && <>
      <p>Invoice number: <strong>{invoice.invoiceNumber}</strong></p>
      <p>Issued: {new Date(invoice.issuedAt).toLocaleString()} · Purchased: {new Date(invoice.purchasedAt).toLocaleString()}</p>
      <p>Bill to: {invoice.customerName}</p><p>Tax ID: {invoice.taxId}</p><p className="snapshot-address">{invoice.address}</p>
      <Table caption={`Invoice ${invoice.invoiceNumber}`} columns={[
        { key: "productName", header: "Product", rowHeader: true }, { key: "quantity", header: "Quantity" },
        { key: "unitPrice", header: "Paid unit price", render: line => formatPrice(line.unitPrice) },
        { key: "total", header: "Total", render: line => formatPrice(line.total) }
      ]} rows={invoice.lines.map((line, index) => ({ ...line, id: index }))} />
      <p className="purchase-total">Invoice total: {formatPrice(invoice.total)}</p>
      <Button onClick={() => window.print()}>Print invoice</Button>{" "}<a href={`/api/customer/orders/${orderId}/invoice`}>Open / download PDF</a>
    </>}
  </section>;
}

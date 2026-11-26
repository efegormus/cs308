import { useCallback, useEffect } from "react";
import { useSessionExpiry } from "../auth/useSessionExpiry.js";
import { fetchInvoice } from "../lib/invoice-api.js";
import { useRequest } from "../lib/useRequest.js";
import { formatPrice } from "../lib/product-display.js";
import { ErrorState, LoadingState } from "./RequestStates.js";
import Button from "./ui/Button.jsx";
import Table from "./ui/Table.js";
import "./ui/Table.css";

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
      <dl className="invoice-meta">
        <div><dt>Invoice number</dt><dd><strong>{invoice.invoiceNumber}</strong></dd></div>
        <div><dt>Issued</dt><dd>{new Date(invoice.issuedAt).toLocaleString()}</dd></div>
        <div><dt>Purchased</dt><dd>{new Date(invoice.purchasedAt).toLocaleString()}</dd></div>
        <div><dt>Bill to</dt><dd>{invoice.customerName}</dd></div>
        <div><dt>Tax ID</dt><dd>{invoice.taxId}</dd></div>
        <div className="wide"><dt>Address</dt><dd className="snapshot-address">{invoice.address}</dd></div>
      </dl>
      <Table caption={`Invoice ${invoice.invoiceNumber}`} columns={[
        { key: "productName", header: "Product", rowHeader: true }, { key: "quantity", header: "Quantity", align: "end" },
        { key: "unitPrice", header: "Paid unit price", align: "end", render: line => formatPrice(line.unitPrice) },
        { key: "total", header: "Total", align: "end", render: line => formatPrice(line.total) }
      ]} rows={invoice.lines.map((line, index) => ({ ...line, id: index }))} />
      <p className="purchase-total">Invoice total: {formatPrice(invoice.total)}</p>
      <div className="invoice-actions">
        <Button onClick={() => window.print()}>Print invoice</Button>
        <a href={`/api/customer/orders/${orderId}/invoice`}>Open / download PDF</a>
      </div>
    </>}
  </section>;
}

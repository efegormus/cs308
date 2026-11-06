import { useCallback, useEffect } from "react";
import { Link, useLocation, useSearchParams } from "react-router";
import PurchaseAccess from "../components/PurchaseAccess.jsx";
import PurchaseLines from "../components/PurchaseLines.jsx";
import { ErrorState, LoadingState } from "../components/RequestStates.js";
import { loginHref } from "../lib/auth-utils.js";
import { fetchReceipt } from "../lib/purchase-api.js";
import { formatPrice } from "../lib/product-display.js";
import { useRequest } from "../lib/useRequest.js";
import "../styles/request-states.css";

const date = value => value ? new Date(value).toLocaleString() : "Unknown";
function Receipt({ orderId }) {
  const { status, data, error, run, cancel } = useRequest();
  const location = useLocation();
  const load = useCallback(() => run(({ signal }) => fetchReceipt(orderId, { signal })), [orderId, run]);
  useEffect(() => { void load(); return cancel; }, [load, cancel]);
  return <>
    {(status === "idle" || status === "loading") && <LoadingState label="Loading order…" />}
    <ErrorState error={error} onRetry={load} />
    {error?.status === 401 && <p><Link to={loginHref(location)}>Log in again</Link></p>}
    {data && <section id="receipt" className="purchase-card" aria-label="Purchase receipt">
      <p id="order-status">{data.status} · Payment: {data.paymentStatus} · {date(data.purchasedAt)}</p>
      <p id="order-address" className="snapshot-address">{data.name} · {data.address}</p>
      <PurchaseLines items={data.items} />
      <p className="purchase-total">Total: <span id="total">{formatPrice(data.total)}</span></p>
      {data.invoiceStatus === "Ready"
        ? <a id="invoice" href={`/api/customer/orders/${data.orderId}/invoice`}>Download invoice PDF</a>
        : <p id="invoice-pending">Your invoice is being prepared. Refresh to check its availability.</p>}
    </section>}
    {data && <button id="retry" type="button" disabled={status === "loading"} onClick={load}>Refresh order</button>}
  </>;
}
export default function OrderConfirmationPage() {
  const [params] = useSearchParams();
  const raw = params.get("orderId");
  const id = /^[1-9]\d*$/.test(raw ?? "") && Number(raw) <= 2147483647 ? Number(raw) : null;
  return <div className="route-page catalog-page purchase-page">
    <title>Order confirmation | Online Store</title>
    <main id="main-content" className="purchase-layout">
      <h1 id="order-heading">{id ? `Order #${id}` : "Order confirmation"}</h1>
      <PurchaseAccess role="Customer">{account => id
        ? <Receipt key={`${account.id}:${id}`} orderId={id} />
        : <p role="alert">Enter a valid order ID.</p>}
      </PurchaseAccess>
    </main>
  </div>;
}

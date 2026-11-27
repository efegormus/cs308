import { useCallback, useEffect, useRef, useState } from "react";
import { Link, useLocation } from "react-router";
import { useAuth } from "../auth/AuthProvider.jsx";
import { useSessionExpiry } from "../auth/useSessionExpiry.js";
import PurchaseAccess from "../components/PurchaseAccess.jsx";
import { ErrorState, LoadingState } from "../components/RequestStates.js";
import Button from "../components/ui/Button.jsx";
import Modal from "../components/ui/Modal.jsx";
import { ToastRegion, useToasts } from "../components/ui/Toast.jsx";
import { loginHref } from "../lib/auth-utils.js";
import { cancelErrorMessage, cancelOrder, canCancelOrder } from "../lib/cancel-order.js";
import { fetchOrderHistory } from "../lib/order-history-api.js";
import { orderStatusLabel, stepStates } from "../lib/order-status.js";
import { formatPrice } from "../lib/product-display.js";
import { useRequest } from "../lib/useRequest.js";
import "../styles/request-states.css";
import "../styles/orders.css";

const date = value => value ? new Date(value).toLocaleString() : "Unknown";

// The Cancel button of a paid order that is still Processing (SCRUM-158). It asks first; the server decides again.
function CancelOrder({ order, onDone }) {
  const { csrfToken, refreshCsrf } = useAuth();
  const request = useRequest();
  useSessionExpiry(request.error);
  const [open, setOpen] = useState(false);
  const [message, setMessage] = useState("");
  const locked = useRef(false);
  if (!canCancelOrder(order)) return null;
  const busy = request.status === "loading";

  async function confirm() {
    if (locked.current) return;
    locked.current = true; setMessage("");
    const result = await request.run(async ({ signal }) => cancelOrder(order.orderId, await csrfToken(), { signal }));
    if (result.status === "success") {
      setOpen(false);
      onDone(`Order #${order.orderId} was cancelled.`, "success", true);
    } else if (result.status === "error") {
      const status = result.error?.status;
      if (status === 400) await refreshCsrf();
      if (status === 409 || status === 404) { setOpen(false); onDone(cancelErrorMessage(result.error), "error", true); }
      else setMessage(cancelErrorMessage(result.error));
    }
    locked.current = false;
  }

  return <>
    <Button variant="secondary" className="order-cancel" aria-label={`Cancel order #${order.orderId}`} onClick={() => { setMessage(""); setOpen(true); }}>
      Cancel order
    </Button>
    <Modal open={open} onClose={() => { if (!busy) setOpen(false); }} title={`Cancel order #${order.orderId}?`}
      footer={<>
        <Button variant="secondary" disabled={busy} onClick={() => setOpen(false)}>Keep order</Button>
        <Button disabled={busy} onClick={confirm}>{busy ? "Cancelling…" : "Yes, cancel order"}</Button>
      </>}>
      <p>The items go back to stock and your payment is refunded. This cannot be undone.</p>
      {message && <p role="alert" className="order-cancel-error">{message}</p>}
    </Modal>
  </>;
}

// One order of the history. The three steps show where it is: processing, in transit, delivered.
function OrderCard({ order, onDone }) {
  return (
    <li className="order-card">
      <div className="order-card-head">
        <h2>Order #{order.orderId}</h2>
        <span className={`order-badge order-badge--${order.status.toLowerCase()}`}>{orderStatusLabel(order.status)}</span>
      </div>
      <p>Placed {date(order.purchasedAt)}{order.deliveredAt && ` · Delivered ${date(order.deliveredAt)}`}</p>
      {order.status !== "Cancelled" && (
        <ol className="order-steps" aria-label="Order progress">
          {stepStates(order.status).map(({ step, label, state }) => (
            <li key={step} className={`order-step order-step--${state}`} aria-current={state === "current" ? "step" : undefined}>
              {label}
            </li>
          ))}
        </ol>
      )}
      <p className="order-card-total">
        {order.itemCount} {order.itemCount === 1 ? "item" : "items"} · <strong>{formatPrice(order.total)}</strong>
      </p>
      <p className="order-card-links">
        <Link to={`/order-confirmation?orderId=${order.orderId}`}>View details</Link>
        {order.invoiceUrl && <> · <a href={order.invoiceUrl}>Download invoice (PDF)</a></>}
        {!order.invoiceUrl && <> · <span>Invoice is being prepared</span></>}
      </p>
      <CancelOrder order={order} onDone={onDone} />
      {order.status === "Delivered" && order.reviewItems?.length > 0 && <ul className="order-review-links">
        {order.reviewItems.map(item => <li key={item.productId}>
          <Link to={`/product?id=${item.productId}#reviews`}>{item.canRate || item.canComment ? "Rate / Comment" : "View your review"}: {item.name}</Link>
        </li>)}
      </ul>}
    </li>
  );
}

function OrderHistory() {
  const location = useLocation();
  const read = useRequest();
  useSessionExpiry(read.error);
  const [orders, setOrders] = useState([]);
  const [next, setNext] = useState(null);
  const [loaded, setLoaded] = useState(false);
  const busy = useRef(false);
  const lastCursor = useRef(null);
  const toasts = useToasts();

  // beforeId null loads the first (newest) page and replaces the list; a number appends the next older page.
  const load = useCallback(async (beforeId = null) => {
    if (busy.current) return;
    busy.current = true;
    lastCursor.current = beforeId;
    const result = await read.run(({ signal }) => fetchOrderHistory(beforeId, { signal }));
    if (result.status === "success") {
      setOrders(previous => beforeId === null ? result.data.orders
        : [...new Map([...previous, ...result.data.orders].map(order => [order.orderId, order])).values()]);
      setNext(result.data.nextBeforeId);
      setLoaded(true);
    } else if (result.status === "error" && [401, 403].includes(result.error.status)) {
      // The session ended: stop showing private orders.
      setOrders([]); setNext(null); setLoaded(false);
    }
    busy.current = false;
  }, [read.run]);
  useEffect(() => { void load(); return () => { read.cancel(); busy.current = false; }; }, [load, read.cancel]);

  // After a cancellation the list is read again, so every card shows what the server says now.
  const cancelled = useCallback((message, tone, reload) => {
    toasts.show(message, { tone });
    if (reload) void load();
  }, [toasts.show, load]);

  const loading = read.status === "loading";
  return (
    <>
      <ToastRegion toasts={toasts.items} onDismiss={toasts.dismiss} />
      {loading && orders.length === 0 && <LoadingState label="Loading your orders…" />}
      <ErrorState error={read.error} onRetry={() => load(lastCursor.current)} />
      {read.error?.status === 401 && <p><Link to={loginHref(location)}>Log in again</Link></p>}
      {loaded && orders.length === 0 && (
        <p role="status">You have not placed any orders yet. <Link to="/products">Browse products</Link></p>
      )}
      {orders.length > 0 && <ul className="order-list" aria-busy={loading}>{orders.map(order => <OrderCard key={order.orderId} order={order} onDone={cancelled} />)}</ul>}
      {next !== null && (
        <button id="more-orders" className="ui-button" type="button" disabled={loading} onClick={() => load(next)}>
          {loading ? "Loading…" : "Load older orders"}
        </button>
      )}
    </>
  );
}

// The customer's order history (SCRUM-76), opened as /orders.
export default function OrdersPage() {
  return (
    <div className="route-page catalog-page purchase-page orders-page">
      <title>My orders | Siesta</title>
      <main id="main-content" className="purchase-layout">
        <h1>My orders</h1>
        <p className="intro">Follow your orders from processing to delivery.</p>
        <PurchaseAccess role="Customer">{account => <OrderHistory key={account.id} />}</PurchaseAccess>
      </main>
    </div>
  );
}

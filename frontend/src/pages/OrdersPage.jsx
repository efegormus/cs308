import { useCallback, useEffect, useRef, useState } from "react";
import { Link, useLocation } from "react-router";
import { useSessionExpiry } from "../auth/useSessionExpiry.js";
import PurchaseAccess from "../components/PurchaseAccess.jsx";
import { ErrorState, LoadingState } from "../components/RequestStates.js";
import { loginHref } from "../lib/auth-utils.js";
import { fetchOrderHistory } from "../lib/order-history-api.js";
import { orderStatusLabel, stepStates } from "../lib/order-status.js";
import { formatPrice } from "../lib/product-display.js";
import { useRequest } from "../lib/useRequest.js";
import "../styles/request-states.css";
import "../styles/orders.css";

const date = value => value ? new Date(value).toLocaleString() : "Unknown";

// One order of the history. The three steps show where it is: processing, in transit, delivered.
function OrderCard({ order }) {
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

  const loading = read.status === "loading";
  return (
    <>
      {loading && orders.length === 0 && <LoadingState label="Loading your orders…" />}
      <ErrorState error={read.error} onRetry={() => load(lastCursor.current)} />
      {read.error?.status === 401 && <p><Link to={loginHref(location)}>Log in again</Link></p>}
      {loaded && orders.length === 0 && (
        <p role="status">You have not placed any orders yet. <Link to="/products">Browse products</Link></p>
      )}
      {orders.length > 0 && <ul className="order-list" aria-busy={loading}>{orders.map(order => <OrderCard key={order.orderId} order={order} />)}</ul>}
      {next !== null && (
        <button id="more-orders" type="button" disabled={loading} onClick={() => load(next)}>
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
      <title>My orders | Online Store</title>
      <main id="main-content" className="purchase-layout">
        <h1>My orders</h1>
        <p className="intro">Follow your orders from processing to delivery.</p>
        <PurchaseAccess role="Customer">{account => <OrderHistory key={account.id} />}</PurchaseAccess>
      </main>
    </div>
  );
}

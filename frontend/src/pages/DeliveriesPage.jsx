import { useCallback, useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider.jsx";
import PurchaseAccess from "../components/PurchaseAccess.jsx";
import { ErrorState, LoadingState } from "../components/RequestStates.js";
import { fetchDeliveries, transitionDelivery } from "../lib/purchase-api.js";
import { formatPrice } from "../lib/product-display.js";
import { useRequest } from "../lib/useRequest.js";
import "../styles/request-states.css";

const date = value => value ? new Date(value).toLocaleString() : "Unknown";
function DeliveryCard({ delivery, busy, onTransition }) {
  return <section className="purchase-card" aria-labelledby={`delivery-${delivery.id}`}>
    <h2 id={`delivery-${delivery.id}`}>Delivery #{delivery.id} · Order #{delivery.orderId}</h2>
    <p>Customer #{delivery.customerId} · {delivery.status} · Payment: {delivery.paymentStatus}</p>
    <p className="snapshot-address">{delivery.address}</p>
    <p>Created: {date(delivery.createdAt)}{delivery.deliveredAt && ` · Delivered: ${date(delivery.deliveredAt)}`}</p>
    <ul className="purchase-lines">{delivery.items.map(item => <li key={item.productId}>
      Product #{item.productId} · {item.name} · Quantity {item.quantity} · {formatPrice(item.total)}
    </li>)}</ul>
    <p className="purchase-total">Original total: {formatPrice(delivery.total)}</p>
    {(delivery.canShip || delivery.canDeliver)
      ? <button type="button" disabled={busy} onClick={() => onTransition(delivery, delivery.canShip ? "InTransit" : "Delivered")}>
        {delivery.canShip ? "Mark in transit" : "Mark delivered"}
      </button>
      : ["Processing", "InTransit"].includes(delivery.status)
        && <p>This delivery needs payment or shipment reconciliation before it can continue.</p>}
  </section>;
}
function Deliveries() {
  const { csrfToken } = useAuth();
  const read = useRequest();
  const mutation = useRequest();
  const [items, setItems] = useState([]);
  const [cursor, setCursor] = useState(null);
  const [loaded, setLoaded] = useState(false);
  const [notice, setNotice] = useState("");
  const busy = useRef(false);
  const lastCursor = useRef(0);
  const load = useCallback(async (afterId = 0) => {
    if (busy.current) return;
    busy.current = true;
    lastCursor.current = afterId;
    mutation.reset();
    const result = await read.run(({ signal }) => fetchDeliveries(afterId, { signal }));
    if (result.status === "success") {
      setItems(previous => afterId === 0 ? result.data.items
        : [...new Map([...previous, ...result.data.items].map(item => [item.id, item])).values()]);
      setCursor(result.data.nextAfterId);
      setLoaded(true);
    }
    busy.current = false;
  }, [read.run, mutation.reset]);
  useEffect(() => { void load(); return () => { read.cancel(); busy.current = false; }; }, [load, read.cancel]);

  async function transition(delivery, status) {
    if (busy.current) return;
    busy.current = true;
    setNotice("");
    const result = await mutation.run(async ({ signal }) =>
      transitionDelivery(delivery, status, await csrfToken(), { signal }));
    if (result.status === "success") {
      setItems(previous => previous.map(item => item.id === delivery.id ? result.data : item));
      setNotice(`Delivery #${delivery.id} updated.`);
    }
    // Lost responses/stale versions require a GET refresh. Never retry a transition automatically.
    busy.current = false;
  }
  const disabled = read.status === "loading" || mutation.status === "loading";
  return <>
    {read.status === "loading" && <LoadingState label="Loading deliveries…" />}
    <ErrorState error={read.error} onRetry={() => load(lastCursor.current)} />
    <ErrorState error={mutation.error} />
    {notice && <p role="status">{notice}</p>}
    <button id="retry" type="button" disabled={disabled} onClick={() => load()}>Refresh deliveries</button>
    {loaded && items.length === 0 && <p role="status">No deliveries yet.</p>}
    <div id="deliveries">{items.map(item => <DeliveryCard key={item.id} delivery={item} busy={disabled} onTransition={transition} />)}</div>
    {cursor !== null && <button id="more" type="button" disabled={disabled} onClick={() => load(cursor)}>Load more deliveries</button>}
  </>;
}
export default function DeliveriesPage() {
  return <div className="route-page catalog-page purchase-page">
    <title>Deliveries | Online Store</title>
    <main id="main-content" className="purchase-layout">
      <h1>Deliveries</h1>
      <p className="intro">Manage confirmed orders using their original purchase details.</p>
      <PurchaseAccess role="ProductManager">{account => <Deliveries key={account.id} />}</PurchaseAccess>
    </main>
  </div>;
}

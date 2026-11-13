import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router";
import { useSessionExpiry } from "../auth/useSessionExpiry.js";
import PurchaseAccess from "../components/PurchaseAccess.jsx";
import { ErrorState, LoadingState } from "../components/RequestStates.js";
import { fetchCustomerProfile } from "../lib/customer-profile-api.js";
import { fetchOrderHistory } from "../lib/order-history-api.js";
import { orderStatusLabel } from "../lib/order-status.js";
import { formatPrice } from "../lib/product-display.js";
import { recentOrders } from "../lib/recent-orders.js";
import { roleLinks } from "../lib/role-routes.js";
import { useRequest } from "../lib/useRequest.js";
import "../styles/request-states.css";
import "../styles/role-home.css";

const date = value => value ? new Date(value).toLocaleDateString() : "Unknown";

// Loads one private resource when the page opens. A 401 ends the session (useSessionExpiry); every other error
// is shown in its own section with a "Try again" button, so one failing request does not hide the other section.
function useSection(fetcher) {
  const read = useRequest();
  const [data, setData] = useState(null);
  useSessionExpiry(read.error);
  const load = useCallback(async () => {
    const result = await read.run(({ signal }) => fetcher({ signal }));
    if (result.status === "success") setData(result.data);
  }, [read.run, fetcher]);
  useEffect(() => { void load(); return () => read.cancel(); }, [load, read.cancel]);
  return { data, error: read.error, loading: read.status === "loading", retry: load };
}

const loadOrders = ({ signal }) => fetchOrderHistory(null, { signal });

function RecentOrders() {
  const { data, error, loading, retry } = useSection(loadOrders);
  const orders = data ? recentOrders(data.orders) : null;
  return (
    <section className="dash-section" aria-labelledby="recent-orders-title">
      <div className="dash-section-head">
        <h2 id="recent-orders-title">Recent orders</h2>
        <Link to="/orders">View all orders</Link>
      </div>
      {loading && !orders && <LoadingState label="Loading your orders…" />}
      <ErrorState error={error} onRetry={retry} retrying={loading} />
      {orders && orders.length === 0 && (
        <p role="status">You have not placed any orders yet. <Link to="/products">Browse products</Link></p>
      )}
      {orders && orders.length > 0 && (
        <ul className="dash-orders" aria-busy={loading}>
          {orders.map(order => <li key={order.orderId} className="dash-order">
            <div className="dash-order-head">
              <strong>Order #{order.orderId}</strong>
              <span className={`dash-status dash-status--${order.status.toLowerCase()}`}>{orderStatusLabel(order.status)}</span>
            </div>
            <p>{date(order.purchasedAt)} · {order.itemCount} {order.itemCount === 1 ? "item" : "items"} · {formatPrice(order.total)}</p>
            <Link to={`/order-confirmation?orderId=${order.orderId}`}>View details</Link>
          </li>)}
        </ul>
      )}
    </section>
  );
}

function ProfileSummary() {
  const { data, error, loading, retry } = useSection(fetchCustomerProfile);
  return (
    <section className="dash-section" aria-labelledby="profile-title">
      <div className="dash-section-head">
        <h2 id="profile-title">Your details</h2>
        <Link to="/profile">Edit details</Link>
      </div>
      {loading && !data && <LoadingState label="Loading your details…" />}
      <ErrorState error={error} onRetry={retry} retrying={loading} />
      {data && (
        <dl className="dash-profile">
          <div><dt>Name</dt><dd>{data.name}</dd></div>
          <div><dt>Email</dt><dd>{data.email}</dd></div>
          <div><dt>Tax ID</dt><dd>{data.taxId}</dd></div>
          <div><dt>Home address</dt><dd>{data.homeAddress}</dd></div>
        </dl>
      )}
    </section>
  );
}

// The customer's account page (SCRUM-125), opened as /dashboard from the profile icon in the header.
export default function DashboardPage() {
  return <div className="route-page catalog-page purchase-page role-home-page">
    <title>My account | Online Store</title>
    <main id="main-content" className="purchase-layout">
      <PurchaseAccess role="Customer">{account => <>
        <header className="role-home-head">
          <p className="role-home-badge">Customer</p>
          <h1 id="welcome">Welcome, {account.name}</h1>
          <p className="role-home-sub">My account · {account.email}</p>
        </header>
        <div className="dash-columns">
          <RecentOrders key={`orders-${account.id}`} />
          <ProfileSummary key={`profile-${account.id}`} />
        </div>
        <nav aria-label="Shortcuts">
          <ul className="role-home-grid">
            {roleLinks(account.role).map(([to, label, text]) => <li key={to}>
              <Link className="role-home-card" to={to}>
                <span className="role-home-card-title">{label}</span>
                <span className="role-home-card-text">{text}</span>
              </Link>
            </li>)}
          </ul>
        </nav>
      </>}</PurchaseAccess>
    </main>
  </div>;
}

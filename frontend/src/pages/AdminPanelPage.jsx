// The admin panel for product and sales managers (SCRUM-124) at /manage and /manage/<section>. It replaces the small
// landing page of SCRUM-130. Data comes from the existing APIs only (docs/api-contract-dashboards.md and the
// deliveries API); review approval waits for SCRUM-81.
import { useCallback, useEffect, useRef, useState } from "react";
import { Link, Navigate, useParams } from "react-router";
import { useSessionExpiry } from "../auth/useSessionExpiry.js";
import AdminShell from "../components/admin/AdminShell.jsx";
import KpiCard from "../components/admin/KpiCard.jsx";
import ReviewApprovalRow from "../components/admin/ReviewApprovalRow.jsx";
import PurchaseAccess from "../components/PurchaseAccess.jsx";
import { ErrorState } from "../components/RequestStates.js";
import Button from "../components/ui/Button.jsx";
import Table from "../components/ui/Table.js";
import { fetchProductManagementDashboard, fetchSalesDashboard, salesPeriod } from "../lib/admin-api.js";
import { resolveSection } from "../lib/admin-menu.js";
import { fetchDeliveries } from "../lib/purchase-api.js";
import { formatPrice } from "../lib/product-display.js";
import { useRequest } from "../lib/useRequest.js";
import "../components/ui/Table.css";
import "../styles/request-states.css";
import "../styles/admin.css";

const dateTime = value => value ? new Date(value).toLocaleString("tr-TR") : "—";
const day = value => new Date(value).toLocaleDateString("tr-TR");
const percent = new Intl.NumberFormat("tr-TR", { style: "percent", maximumFractionDigits: 1 });
const SALES_DAYS = 30;

/** Placeholder for SCRUM-81: there is no approval API yet, so nothing is saved and this resolves at once. */
const approveReviewPlaceholder = () => Promise.resolve();

// Loads one resource when the section opens; a 401 ends the session.
function useResource(fetcher) {
  const read = useRequest();
  const [data, setData] = useState(null);
  useSessionExpiry(read.error);
  const load = useCallback(async () => {
    const result = await read.run(({ signal }) => fetcher({ signal }));
    if (result.status === "success") setData(result.data);
  }, [read.run, fetcher]);
  useEffect(() => { void load(); return () => read.cancel(); }, [load, read.cancel]);
  return { data, setData, error: read.error, loading: read.status === "loading", retry: load };
}

function SectionHead({ title, children }) {
  return <header className="admin-section-head">
    <h1 tabIndex={-1}>{title}</h1>
    {children && <p className="admin-lead">{children}</p>}
  </header>;
}

const capped = (list, cap) => list.length >= cap ? `The first ${cap} are shown.` : undefined;

// ---- Product manager --------------------------------------------------------------------------------------------

const lowStockColumns = [
  { key: "name", header: "Product", rowHeader: true },
  { key: "sku", header: "SKU" },
  { key: "stockQuantity", header: "In stock", align: "end" }
];
const pendingColumns = [
  { key: "id", header: "Delivery", rowHeader: true, render: row => `#${row.id}` },
  { key: "orderId", header: "Order", render: row => `#${row.orderId}` },
  { key: "status", header: "Status", render: row => row.status === "InTransit" ? "In transit" : row.status },
  { key: "createdAt", header: "Created", render: row => dateTime(row.createdAt) }
];
const reviewColumns = [
  { key: "productName", header: "Product", rowHeader: true },
  { key: "body", header: "Review", render: row => row.body.length > 120 ? `${row.body.slice(0, 120)}…` : row.body },
  { key: "createdAt", header: "Written", render: row => day(row.createdAt) }
];

function ProductOverview({ dashboard }) {
  const { data, error, loading, retry } = dashboard;
  return <>
    <SectionHead title="Overview">Stock, deliveries and reviews that need you.</SectionHead>
    <ErrorState error={error} onRetry={retry} retrying={loading} />
    {data && <div className="admin-kpis">
      <KpiCard index={0} label="Low-stock products" value={data.lowStockProducts.length} hint={capped(data.lowStockProducts, 20)} />
      <KpiCard index={1} label="Pending deliveries" value={data.pendingDeliveries.length} hint={capped(data.pendingDeliveries, 20)} />
      <KpiCard index={2} label="Reviews to approve" value={data.commentsWaitingForApproval.length} hint={capped(data.commentsWaitingForApproval, 10)} />
    </div>}
    <div className="admin-panels">
      <section aria-labelledby="pm-low-stock">
        <div className="admin-panel-head"><h2 id="pm-low-stock">Low stock</h2><Link to="/manage/low-stock">See all</Link></div>
        <Table columns={lowStockColumns} rows={data?.lowStockProducts.slice(0, 5) ?? []} loading={loading && !data}
          loadingLabel="Loading low-stock products…" emptyText="No product is low on stock." caption="Lowest stock first" />
      </section>
      <section aria-labelledby="pm-pending">
        <div className="admin-panel-head"><h2 id="pm-pending">Pending deliveries</h2><Link to="/manage/deliveries">See all</Link></div>
        <Table columns={pendingColumns} rows={data?.pendingDeliveries.slice(0, 5) ?? []} loading={loading && !data}
          loadingLabel="Loading pending deliveries…" emptyText="No delivery is waiting." caption="Processing and in transit, newest first" />
      </section>
      <section aria-labelledby="pm-reviews">
        <div className="admin-panel-head"><h2 id="pm-reviews">Reviews to approve</h2><Link to="/manage/reviews">Open reviews</Link></div>
        <Table columns={reviewColumns} rows={data?.commentsWaitingForApproval.slice(0, 5) ?? []} loading={loading && !data}
          loadingLabel="Loading reviews…" emptyText="No review is waiting for approval." caption="Oldest first" />
      </section>
    </div>
  </>;
}

const deliveryColumns = [
  { key: "id", header: "Delivery", rowHeader: true, render: row => `#${row.id}` },
  { key: "customerId", header: "Customer", render: row => `#${row.customerId}` },
  { key: "items", header: "Products", render: row => <ul className="admin-cell-list">{row.items.map(item =>
    <li key={item.productId}>#{item.productId} {item.name} × {item.quantity}</li>)}</ul> },
  { key: "total", header: "Total", align: "end", render: row => formatPrice(row.total) },
  { key: "address", header: "Address" },
  { key: "status", header: "Completed", render: row => row.status === "Delivered" ? `Yes, ${dateTime(row.deliveredAt)}`
    : `No (${row.status === "InTransit" ? "in transit" : row.status.toLowerCase()})` }
];

function DeliveriesSection() {
  const read = useRequest();
  useSessionExpiry(read.error);
  const [items, setItems] = useState(null);
  const [cursor, setCursor] = useState(null);
  const load = useCallback(async (afterId = 0) => {
    const result = await read.run(({ signal }) => fetchDeliveries(afterId, { signal }));
    if (result.status !== "success") return;
    setItems(previous => afterId === 0 || !previous ? result.data.items
      : [...new Map([...previous, ...result.data.items].map(item => [item.id, item])).values()]);
    setCursor(result.data.nextAfterId);
  }, [read.run]);
  useEffect(() => { void load(); return () => read.cancel(); }, [load, read.cancel]);
  const loading = read.status === "loading";
  return <>
    <SectionHead title="Deliveries">
      Every order's delivery. To mark one in transit or delivered, use the <Link to="/deliveries">Deliveries page</Link>.
    </SectionHead>
    <ErrorState error={read.error} onRetry={() => load(0)} retrying={loading} />
    <Table columns={deliveryColumns} rows={items ?? []} loading={loading && !items} caption="Deliveries"
      loadingLabel="Loading deliveries…" emptyText="No deliveries yet." />
    {cursor !== null && items && <Button className="admin-more" disabled={loading} onClick={() => load(cursor)}>Load more deliveries</Button>}
  </>;
}

function ReviewsSection({ dashboard }) {
  const { data, setData, error, loading, retry } = dashboard;
  const [announcement, setAnnouncement] = useState("");
  const list = useRef(null);
  const reviews = data?.commentsWaitingForApproval ?? [];

  const removed = useCallback(id => {
    const rows = [...(list.current?.querySelectorAll(".admin-review") ?? [])];
    const index = rows.findIndex(element => element.dataset.id === String(id));
    setData(previous => previous && ({
      ...previous, commentsWaitingForApproval: previous.commentsWaitingForApproval.filter(review => review.id !== id)
    }));
    // The removed row held the focus; hand it to the next row's button, or to the heading when the list is empty.
    requestAnimationFrame(() => {
      if (document.activeElement && document.activeElement !== document.body) return;
      const rest = list.current?.querySelectorAll(".admin-approve") ?? [];
      (rest[Math.min(Math.max(index, 0), rest.length - 1)] ?? document.querySelector(".admin-section h1"))?.focus();
    });
  }, [setData]);

  return <>
    <SectionHead title="Reviews">
      Reviews wait here until a product manager approves them. Approving is not saved yet: the approval API comes
      with SCRUM-81, so an approved review comes back after a reload.
    </SectionHead>
    <ErrorState error={error} onRetry={retry} retrying={loading} />
    <div className="admin-reviews" aria-live="polite">
      {loading && !data && <Table columns={reviewColumns} loading loadingLabel="Loading reviews…" />}
      {data && reviews.length === 0 && <p className="admin-empty" role="status">No review is waiting for approval.</p>}
      {reviews.length > 0 && <ul ref={list} className="admin-review-list">
        {reviews.map(review => <ReviewApprovalRow key={review.id} review={review} onApprove={approveReviewPlaceholder}
          onRemoved={removed} onApproved={() => setAnnouncement(`Review approved: ${review.productName}`)} />)}
      </ul>}
      <p className="admin-sr">{announcement}</p>
    </div>
  </>;
}

function LowStockSection({ dashboard }) {
  const { data, error, loading, retry } = dashboard;
  return <>
    <SectionHead title="Low stock">Active products with 5 or fewer in stock, lowest first.</SectionHead>
    <ErrorState error={error} onRetry={retry} retrying={loading} />
    <Table columns={lowStockColumns} rows={data?.lowStockProducts ?? []} loading={loading && !data} caption="Low-stock products"
      loadingLabel="Loading low-stock products…" emptyText="No product is low on stock." />
  </>;
}

// ---- Sales manager ----------------------------------------------------------------------------------------------

const invoiceColumns = [
  { key: "invoiceNumber", header: "Invoice", rowHeader: true },
  { key: "orderId", header: "Order", render: row => `#${row.orderId}` },
  { key: "issuedAt", header: "Issued", render: row => dateTime(row.issuedAt) },
  { key: "total", header: "Total", align: "end", render: row => formatPrice(row.total) }
];
const discountColumns = [
  { key: "name", header: "Discount", rowHeader: true },
  { key: "rate", header: "Rate", align: "end", render: row => percent.format(row.rate) },
  { key: "startsAt", header: "Starts", render: row => day(row.startsAt) },
  { key: "endsAt", header: "Ends", render: row => day(row.endsAt) },
  { key: "productCount", header: "Products", align: "end" }
];

function SalesOverview({ dashboard }) {
  const { data, error, loading, retry } = dashboard;
  return <>
    <SectionHead title="Overview">Revenue for the last {SALES_DAYS} days, recent invoices and running discounts.</SectionHead>
    <ErrorState error={error} onRetry={retry} retrying={loading} />
    {data && <div className="admin-kpis">
      <KpiCard index={0} kind="money" label="Gross revenue" value={data.grossRevenue} />
      <KpiCard index={1} kind="money" label="Refunds" value={data.refunds} />
      <KpiCard index={2} kind="money" label="Net revenue" value={data.netRevenue} />
      <KpiCard index={3} label="Active discounts" value={data.activeDiscounts.length} hint={capped(data.activeDiscounts, 10)} />
    </div>}
    <div className="admin-panels">
      <section aria-labelledby="sm-invoices">
        <div className="admin-panel-head"><h2 id="sm-invoices">Recent invoices</h2><Link to="/manage/invoices">See all</Link></div>
        <Table columns={invoiceColumns} rows={data?.recentInvoices ?? []} loading={loading && !data} caption="Newest first"
          loadingLabel="Loading invoices…" emptyText="No invoices yet." />
      </section>
    </div>
  </>;
}

function InvoicesSection({ dashboard }) {
  const { data, error, loading, retry } = dashboard;
  return <>
    <SectionHead title="Invoices">The latest ten invoices, newest first.</SectionHead>
    <ErrorState error={error} onRetry={retry} retrying={loading} />
    <Table columns={invoiceColumns} rows={data?.recentInvoices ?? []} loading={loading && !data} caption="Recent invoices"
      loadingLabel="Loading invoices…" emptyText="No invoices yet." />
  </>;
}

function DiscountsSection({ dashboard }) {
  const { data, error, loading, retry } = dashboard;
  return <>
    <SectionHead title="Discounts">Discounts running now.</SectionHead>
    <ErrorState error={error} onRetry={retry} retrying={loading} />
    <Table columns={discountColumns} rows={data?.activeDiscounts ?? []} loading={loading && !data} caption="Active discounts, ending soonest first"
      loadingLabel="Loading discounts…" emptyText="No discount is running." />
  </>;
}

// ---- Panels -----------------------------------------------------------------------------------------------------

const loadProductDashboard = ({ signal }) => fetchProductManagementDashboard({ signal });

function ProductManagerPanel({ account, section }) {
  // One dashboard request serves the overview, reviews and low-stock sections; it lives above the section switch,
  // so moving between those sections does not load it again.
  const dashboard = useResource(loadProductDashboard);
  return <AdminShell account={account} section={section}>
    {section.id === "deliveries" ? <DeliveriesSection />
      : section.id === "reviews" ? <ReviewsSection dashboard={dashboard} />
      : section.id === "low-stock" ? <LowStockSection dashboard={dashboard} />
      : <ProductOverview dashboard={dashboard} />}
  </AdminShell>;
}

function SalesManagerPanel({ account, section }) {
  const [period] = useState(() => salesPeriod(new Date(), SALES_DAYS));
  const load = useCallback(({ signal }) => fetchSalesDashboard({ ...period, signal }), [period]);
  const dashboard = useResource(load);
  return <AdminShell account={account} section={section}>
    {section.id === "invoices" ? <InvoicesSection dashboard={dashboard} />
      : section.id === "discounts" ? <DiscountsSection dashboard={dashboard} />
      : <SalesOverview dashboard={dashboard} />}
  </AdminShell>;
}

function Panel({ account }) {
  const { section: segment } = useParams();
  const section = resolveSection(account.role, segment);
  if (!section) return <Navigate replace to="/manage" />;
  return account.role === "SalesManager"
    ? <SalesManagerPanel account={account} section={section} />
    : <ProductManagerPanel account={account} section={section} />;
}

export default function AdminPanelPage() {
  return <div className="route-page admin-page">
    <title>Management | Online Store</title>
    <main id="main-content" className="admin-main">
      <PurchaseAccess role={["ProductManager", "SalesManager"]}>{account => <Panel key={account.id} account={account} />}</PurchaseAccess>
    </main>
  </div>;
}

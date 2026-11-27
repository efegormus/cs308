import { useCallback, useEffect, useMemo, useState } from "react";
import { useAuth } from "../../auth/AuthProvider.jsx";
import { useSessionExpiry } from "../../auth/useSessionExpiry.js";
import { fetchProducts, setStock } from "../../lib/catalog-management-api.js";
import { mergeById } from "../../lib/catalog-management.js";
import { buildStockRequest, stockFailureMessage, stockLabel, visibleStockProducts } from "../../lib/stock-management.js";
import { useRequest } from "../../lib/useRequest.js";
import { ErrorState } from "../RequestStates.js";
import Button from "../ui/Button.jsx";
import Input from "../ui/Input.jsx";
import Table from "../ui/Table.js";

const WRITE_TIMEOUT_MS = 15000;

// The product manager's stock screen (SCRUM-78; API: docs/catalog-management.md). Each row shows the stock the server
// last reported and a field for the new count. The save sends both counts, so a sale or a cancellation that happened
// while the page was open is never overwritten: the server answers 409 and the row shows the new count. A row changes
// only after the server answered, and a write is never retried by itself.
export default function StockManagement() {
  const { csrfToken } = useAuth();
  const read = useRequest();
  const [expired, setExpired] = useState(null);
  useSessionExpiry(read.error, expired);

  const [products, setProducts] = useState(null);
  const [cursor, setCursor] = useState(null);
  const [search, setSearch] = useState("");
  const [lowOnly, setLowOnly] = useState(false);
  const [drafts, setDrafts] = useState({});
  const [rowErrors, setRowErrors] = useState({});
  const [saving, setSaving] = useState(null);
  const [notice, setNotice] = useState({ kind: "", text: "" });

  // refreshOnly reads rows again to correct them (after a 409) and leaves the "Load more" position alone.
  const load = useCallback(async (afterId = 0, refreshOnly = false) => {
    const result = await read.run(({ signal }) => fetchProducts({ afterId, signal }));
    if (result.status !== "success") return;
    setProducts(previous => afterId === 0 || !previous ? result.data.products : mergeById(previous, result.data.products));
    if (!refreshOnly) setCursor(result.data.nextAfterId);
  }, [read.run]);

  useEffect(() => { void load(); return () => read.cancel(); }, [load, read.cancel]);

  const visible = useMemo(() => visibleStockProducts(products ?? [], { search, lowOnly }), [products, search, lowOnly]);

  async function save(product) {
    const built = buildStockRequest(product, drafts[product.id]);
    if (!built.ok) {
      setRowErrors(previous => ({ ...previous, [product.id]: built.error }));
      return;
    }
    setSaving(product.id);
    setNotice({ kind: "", text: "" });
    setRowErrors(previous => ({ ...previous, [product.id]: "" }));
    try {
      const token = await csrfToken();
      const updated = await setStock(product.id, built.request, token, { signal: AbortSignal.timeout(WRITE_TIMEOUT_MS) });
      setProducts(previous => mergeById(previous, [updated]));
      setDrafts(previous => ({ ...previous, [product.id]: undefined }));
      setNotice({ kind: "success", text: `Stock saved: ${updated.name} now has ${updated.stockQuantity}.` });
    } catch (error) {
      if (error?.status === 401) setExpired(error);
      setRowErrors(previous => ({ ...previous, [product.id]: stockFailureMessage(error) }));
      // The count changed or the product is gone: read this row again so the screen shows what is true now.
      if (error?.status === 409 || error?.status === 404) void load(product.id - 1, true);
    } finally {
      setSaving(null);
    }
  }

  const loading = read.status === "loading";
  const columns = [
    { key: "name", header: "Product", rowHeader: true, render: row => `${row.name} (${row.model})${row.isActive ? "" : " – off sale"}` },
    { key: "sku", header: "SKU" },
    { key: "state", header: "Status", render: row => stockLabel(row) },
    { key: "stockQuantity", header: "In stock", align: "end" },
    { key: "set", header: "New count", render: row => <div className="admin-pricing-set">
      <Input label={`New stock count for ${row.name}`} hideLabel inputMode="numeric" autoComplete="off" value={drafts[row.id] ?? ""}
        placeholder={String(row.stockQuantity)} error={rowErrors[row.id] || undefined} errorRole="alert"
        onChange={event => setDrafts(previous => ({ ...previous, [row.id]: event.target.value }))} />
      <Button variant="secondary" disabled={saving === row.id || !(drafts[row.id] ?? "").trim()} onClick={() => save(row)}
        aria-label={`Save stock for ${row.name}`}>{saving === row.id ? "Saving…" : "Save stock"}</Button>
    </div> }
  ];

  return <>
    <header className="admin-section-head">
      <h1 tabIndex={-1}>Stock</h1>
      <p className="admin-lead">Set how many units of each product are in stock. The stock cannot go below zero. Shoppers see the
        new count at once, and a product with none left cannot be added to a cart.</p>
    </header>
    <p className={notice.kind === "error" ? "request-state request-state--error" : "admin-sr"} role={notice.kind === "error" ? "alert" : "status"}>
      {notice.text}
    </p>
    {notice.kind === "success" && <p className="admin-pricing-notice" aria-hidden="true">{notice.text}</p>}

    <section aria-labelledby="stock-products">
      <div className="admin-panel-head"><h2 id="stock-products">Products</h2></div>
      <div className="admin-pricing-filters">
        <Button variant={lowOnly ? "primary" : "secondary"} aria-pressed={lowOnly} onClick={() => setLowOnly(value => !value)}>
          Low stock only
        </Button>
        <Input label="Search products" hideLabel type="search" placeholder="Search name, model or SKU" value={search}
          onChange={event => setSearch(event.target.value)} />
      </div>
      <ErrorState error={read.error} onRetry={() => load(0)} retrying={loading} />
      <Table columns={columns} rows={visible} loading={loading && !products} caption="Stock by product, lowest first"
        loadingLabel="Loading products…" emptyText={lowOnly ? "No loaded product is low on stock." : "No product matches."} />
      {cursor !== null && products && <Button variant="secondary" className="admin-more" disabled={loading}
        onClick={() => load(cursor)}>Load more products</Button>}
    </section>
  </>;
}

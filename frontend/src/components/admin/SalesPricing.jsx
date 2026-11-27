import { useCallback, useEffect, useMemo, useState } from "react";
import { useAuth } from "../../auth/AuthProvider.jsx";
import { useSessionExpiry } from "../../auth/useSessionExpiry.js";
import { cancelDiscount, createDiscount, fetchDiscounts, fetchSalesProducts, setProductPrice } from "../../lib/sales-pricing-api.js";
import {
  buildDiscountRequest, isUnpriced, parsePercent, parsePrice, previewRows, pricingFailureMessage, toggleSelection, visibleProducts
} from "../../lib/sales-pricing.js";
import { formatPrice } from "../../lib/product-display.js";
import { useRequest } from "../../lib/useRequest.js";
import { ErrorState } from "../RequestStates.js";
import Button from "../ui/Button.jsx";
import Input from "../ui/Input.jsx";
import Table from "../ui/Table.js";

const WRITE_TIMEOUT_MS = 15000;
const dateTime = value => value ? new Date(value).toLocaleString("tr-TR") : "—";
const merge = (previous, next) => [...new Map([...(previous ?? []), ...next].map(item => [item.id, item])).values()];

// The sales manager's pricing screen (SCRUM-156; API: docs/sales-pricing-api.md). Unpriced products are listed first so a
// price can be set at once; products are then picked for a discount with a live preview of the new prices. The server
// checks everything again: a row or list changes only after the server answered, and a write is never retried by itself.
export default function SalesPricing() {
  const { csrfToken } = useAuth();
  const productsRead = useRequest();
  const discountsRead = useRequest();
  const [expired, setExpired] = useState(null);
  useSessionExpiry(productsRead.error ?? discountsRead.error, expired);

  const [products, setProducts] = useState(null);
  const [productCursor, setProductCursor] = useState(null);
  const [discounts, setDiscounts] = useState(null);
  const [discountCursor, setDiscountCursor] = useState(null);
  const [view, setView] = useState("unpriced");
  const [search, setSearch] = useState("");
  const [drafts, setDrafts] = useState({});
  const [rowErrors, setRowErrors] = useState({});
  const [saving, setSaving] = useState(null);
  const [selected, setSelected] = useState([]);
  const [form, setForm] = useState({ name: "", percent: "", startsAt: "", endsAt: "" });
  const [formErrors, setFormErrors] = useState({});
  const [applying, setApplying] = useState(false);
  const [notice, setNotice] = useState({ kind: "", text: "" });

  const loadProducts = useCallback(async (afterId = 0) => {
    const result = await productsRead.run(({ signal }) => fetchSalesProducts({ afterId, signal }));
    if (result.status !== "success") return;
    setProducts(previous => afterId === 0 || !previous ? result.data.products : merge(previous, result.data.products));
    setProductCursor(result.data.nextAfterId);
  }, [productsRead.run]);

  const loadDiscounts = useCallback(async (beforeId = 0) => {
    const result = await discountsRead.run(({ signal }) => fetchDiscounts({ beforeId, signal }));
    if (result.status !== "success") return;
    setDiscounts(previous => beforeId === 0 || !previous ? result.data.discounts : merge(previous, result.data.discounts));
    setDiscountCursor(result.data.nextBeforeId);
  }, [discountsRead.run]);

  useEffect(() => { void loadProducts(); void loadDiscounts(); return () => { productsRead.cancel(); discountsRead.cancel(); }; },
    [loadProducts, loadDiscounts, productsRead.cancel, discountsRead.cancel]);

  const failed = error => {
    if (error?.status === 401) setExpired(error);
    return pricingFailureMessage(error);
  };

  async function savePrice(product) {
    const price = parsePrice(drafts[product.id]);
    if (price === null) {
      setRowErrors(previous => ({ ...previous, [product.id]: "Use a price above 0 with at most 2 decimals." }));
      return;
    }
    setSaving(product.id);
    setRowErrors(previous => ({ ...previous, [product.id]: "" }));
    try {
      const token = await csrfToken();
      const updated = await setProductPrice(product.id, price, token, { signal: AbortSignal.timeout(WRITE_TIMEOUT_MS) });
      setProducts(previous => previous.map(row => row.id === updated.id ? updated : row));
      setDrafts(previous => ({ ...previous, [product.id]: undefined }));
      setNotice({ kind: "success", text: `Price saved: ${updated.name} is now ${formatPrice(updated.listPrice)}.` });
    } catch (error) {
      setRowErrors(previous => ({ ...previous, [product.id]: failed(error) }));
      if (error?.status === 404) void loadProducts();
    } finally {
      setSaving(null);
    }
  }

  const rate = parsePercent(form.percent);
  const visible = useMemo(() => visibleProducts(products ?? [], { view, search }), [products, view, search]);
  const preview = useMemo(() => previewRows(products ?? [], selected, rate), [products, selected, rate]);

  async function applyDiscount(event) {
    event.preventDefault();
    setNotice({ kind: "", text: "" });
    const built = buildDiscountRequest({ ...form, productIds: selected });
    setFormErrors(built.ok ? {} : built.errors);
    if (!built.ok) return;
    setApplying(true);
    try {
      const token = await csrfToken();
      const created = await createDiscount(built.request, token, { signal: AbortSignal.timeout(WRITE_TIMEOUT_MS) });
      setDiscounts(previous => merge([created], previous ?? []).sort((a, b) => b.id - a.id));
      setSelected([]);
      setForm({ name: "", percent: "", startsAt: "", endsAt: "" });
      setNotice({ kind: "success", text: `Discount created: ${created.name} (${created.status.toLowerCase()}).` });
      void loadProducts(); // the shown prices changed
    } catch (error) {
      setNotice({ kind: "error", text: failed(error) });
    } finally {
      setApplying(false);
    }
  }

  async function cancel(discount) {
    setNotice({ kind: "", text: "" });
    try {
      const token = await csrfToken();
      const updated = await cancelDiscount(discount.id, token, { signal: AbortSignal.timeout(WRITE_TIMEOUT_MS) });
      setDiscounts(previous => previous.map(row => row.id === updated.id ? updated : row));
      setNotice({ kind: "success", text: `Discount ${updated.status.toLowerCase()}: ${updated.name}.` });
      void loadProducts();
    } catch (error) {
      setNotice({ kind: "error", text: failed(error) });
      if (error?.status === 404) void loadDiscounts();
    }
  }

  const productColumns = [
    { key: "select", header: "Discount", render: row => <input type="checkbox" checked={selected.includes(row.id)}
      disabled={!row.isActive || isUnpriced(row)} aria-label={`Select ${row.name} for the discount`}
      onChange={() => setSelected(previous => toggleSelection(previous, row.id))} /> },
    { key: "name", header: "Product", rowHeader: true, render: row => `${row.name}${row.isActive ? "" : " (inactive)"}` },
    { key: "listPrice", header: "List price", align: "end", render: row => isUnpriced(row) ? "Not priced" : formatPrice(row.listPrice) },
    { key: "price", header: "Price now", align: "end", render: row => isUnpriced(row) ? "—"
      : row.discountRate > 0 ? `${formatPrice(row.price)} (−${Math.round(row.discountRate * 10000) / 100}%)` : formatPrice(row.price) },
    { key: "set", header: "Set price", render: row => <div className="admin-pricing-set">
      <Input label={`New price for ${row.name}`} hideLabel inputMode="decimal" autoComplete="off" value={drafts[row.id] ?? ""}
        placeholder="0.00" error={rowErrors[row.id] || undefined} errorRole="alert"
        onChange={event => setDrafts(previous => ({ ...previous, [row.id]: event.target.value }))} />
      <Button variant="secondary" disabled={saving === row.id || !(drafts[row.id] ?? "").trim()} onClick={() => savePrice(row)}>
        {saving === row.id ? "Saving…" : "Save price"}
      </Button>
    </div> }
  ];
  const previewColumns = [
    { key: "name", header: "Product", rowHeader: true },
    { key: "listPrice", header: "List price", align: "end", render: row => formatPrice(row.listPrice) },
    { key: "newPrice", header: "New price", align: "end", render: row => row.newPrice === null ? "—" : formatPrice(row.newPrice) }
  ];
  const discountColumns = [
    { key: "name", header: "Discount", rowHeader: true },
    { key: "rate", header: "Rate", align: "end", render: row => `${Math.round(row.rate * 10000) / 100}%` },
    { key: "status", header: "Status" },
    { key: "period", header: "Period", render: row => `${dateTime(row.startsAt)} – ${dateTime(row.endsAt)}` },
    { key: "productIds", header: "Products", align: "end", render: row => row.productIds.length },
    { key: "cancel", header: "", render: row => (row.status === "Active" || row.status === "Scheduled")
      ? <Button variant="secondary" onClick={() => cancel(row)}>Cancel discount</Button> : null }
  ];

  const loading = productsRead.status === "loading";
  const unpricedCount = (products ?? []).filter(isUnpriced).length;
  return <>
    <header className="admin-section-head">
      <h1 tabIndex={-1}>Pricing</h1>
      <p className="admin-lead">Set prices, then discount products. A discount changes the price shoppers pay while it runs;
        the list price stays as you set it.</p>
    </header>
    <p className={notice.kind === "error" ? "request-state request-state--error" : "admin-sr"} role={notice.kind === "error" ? "alert" : "status"}>
      {notice.text}
    </p>
    {notice.kind === "success" && <p className="admin-pricing-notice" aria-hidden="true">{notice.text}</p>}

    <section aria-labelledby="pricing-products">
      <div className="admin-panel-head"><h2 id="pricing-products">Products</h2></div>
      <div className="admin-pricing-filters">
        <Button variant={view === "unpriced" ? "primary" : "secondary"} aria-pressed={view === "unpriced"} onClick={() => setView("unpriced")}>
          Unpriced{products ? ` (${unpricedCount})` : ""}
        </Button>
        <Button variant={view === "all" ? "primary" : "secondary"} aria-pressed={view === "all"} onClick={() => setView("all")}>All products</Button>
        <Input label="Search products" hideLabel type="search" placeholder="Search name or model" value={search}
          onChange={event => setSearch(event.target.value)} />
      </div>
      <ErrorState error={productsRead.error} onRetry={() => loadProducts(0)} retrying={loading} />
      <Table columns={productColumns} rows={visible} loading={loading && !products} caption={view === "unpriced" ? "Products without a price" : "All products"}
        loadingLabel="Loading products…" emptyText={view === "unpriced" ? "Every loaded product has a price." : "No product matches."} />
      {productCursor !== null && products && <Button variant="secondary" className="admin-more" disabled={loading}
        onClick={() => loadProducts(productCursor)}>Load more products</Button>}
    </section>

    <section aria-labelledby="pricing-discount">
      <div className="admin-panel-head"><h2 id="pricing-discount">New discount</h2></div>
      <form className="admin-pricing-form" onSubmit={applyDiscount} noValidate>
        <Input label="Name" value={form.name} maxLength={100} error={formErrors.name}
          onChange={event => setForm(previous => ({ ...previous, name: event.target.value }))} />
        <Input label="Discount percentage" inputMode="decimal" placeholder="20" value={form.percent} error={formErrors.rate}
          hint="For example 20 for 20% off."
          onChange={event => setForm(previous => ({ ...previous, percent: event.target.value }))} />
        <Input label="Starts (optional)" type="datetime-local" value={form.startsAt} error={formErrors.startsAt}
          hint="Leave empty to start now."
          onChange={event => setForm(previous => ({ ...previous, startsAt: event.target.value }))} />
        <Input label="Ends" type="datetime-local" value={form.endsAt} error={formErrors.endsAt}
          onChange={event => setForm(previous => ({ ...previous, endsAt: event.target.value }))} />
        {formErrors.productIds && <p className="ui-field-error" role="alert">{formErrors.productIds}</p>}
        <h3>Preview ({selected.length} selected)</h3>
        <Table columns={previewColumns} rows={preview} caption="New prices with this discount"
          emptyText="Tick products in the table above to see their new prices." />
        <Button type="submit" disabled={applying}>{applying ? "Applying…" : "Apply discount"}</Button>
      </form>
    </section>

    <section aria-labelledby="pricing-discounts">
      <div className="admin-panel-head"><h2 id="pricing-discounts">Discounts</h2></div>
      <ErrorState error={discountsRead.error} onRetry={() => loadDiscounts(0)} retrying={discountsRead.status === "loading"} />
      <Table columns={discountColumns} rows={discounts ?? []} loading={discountsRead.status === "loading" && !discounts}
        caption="Discounts, newest first" loadingLabel="Loading discounts…" emptyText="No discount yet." />
      {discountCursor !== null && discounts && <Button variant="secondary" className="admin-more" disabled={discountsRead.status === "loading"}
        onClick={() => loadDiscounts(discountCursor)}>Load more discounts</Button>}
    </section>
  </>;
}

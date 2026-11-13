// Clients for the two manager dashboards (SCRUM-124; contract: docs/api-contract-dashboards.md).
// Responses are checked before they are shown; nothing from an error body is forwarded.
const id = value => Number.isSafeInteger(value) && value > 0;
const count = value => Number.isSafeInteger(value) && value >= 0;
const amount = value => Number.isFinite(value);
const money = value => amount(value) && value >= 0;
const text = value => typeof value === "string";
const instant = value => text(value) && Number.isFinite(Date.parse(value));
const listOf = (value, check) => Array.isArray(value) && value.every(check);

const invoice = value => id(value?.orderId) && text(value.invoiceNumber) && instant(value.issuedAt)
  && money(value.total) && text(value.currencyCode);
const discount = value => id(value?.id) && text(value.name) && amount(value.rate) && value.rate >= 0 && value.rate <= 1
  && instant(value.startsAt) && instant(value.endsAt) && count(value.productCount);
const lowStock = value => id(value?.id) && text(value.sku) && text(value.name) && Number.isSafeInteger(value.stockQuantity);
const pendingDelivery = value => id(value?.id) && id(value.orderId) && text(value.status) && instant(value.createdAt);
const pendingComment = value => id(value?.id) && id(value.productId) && text(value.productName) && text(value.body)
  && instant(value.createdAt);

/** Net revenue may be negative: refunds in a period can exceed its payments. */
export function isSalesDashboard(value) {
  return instant(value?.from) && instant(value.to) && text(value.currencyCode)
    && money(value.grossRevenue) && money(value.refunds) && amount(value.netRevenue)
    && listOf(value.recentInvoices, invoice) && listOf(value.activeDiscounts, discount);
}

export function isProductManagementDashboard(value) {
  return listOf(value?.lowStockProducts, lowStock) && listOf(value.pendingDeliveries, pendingDelivery)
    && listOf(value.commentsWaitingForApproval, pendingComment);
}

/** The sales overview's period: the last `days` days up to now, as the half-open UTC range the API expects. */
export function salesPeriod(now = new Date(), days = 30) {
  const to = new Date(now.getTime());
  const from = new Date(to.getTime() - days * 24 * 60 * 60 * 1000);
  return { from: from.toISOString(), to: to.toISOString() };
}

async function getJson(path, validate, signal) {
  signal?.throwIfAborted();
  const response = await fetch(path, { signal, headers: { Accept: "application/json" } });
  if (!response.ok) throw { status: response.status, headers: response.headers };
  let data;
  try { data = await response.json(); } catch { throw { kind: "invalid-response" }; }
  if (!validate(data)) throw { kind: "invalid-response" };
  return data;
}

export function fetchSalesDashboard({ from, to, signal } = {}) {
  const query = new URLSearchParams({ from, to });
  return getJson(`/api/sales/dashboard?${query}`, isSalesDashboard, signal);
}

export function fetchProductManagementDashboard({ signal } = {}) {
  return getJson("/api/product-management/dashboard", isProductManagementDashboard, signal);
}

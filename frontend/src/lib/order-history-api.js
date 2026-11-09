// The order history client (SCRUM-76): GET /api/customer/orders, newest first, 20 per page.
// A response is checked before it is shown; nothing from an error body is forwarded.
const id = value => Number.isSafeInteger(value) && value > 0;
const money = value => Number.isFinite(value) && value >= 0;
const instant = value => value === null || (typeof value === "string" && Number.isFinite(Date.parse(value)));
const STATUSES = new Set(["Processing", "InTransit", "Delivered", "Cancelled"]);
const PAYMENTS = new Set(["Pending", "Confirmed", "Failed", "Refunded", "Unknown"]);

export function isOrderSummary(value) {
  return id(value?.orderId) && STATUSES.has(value.status) && PAYMENTS.has(value.paymentStatus)
    && instant(value.purchasedAt) && instant(value.deliveredAt)
    && Number.isSafeInteger(value.itemCount) && value.itemCount >= 0 && money(value.total)
    && ["Pending", "Ready"].includes(value.invoiceStatus)
    && (value.invoiceUrl === null || typeof value.invoiceUrl === "string");
}

export function isOrderHistoryPage(value) {
  return Array.isArray(value?.orders) && value.orders.every(isOrderSummary)
    && (value.nextBeforeId === null || id(value.nextBeforeId));
}

// beforeId is the nextBeforeId of the previous page (leave it out for the first page).
export async function fetchOrderHistory(beforeId = null, { signal } = {}) {
  if (beforeId !== null && !id(beforeId)) throw { status: 400 };
  signal?.throwIfAborted();
  const query = beforeId === null ? "" : `?beforeId=${beforeId}`;
  const response = await fetch(`/api/customer/orders${query}`, { signal, headers: { Accept: "application/json" } });
  if (!response.ok) throw { status: response.status, headers: response.headers };
  let data;
  try { data = await response.json(); } catch { throw { kind: "invalid-response" }; }
  if (!isOrderHistoryPage(data)) throw { kind: "invalid-response" };
  return data;
}

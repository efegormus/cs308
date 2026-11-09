import { parseCart } from "./cart-api.js";

// Purchase responses are validated before rendering. Never forward an API title/detail or payment input into errors.
const id = value => Number.isSafeInteger(value) && value > 0;
const money = value => Number.isFinite(value) && value >= 0;
const string = value => typeof value === "string";
const instant = value => value === null || (string(value) && Number.isFinite(Date.parse(value)));
const statuses = new Set(["Processing", "InTransit", "Delivered", "Cancelled"]);
const payments = new Set(["Pending", "Confirmed", "Failed", "Refunded", "Unknown"]);
const line = value => id(value?.productId) && string(value.name) && id(value.quantity)
  && money(value.unitPrice) && money(value.total);
const lines = value => Array.isArray(value) && value.every(line);
export const validQuoteToken = value => typeof value === "string" && /^[A-Fa-f0-9]{64}$/.test(value);
export const validAttemptKey = value => typeof value === "string" && /^[a-f0-9]{8}-(?:[a-f0-9]{4}-){3}[a-f0-9]{12}$/i.test(value);

export function isQuote(value) {
  return id(value?.cartId) && validQuoteToken(value.quoteToken) && value.currency === "TRY"
    && [value.name, value.taxId, value.email, value.address].every(string) && lines(value.items)
    && value.items.length > 0 && money(value.total);
}
export function isReceipt(value) {
  return id(value?.orderId) && (value.cartId === null || id(value.cartId)) && value.currency === "TRY"
    && statuses.has(value.status) && payments.has(value.paymentStatus) && instant(value.purchasedAt)
    && instant(value.deliveredAt) && [value.name, value.taxId, value.address].every(string)
    && lines(value.items) && money(value.total) && ["Pending", "Ready"].includes(value.invoiceStatus);
}
export function isDelivery(value) {
  return id(value?.id) && id(value.orderId) && id(value.customerId) && string(value.address)
    && statuses.has(value.status) && payments.has(value.paymentStatus) && instant(value.createdAt)
    && instant(value.deliveredAt) && typeof value.version === "string" && /^\d+$/.test(value.version)
    && money(value.total) && typeof value.canShip === "boolean" && typeof value.canDeliver === "boolean"
    && Array.isArray(value.items) && value.items.every(item => id(item?.productId) && string(item.name)
      && id(item.quantity) && money(item.total));
}
function isPage(value) {
  return Array.isArray(value?.items) && value.items.every(isDelivery)
    && (value.nextAfterId === null || id(value.nextAfterId));
}

async function request(path, { body, token, signal, missing = false } = {}, validate) {
  signal?.throwIfAborted();
  const response = await fetch(path, {
    method: body === undefined ? "GET" : "POST", signal,
    headers: { Accept: "application/json", ...(body === undefined ? {} : { "Content-Type": "application/json", "X-CSRF-TOKEN": token }) },
    ...(body === undefined ? {} : { body: JSON.stringify(body) })
  });
  if (missing && response.status === 404) return null;
  if (!response.ok) throw { status: response.status, headers: response.headers };
  let data;
  try { data = await response.json(); } catch { throw { kind: "invalid-response" }; }
  if (!validate(data)) throw { kind: "invalid-response" };
  return data;
}

export const fetchQuote = ({ signal } = {}) => request("/api/customer/checkout", { signal }, isQuote);
export function recoverPurchase(key, { signal } = {}) {
  if (!validAttemptKey(key)) throw { kind: "invalid-response" };
  return request(`/api/customer/checkout/attempts/${key}`, { signal, missing: true }, isReceipt);
}
export function fetchReceipt(orderId, { signal } = {}) {
  if (!id(orderId)) throw { status: 400 };
  return request(`/api/customer/orders/${orderId}`, { signal }, isReceipt);
}
export function submitCheckout(attempt, payment, token, { signal } = {}) {
  return request("/api/customer/checkout", {
    signal, token, body: {
      cartId: attempt.cartId, quoteToken: attempt.quoteToken, idempotencyKey: attempt.key,
      payment: { number: payment.number, expiryMonth: payment.expiryMonth, expiryYear: payment.expiryYear, securityCode: payment.securityCode }
    }
  }, isReceipt);
}
export function fetchDeliveries(afterId = 0, { signal } = {}) {
  return request(`/api/product-management/deliveries?afterId=${afterId}`, { signal }, data =>
    isPage(data) && (data.nextAfterId === null || data.nextAfterId > afterId));
}
export function transitionDelivery(delivery, status, token, { signal } = {}) {
  if (!id(delivery?.id) || !["InTransit", "Delivered"].includes(status)) throw { status: 400 };
  return request(`/api/product-management/deliveries/${delivery.id}/status`,
    { signal, token, body: { status, version: delivery.version } }, isDelivery);
}

export function mergeGuestCart(token, { signal } = {}) {
  return request("/api/cart/merge", { token, signal, body: {} }, data => parseCart(data?.cart) !== null
    && typeof data.merged === "boolean" && Array.isArray(data.conflicts)
    && data.conflicts.every(item => id(item?.productId) && string(item.name)
      && ["NotEnoughStock", "Unavailable", "CartFull"].includes(item.reason) && id(item.quantity)));
}

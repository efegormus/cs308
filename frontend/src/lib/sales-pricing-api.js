// Client for the sales manager's pricing and discount API (SCRUM-155; contract: docs/sales-pricing-api.md).
// Responses are checked before they are shown; nothing from an error body is forwarded. Writes carry the CSRF token.
const id = value => Number.isSafeInteger(value) && value > 0;
const text = value => typeof value === "string";
const money = value => Number.isFinite(value) && value >= 0;
const instant = value => text(value) && Number.isFinite(Date.parse(value));

export const DISCOUNT_STATUSES = Object.freeze(["Scheduled", "Active", "Expired", "Cancelled"]);

export const isSalesProduct = value => id(value?.id) && text(value.name) && text(value.model)
  && Number.isSafeInteger(value.stockQuantity) && typeof value.isActive === "boolean"
  && money(value.listPrice) && money(value.price) && value.price <= value.listPrice
  && Number.isFinite(value.discountRate) && value.discountRate >= 0 && value.discountRate < 1;

export const isDiscount = value => id(value?.id) && text(value.name) && Number.isFinite(value.rate)
  && value.rate > 0 && value.rate < 1 && instant(value.startsAt) && instant(value.endsAt)
  && DISCOUNT_STATUSES.includes(value.status) && (value.cancelledAt === null || instant(value.cancelledAt))
  && Array.isArray(value.productIds) && value.productIds.every(id);

/** A page of products; the cursor must move forward so "Load more" can never loop. */
export const isProductPage = (value, afterId = 0) => Array.isArray(value?.products) && value.products.length <= 50
  && value.products.every(product => isSalesProduct(product) && product.id > afterId)
  && (value.nextAfterId === null || (id(value.nextAfterId) && value.nextAfterId > afterId));

/** A page of discounts, newest first; the cursor must move backward. */
export const isDiscountPage = (value, beforeId = 0) => Array.isArray(value?.discounts) && value.discounts.length <= 50
  && value.discounts.every(discount => isDiscount(discount) && (beforeId === 0 || discount.id < beforeId))
  && (value.nextBeforeId === null || (id(value.nextBeforeId) && (beforeId === 0 || value.nextBeforeId < beforeId)));

async function request(path, { method = "GET", body, token, signal } = {}, validate) {
  signal?.throwIfAborted();
  const writes = method !== "GET";
  const response = await fetch(path, {
    method, signal,
    headers: { Accept: "application/json", ...(writes ? { "X-CSRF-TOKEN": token } : {}),
      ...(body === undefined ? {} : { "Content-Type": "application/json" }) },
    ...(body === undefined ? {} : { body: JSON.stringify(body) })
  });
  if (!response.ok) throw { status: response.status, headers: response.headers };
  let data;
  try { data = await response.json(); } catch { throw { kind: "invalid-response" }; }
  if (!validate(data)) throw { kind: "invalid-response" };
  return data;
}

export function fetchSalesProducts({ afterId = 0, signal } = {}) {
  if (!Number.isSafeInteger(afterId) || afterId < 0) throw { status: 400 };
  return request(`/api/sales/products?afterId=${afterId}`, { signal }, data => isProductPage(data, afterId));
}

/** Changes the list price. Resolves with the updated product only after the server answered 200. */
export function setProductPrice(productId, price, token, { signal } = {}) {
  if (!id(productId) || !(price > 0)) throw { status: 400 };
  return request(`/api/sales/products/${productId}/price`, { method: "PUT", body: { price }, token, signal },
    data => isSalesProduct(data) && data.id === productId && data.listPrice === price);
}

export function fetchDiscounts({ beforeId = 0, signal } = {}) {
  if (!Number.isSafeInteger(beforeId) || beforeId < 0) throw { status: 400 };
  return request(`/api/sales/discounts?beforeId=${beforeId}`, { signal }, data => isDiscountPage(data, beforeId));
}

/** Creates a campaign from a body built by buildDiscountRequest. Resolves with the campaign on 201. */
export function createDiscount(body, token, { signal } = {}) {
  return request("/api/sales/discounts", { method: "POST", body, token, signal },
    data => isDiscount(data) && data.rate === body.rate);
}

export function cancelDiscount(discountId, token, { signal } = {}) {
  if (!id(discountId)) throw { status: 400 };
  return request(`/api/sales/discounts/${discountId}/cancel`, { method: "POST", token, signal },
    data => isDiscount(data) && data.id === discountId);
}

// Logic of the product manager's Stock screen (SCRUM-78; API: docs/catalog-management.md, Stock). Pure, so Node can test it.

import { LIMITS, parseStock } from "./catalog-management.js";

// The dashboard's low-stock threshold (docs/api-contract-dashboards.md): an active product with 5 or fewer.
export const LOW_STOCK_THRESHOLD = 5;

export const isLowStock = product => product.isActive && product.stockQuantity <= LOW_STOCK_THRESHOLD;

/**
 * What the row's field holds: a whole number from 0 up to the limit. A blank, a minus sign or a fraction is not a count,
 * so the stock can never be sent below zero from here either. null when the text is not a valid count.
 */
export function parseStockInput(text) {
  return parseStock(text);
}

/** The request for one row: the new count and the count the manager saw, or the message to show under the field. */
export function buildStockRequest(product, draft) {
  const quantity = parseStockInput(draft);
  if (quantity === null) return { ok: false, error: `Use a whole number from 0 to ${LIMITS.maxStock}. The stock cannot go below zero.` };
  if (quantity === product.stockQuantity) return { ok: false, error: "This is already the current stock." };
  return { ok: true, request: { quantity, expectedQuantity: product.stockQuantity } };
}

/** Products shown on the screen: the search matches name, model or SKU; low stock first, then by name. */
export function visibleStockProducts(products, { search = "", lowOnly = false } = {}) {
  const term = search.trim().toLowerCase();
  return products
    .filter(product => !term || [product.name, product.model, product.sku].some(value => value.toLowerCase().includes(term)))
    .filter(product => !lowOnly || isLowStock(product))
    .sort((a, b) => Number(isLowStock(b)) - Number(isLowStock(a)) || a.stockQuantity - b.stockQuantity
      || a.name.localeCompare(b.name, "en") || a.id - b.id);
}

/** A short label for the stock column. */
export function stockLabel(product) {
  if (product.stockQuantity === 0) return "Out of stock";
  return isLowStock(product) ? "Low" : "In stock";
}

/** The sentence for a failed stock change. 409 means the count changed while the manager looked at the old one. */
export function stockFailureMessage(error) {
  switch (error?.status) {
    case 400: return "The server did not accept this count. Use a whole number from 0.";
    case 401: return "Your session expired. Log in again.";
    case 403: return "Only a product manager can change the stock.";
    case 404: return "This product no longer exists. The list was refreshed.";
    case 409: return "The stock changed in the meantime. The row shows the new count. Check it and save again.";
    default: return error?.kind === "invalid-response"
      ? "The server answered with something we could not read. The count on screen was not changed."
      : "We couldn't reach the server. Please try again.";
  }
}

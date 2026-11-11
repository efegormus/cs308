// The cart API client (SCRUM-49, contract in docs/api-contract-cart.md). Guests and customers use the same routes;
// the guest cart is identified by an HttpOnly cookie the browser sends by itself.

// The most one request may set, as in the API (CartEndpoints.MaxQuantityPerRequest).
export const MAX_LINE_QUANTITY = 99;

// Sent on window after every successful change to the cart, so the header's item count follows (SCRUM-123).
export const CART_CHANGED_EVENT = "cart-changed";

export class CartError extends Error {
  constructor(message, status) {
    super(message);
    this.name = "CartError";
    this.status = status; // The HTTP status, or undefined when the response itself was unusable.
  }
}

function isMoney(value) {
  return Number.isFinite(value) && value >= 0;
}

function isLine(line) {
  return Number.isInteger(line?.productId) && typeof line.name === "string"
    && isMoney(line.unitPrice) && Number.isInteger(line.quantity) && line.quantity > 0
    && Number.isInteger(line.stockQuantity) && line.stockQuantity >= 0
    && isMoney(line.lineTotal) && typeof line.available === "boolean";
}

// Returns the cart when the body has the documented shape and null otherwise.
export function parseCart(body) {
  if (!Array.isArray(body?.items) || !body.items.every(isLine)) return null;
  if (!Number.isInteger(body.itemCount) || !isMoney(body.total)) return null;
  return body;
}

async function sendCartRequest(path, init, signal) {
  const response = await fetch(path, { ...init, signal, headers: { Accept: "application/json", ...init.headers } });
  if (!response.ok) throw new CartError("Cart request failed.", response.status);
  let body;
  try {
    body = await response.json();
  } catch {
    throw new CartError("Invalid cart response.");
  }
  const cart = parseCart(body);
  if (!cart) throw new CartError("Invalid cart response.");
  if (init.method !== "GET") globalThis.window?.dispatchEvent(new Event(CART_CHANGED_EVENT));
  return cart;
}

export function fetchCart({ signal } = {}) {
  return sendCartRequest("/api/cart", { method: "GET" }, signal);
}

// Removes the whole line. A product that is not in the cart is not an error. csrfToken comes from useAuth().
export function removeCartItem(productId, csrfToken, { signal } = {}) {
  if (!Number.isInteger(productId) || productId <= 0) throw new CartError("Invalid product.");
  return sendCartRequest(`/api/cart/items/${productId}`, {
    method: "DELETE", headers: { "X-CSRF-TOKEN": csrfToken }
  }, signal);
}

// Sets the quantity of a line that is already in the cart (PUT /api/cart/items/{productId}). 404 means the line is
// gone, 409 means the stock changed. The minus and plus buttons of the cart page use it.
export function setCartItemQuantity(productId, quantity, csrfToken, { signal } = {}) {
  if (!Number.isInteger(productId) || productId <= 0) throw new CartError("Invalid product.");
  if (!Number.isInteger(quantity) || quantity < 1 || quantity > MAX_LINE_QUANTITY) throw new CartError("Invalid quantity.");
  return sendCartRequest(`/api/cart/items/${productId}`, {
    method: "PUT", headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrfToken },
    body: JSON.stringify({ quantity })
  }, signal);
}

// Adds units of a product (POST /api/cart/items). A product that is already in the cart gets more units. The server
// answers 404 for an unknown or inactive product, 409 when the stock is too low or the cart is full, 403 for staff.
export function addCartItem(productId, quantity, csrfToken, { signal } = {}) {
  if (!Number.isInteger(productId) || productId <= 0) throw new CartError("Invalid product.");
  if (!Number.isInteger(quantity) || quantity < 1 || quantity > MAX_LINE_QUANTITY) throw new CartError("Invalid quantity.");
  return sendCartRequest("/api/cart/items", {
    method: "POST", headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrfToken },
    body: JSON.stringify({ productId, quantity })
  }, signal);
}

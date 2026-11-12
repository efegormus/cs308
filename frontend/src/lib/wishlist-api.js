import { isValidProduct } from "./catalog-api.js";

export class WishlistError extends Error {
  constructor(status) {
    super("The wishlist request failed.");
    this.name = "WishlistError";
    this.status = status;
  }
}

export function isWishlistItem(value) {
  return Number.isSafeInteger(value?.productId) && value.productId > 0
    && typeof value.addedAt === "string" && Number.isFinite(Date.parse(value.addedAt))
    && isValidProduct(value.product);
}

export async function fetchWishlist({ signal } = {}) {
  const response = await fetch("/api/customer/wishlist", {
    signal, headers: { Accept: "application/json" }
  });
  if (!response.ok) throw new WishlistError(response.status);
  let items;
  try { items = await response.json(); } catch { throw new WishlistError(); }
  if (!Array.isArray(items) || !items.every(isWishlistItem)) throw new WishlistError();
  return items;
}

async function changeWishlist(method, productId, csrfToken, { signal } = {}) {
  if (!Number.isSafeInteger(productId) || productId <= 0) throw new WishlistError(400);
  const response = await fetch(`/api/customer/wishlist/${productId}`, {
    method, signal, headers: { Accept: "application/json", "X-CSRF-TOKEN": csrfToken }
  });
  if (!response.ok) throw new WishlistError(response.status);
}

export const addWishlistItem = (productId, csrfToken, options) =>
  changeWishlist("POST", productId, csrfToken, options);

export const removeWishlistItem = (productId, csrfToken, options) =>
  changeWishlist("DELETE", productId, csrfToken, options);

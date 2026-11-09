import { MAX_LINE_QUANTITY } from "./cart-api.js";

// The most units the product page offers in one click: what is in stock, but never more than the API accepts.
export function maxAddQuantity(stockQuantity) {
  if (!Number.isInteger(stockQuantity) || stockQuantity < 1) return 0;
  return Math.min(stockQuantity, MAX_LINE_QUANTITY);
}

// Turns whatever the quantity box holds into a whole number between 1 and max (1 when it is empty or not a number).
export function clampQuantity(value, max) {
  const number = Math.trunc(Number(value));
  if (!Number.isFinite(number) || number < 1) return 1;
  return Math.min(number, Math.max(max, 1));
}

// A short sentence for a failed "Add to cart", by HTTP status. Nothing from the server body is shown.
export function addToCartError(status) {
  if (status === 403) return "Staff accounts cannot shop. Please use a customer account.";
  if (status === 404) return "This product is no longer available.";
  if (status === 409) return "Not enough stock for that amount, or your cart is full. Check your cart and try again.";
  if (status === 400) return "Your session expired. Please try again.";
  return "The product could not be added. Please try again.";
}

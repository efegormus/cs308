// Text and order for the wishlist page. Pure helpers, so Node can test them.

export function wishlistCountLabel(count) {
  if (count === 0) return "Your wishlist is empty.";
  return `${count} ${count === 1 ? "product" : "products"} saved.`;
}

// The newest product first, because that is the one a customer just saved. A tie keeps the API order.
export function newestFirst(items) {
  return items
    .map((item, index) => ({ item, index, time: Date.parse(item.addedAt) }))
    .sort((a, b) => (Number.isFinite(b.time) ? b.time : 0) - (Number.isFinite(a.time) ? a.time : 0) || a.index - b.index)
    .map(entry => entry.item);
}

export function removeButtonLabel(removing) {
  return removing ? "Removing…" : "Remove";
}

// A remove error for the customer. 401 means the session ended; anything else is a retry.
export function removeErrorMessage(status) {
  return status === 401
    ? "Your session expired. Log in again to manage your wishlist."
    : "We couldn't remove this product. Please try again.";
}

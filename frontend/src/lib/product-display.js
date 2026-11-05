// How a product is shown in the lists. Pure helpers, so Node can test them. Ported from wwwroot/js/products.js.

const priceFormat = new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY" });

export function formatPrice(amount) {
  return priceFormat.format(amount);
}

// Only http and https image URLs are used. Anything else (javascript:, data:, a value that is not a URL) returns null,
// and the card shows its placeholder. A relative URL is resolved against base, which is the current page by default.
export function safeImageUrl(imageUrl, base = globalThis.location?.href) {
  if (typeof imageUrl !== "string" || imageUrl === "") return null;
  let url;
  try {
    url = new URL(imageUrl, base);
  } catch {
    return null;
  }
  return ["http:", "https:"].includes(url.protocol) ? url.href : null;
}

export function stockLabel(stockQuantity) {
  const inStock = stockQuantity > 0;
  return { inStock, text: inStock ? `In stock · ${stockQuantity} available` : "Out of stock" };
}

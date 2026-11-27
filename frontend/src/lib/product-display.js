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

// The stock line of a product card: the status only. The product page still shows the count (stockLabel).
export function stockStatus(stockQuantity) {
  const inStock = stockQuantity > 0;
  return { inStock, text: inStock ? "In stock" : "Out of stock" };
}

// The spec row of a product card: the brand, or else the first line of the description (CSS cuts it with "…").
// null when the product has neither.
export function specRow(product) {
  if (product.brandName) return { kind: "brand", text: product.brandName };
  const firstLine = typeof product.description === "string" ? product.description.trim().split(/\r?\n/)[0].trim() : "";
  return firstLine ? { kind: "description", text: firstLine } : null;
}

// SCRUM-126: the rating line of a product card. The API sends averageRating (null when nobody rated) and ratingCount.
// Returns null for data that is not a sane rating, so the card shows "No ratings yet" instead of a wrong number.
export function ratingSummary(product) {
  const average = product?.averageRating;
  const count = product?.ratingCount;
  if (!Number.isSafeInteger(count) || count < 0) return null;
  if (count === 0) return { rated: false, count: 0, average: null, text: "No ratings yet", label: "No ratings yet" };
  if (!Number.isFinite(average) || average < 1 || average > 5) return null;
  const noun = count === 1 ? "rating" : "ratings";
  return { rated: true, count, average, text: `${average.toFixed(1)} (${count})`,
    label: `Rated ${average.toFixed(1)} out of 5 from ${count} ${noun}` };
}

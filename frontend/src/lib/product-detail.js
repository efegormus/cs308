// Pure helpers for the product detail page (SCRUM-110), so Node can test them. Ported from wwwroot/js/product.js.

// The id comes from the address bar (/product?id=3), so it is validated before it is used.
// Only plain decimal digits are accepted: no sign, no decimals, no spaces, within JavaScript’s safe integer range.
export function parseProductId(raw) {
  if (typeof raw !== "string" || !/^\d{1,16}$/.test(raw)) return null;
  const id = Number(raw);
  return Number.isSafeInteger(id) && id > 0 ? id : null;
}

// The rows of the specification list. A row without a value is left out.
export function specRows(product) {
  return [
    ["Model", product.model],
    ["Warranty", product.warrantyStatus],
    ["Distributor", product.distributorInformation]
  ].filter(([, value]) => typeof value === "string" && value.trim() !== "")
    .map(([label, value]) => ({ label, value }));
}

// The loaded value of a detail request. fetchProduct returns null for an unknown product, and the request
// hook only accepts a non-null value, so the product is wrapped.
export function isLoadedProduct(data) {
  return data !== null && typeof data === "object" && "product" in data;
}

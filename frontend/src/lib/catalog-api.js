// The catalog API client. Same-origin requests to the canonical list APIs (SCRUM-43 lists, SCRUM-50 search,
// SCRUM-51 sorting): no mock inventory and no separate CORS origin. Ported from OnlineStore/wwwroot/js/catalog-api.js.

export const SORT_OPTIONS = ["price", "popularity"];
export const ORDER_OPTIONS = ["asc", "desc"];

export class CatalogError extends Error {
  constructor(message, status) {
    super(message);
    this.name = "CatalogError";
    this.status = status; // The HTTP status, or undefined when the response itself was unusable.
  }
}

// The query for GET /api/products. Empty values are left out, so a blank search means no search, as on the server.
export function productsPath({ categoryId, search, sort, order } = {}) {
  const query = new URLSearchParams();
  if (categoryId !== undefined && categoryId !== null) query.set("categoryId", String(categoryId));
  const text = typeof search === "string" ? search.trim() : "";
  if (text) query.set("search", text);
  if (sort) query.set("sort", sort);
  if (order) query.set("order", order);
  const queryString = query.toString();
  return queryString ? `/api/products?${queryString}` : "/api/products";
}

async function fetchJson(path, signal) {
  const response = await fetch(path, { signal, headers: { Accept: "application/json" } });
  if (!response.ok) throw new CatalogError("Catalog request failed.", response.status);
  try {
    return await response.json();
  } catch {
    throw new CatalogError("Invalid catalog response.");
  }
}

async function fetchList(path, signal) {
  const rows = await fetchJson(path, signal);
  if (!Array.isArray(rows)) throw new CatalogError("Invalid catalog response.");
  return rows;
}

function isValidProduct(product) {
  return Number.isInteger(product?.id) && typeof product.name === "string"
    && Number.isFinite(product.price) && product.price >= 0
    && Number.isInteger(product.stockQuantity) && product.stockQuantity >= 0;
}

export async function fetchCategories({ signal } = {}) {
  return fetchList("/api/categories", signal);
}

// The products of GET /api/products. It accepts the same filters as productsPath.
export async function fetchProducts({ signal, ...filters } = {}) {
  return fetchList(productsPath(filters), signal);
}

// Every category with its products. With filters, categories without a match stay in the list with no products.
export async function fetchCatalog({ signal, ...filters } = {}) {
  const [categories, products] = await Promise.all([
    fetchCategories({ signal }), fetchProducts({ ...filters, signal })
  ]);
  const grouped = new Map();
  for (const category of categories) {
    if (!Number.isInteger(category?.id) || typeof category.name !== "string" || grouped.has(category.id))
      throw new CatalogError("Invalid category response.");
    grouped.set(category.id, { ...category, products: [] });
  }
  for (const product of products) {
    const category = isValidProduct(product) ? grouped.get(product.categoryId) : undefined;
    // A concurrent category change can make two responses inconsistent. Fail so the caller retries, instead of silently losing items.
    if (!category) throw new CatalogError("Invalid product response.");
    category.products.push(product);
  }
  return [...grouped.values()];
}

// One product for the detail page. Returns null when the product does not exist (404).
export async function fetchProduct(id, { signal } = {}) {
  const response = await fetch(`/api/products/${encodeURIComponent(id)}`,
    { signal, headers: { Accept: "application/json" } });
  if (response.status === 404) return null;
  if (!response.ok) throw new CatalogError("Product request failed.", response.status);
  let product;
  try {
    product = await response.json();
  } catch {
    throw new CatalogError("Invalid product response.");
  }
  if (!isValidProduct(product)) throw new CatalogError("Invalid product response.");
  return product;
}

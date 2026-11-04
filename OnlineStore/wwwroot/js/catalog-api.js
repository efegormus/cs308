// Same-origin requests use the canonical SCRUM-43 list APIs. No mock inventory or separate CORS origin.
export async function fetchCategories(signal) {
  return fetchList("/api/categories", signal);
}

async function fetchList(path, signal) {
  const response = await fetch(path, { signal, headers: { Accept: "application/json" } });
  if (!response.ok) throw new Error("Catalog request failed.");
  const rows = await response.json();
  if (!Array.isArray(rows)) throw new Error("Invalid catalog response.");
  return rows;
}

export async function fetchCatalog(signal) {
  const [categories, products] = await Promise.all([
    fetchCategories(signal), fetchList("/api/products", signal)
  ]);
  const grouped = new Map();
  for (const category of categories) {
    if (!Number.isInteger(category.id) || typeof category.name !== "string" || grouped.has(category.id))
      throw new Error("Invalid category response.");
    grouped.set(category.id, { ...category, products: [] });
  }
  for (const product of products) {
    const category = grouped.get(product.categoryId);
    // A concurrent category change can make two responses inconsistent. Retry instead of silently losing items.
    if (!category || !Number.isInteger(product.id) || typeof product.name !== "string"
        || !Number.isFinite(product.price) || product.price < 0
        || !Number.isInteger(product.stockQuantity) || product.stockQuantity < 0)
      throw new Error("Invalid product response.");
    category.products.push(product);
  }
  return [...grouped.values()];
}

// One product for the detail page. Returns null when the product does not exist (404).
export async function fetchProduct(id, signal) {
  const response = await fetch(`/api/products/${encodeURIComponent(id)}`,
    { signal, headers: { Accept: "application/json" } });
  if (response.status === 404) return null;
  if (!response.ok) throw new Error("Product request failed.");
  const product = await response.json();
  if (!Number.isInteger(product.id) || typeof product.name !== "string"
      || !Number.isFinite(product.price) || product.price < 0
      || !Number.isInteger(product.stockQuantity) || product.stockQuantity < 0)
    throw new Error("Invalid product response.");
  return product;
}

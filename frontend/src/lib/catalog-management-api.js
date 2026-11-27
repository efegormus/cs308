// Client for the product manager's catalog API (SCRUM-77; contract: docs/catalog-management.md).
// Responses are checked before they are shown; nothing from an error body is forwarded. Writes carry the CSRF token.
const id = value => Number.isSafeInteger(value) && value > 0;
const text = value => typeof value === "string";

export const isManagedCategory = value => id(value?.id) && (value.parentId === null || id(value.parentId))
  && text(value.name) && text(value.slug) && (value.description === null || text(value.description))
  && typeof value.isActive === "boolean" && Number.isSafeInteger(value.productCount) && value.productCount >= 0
  && Number.isSafeInteger(value.childCount) && value.childCount >= 0;

export const isManagedProduct = value => id(value?.id) && id(value.categoryId) && text(value.categoryName)
  && (value.brandId === null || id(value.brandId)) && text(value.sku) && text(value.name) && text(value.model)
  && text(value.serialNumber) && text(value.description) && Number.isFinite(value.price) && value.price >= 0
  && Number.isSafeInteger(value.stockQuantity) && value.stockQuantity >= 0 && text(value.warrantyStatus)
  && text(value.distributorName) && text(value.distributorContact) && typeof value.isActive === "boolean";

/** A page of products; the cursor must move forward so "Load more" can never loop. */
export const isProductPage = (value, afterId = 0) => Array.isArray(value?.products) && value.products.length <= 50
  && value.products.every(product => isManagedProduct(product) && product.id > afterId)
  && (value.nextAfterId === null || (id(value.nextAfterId) && value.nextAfterId > afterId));

async function request(path, { method = "GET", body, token, signal } = {}, validate) {
  signal?.throwIfAborted();
  const writes = method !== "GET";
  const response = await fetch(path, {
    method, signal,
    headers: { Accept: "application/json", ...(writes ? { "X-CSRF-TOKEN": token } : {}),
      ...(body === undefined ? {} : { "Content-Type": "application/json" }) },
    ...(body === undefined ? {} : { body: JSON.stringify(body) })
  });
  if (!response.ok) throw { status: response.status, headers: response.headers };
  if (response.status === 204) return null;
  let data;
  try { data = await response.json(); } catch { throw { kind: "invalid-response" }; }
  if (!validate(data)) throw { kind: "invalid-response" };
  return data;
}

const ROOT = "/api/product-management";

export function fetchCategories({ signal } = {}) {
  return request(`${ROOT}/categories`, { signal }, data => Array.isArray(data) && data.length <= 500 && data.every(isManagedCategory));
}

export function createCategory(body, token, { signal } = {}) {
  return request(`${ROOT}/categories`, { method: "POST", body, token, signal }, data => isManagedCategory(data) && data.name === body.name);
}

export function editCategory(categoryId, body, token, { signal } = {}) {
  if (!id(categoryId)) throw { status: 400 };
  return request(`${ROOT}/categories/${categoryId}`, { method: "PUT", body, token, signal },
    data => isManagedCategory(data) && data.id === categoryId);
}

/** Resolves with null on 204. A category that still has products or subcategories answers 409. */
export function deleteCategory(categoryId, token, { signal } = {}) {
  if (!id(categoryId)) throw { status: 400 };
  return request(`${ROOT}/categories/${categoryId}`, { method: "DELETE", token, signal }, () => true);
}

export function fetchProducts({ afterId = 0, signal } = {}) {
  if (!Number.isSafeInteger(afterId) || afterId < 0) throw { status: 400 };
  return request(`${ROOT}/products?afterId=${afterId}`, { signal }, data => isProductPage(data, afterId));
}

export function createProduct(body, token, { signal } = {}) {
  return request(`${ROOT}/products`, { method: "POST", body, token, signal },
    data => isManagedProduct(data) && data.sku === body.sku && data.price === body.price);
}

export function editProduct(productId, body, token, { signal } = {}) {
  if (!id(productId)) throw { status: 400 };
  return request(`${ROOT}/products/${productId}`, { method: "PUT", body, token, signal },
    data => isManagedProduct(data) && data.id === productId && data.isActive === body.isActive);
}

/** Takes the product off sale (the row stays, so past orders keep their product). */
export function removeProduct(productId, token, { signal } = {}) {
  if (!id(productId)) throw { status: 400 };
  return request(`${ROOT}/products/${productId}`, { method: "DELETE", token, signal },
    data => isManagedProduct(data) && data.id === productId && data.isActive === false);
}

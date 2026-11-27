// Logic of the product manager's Catalog screen (SCRUM-77; API: docs/catalog-management.md). Pure, so Node can test it.
// The checks below match the server's, so most mistakes are caught before the request. The server checks everything again.

export const LIMITS = Object.freeze({
  sku: 64, name: 200, model: 100, serialNumber: 100, description: 5000, warrantyStatus: 100,
  distributorName: 150, distributorContact: 500, categoryName: 100, categoryDescription: 1000,
  maxStock: 1_000_000, maxPrice: 9_999_999.99
});

export const EMPTY_PRODUCT_FORM = Object.freeze({
  categoryId: "", brandId: null, sku: "", name: "", model: "", serialNumber: "", description: "", price: "",
  stockQuantity: "", warrantyStatus: "", distributorName: "", distributorContact: ""
});

/** The edit form starts from the product as it is now. The brand has no field on the form, so it is carried along. */
export function productToForm(product) {
  return {
    categoryId: String(product.categoryId), brandId: product.brandId ?? null, sku: product.sku, name: product.name,
    model: product.model, serialNumber: product.serialNumber, description: product.description,
    price: String(product.price), stockQuantity: String(product.stockQuantity), warrantyStatus: product.warrantyStatus,
    distributorName: product.distributorName, distributorContact: product.distributorContact
  };
}

/** A price such as "1999.90" or "1999,90", from 0 up to the limit with at most 2 decimals. Anything else is null. */
export function parsePrice(text) {
  const value = String(text ?? "").trim().replace(",", ".");
  if (!/^\d{1,7}(\.\d{1,2})?$/.test(value)) return null;
  const price = Number(value);
  return price <= LIMITS.maxPrice ? price : null;
}

/** A whole stock count from 0 up to the limit. */
export function parseStock(text) {
  const value = String(text ?? "").trim();
  if (!/^\d{1,7}$/.test(value)) return null;
  const stock = Number(value);
  return stock <= LIMITS.maxStock ? stock : null;
}

const required = (value, max) => {
  const text = String(value ?? "").trim();
  return text.length >= 1 && text.length <= max ? text : null;
};

const REQUIRED_TEXT = [
  ["sku", "SKU", LIMITS.sku], ["name", "Name", LIMITS.name], ["model", "Model", LIMITS.model],
  ["serialNumber", "Serial number", LIMITS.serialNumber], ["description", "Description", LIMITS.description],
  ["warrantyStatus", "Warranty", LIMITS.warrantyStatus], ["distributorName", "Distributor", LIMITS.distributorName],
  ["distributorContact", "Distributor contact", LIMITS.distributorContact]
];

/**
 * Turns the form into the request body, or the errors to show under the fields. The stock count is sent when a product
 * is created and never when it is edited (the server has no such field there; stock has its own screen, SCRUM-78).
 */
export function buildProductRequest(form, { editing = false, isActive = true } = {}) {
  const errors = {};
  const body = {};
  const categoryId = Number(form.categoryId);
  if (!Number.isSafeInteger(categoryId) || categoryId <= 0) errors.categoryId = "Choose a category.";
  else body.categoryId = categoryId;
  body.brandId = form.brandId ?? null;
  for (const [field, label, max] of REQUIRED_TEXT) {
    const value = required(form[field], max);
    if (value === null) errors[field] = `Enter ${label.toLowerCase()} (1 to ${max} characters).`;
    else body[field] = value;
  }
  if (!errors.sku && /\s/.test(body.sku)) errors.sku = "The SKU cannot contain spaces.";
  const price = parsePrice(form.price);
  if (price === null) errors.price = "Use a price from 0 with at most 2 decimals.";
  else body.price = price;
  if (editing) body.isActive = isActive;
  else {
    const stock = parseStock(form.stockQuantity);
    if (stock === null) errors.stockQuantity = `Use a whole number from 0 to ${LIMITS.maxStock}.`;
    else body.stockQuantity = stock;
  }
  return Object.keys(errors).length === 0 ? { ok: true, request: body } : { ok: false, errors };
}

export function buildCategoryRequest({ name, description, parentId }, { editing = false } = {}) {
  const errors = {};
  const cleanName = required(name, LIMITS.categoryName);
  if (cleanName === null) errors.name = `Enter a name (1 to ${LIMITS.categoryName} characters).`;
  const cleanDescription = String(description ?? "").trim();
  if (cleanDescription.length > LIMITS.categoryDescription) errors.description = `Use at most ${LIMITS.categoryDescription} characters.`;
  if (Object.keys(errors).length > 0) return { ok: false, errors };
  const request = { name: cleanName, description: cleanDescription || null };
  // A category keeps its place in the tree, so the parent is only sent when it is created.
  if (!editing) request.parentId = parentId ? Number(parentId) : null;
  return { ok: true, request };
}

/** The sentence for a failed request. Nothing from the server's error body is shown. */
export function catalogFailureMessage(error, subject = "item") {
  switch (error?.status) {
    case 400: return "The server did not accept these values. Check the fields and try again.";
    case 401: return "Your session expired. Log in again.";
    case 403: return "Only a product manager can change the catalog.";
    case 404: return `This ${subject} no longer exists. The list was refreshed.`;
    case 409: return subject === "category"
      ? "This category name is taken, or the category still has products or subcategories."
      : "This SKU is already used by another product.";
    default: return error?.kind === "invalid-response"
      ? "The server answered with something we could not read. Nothing was changed on screen."
      : "We couldn't reach the server. Please try again.";
  }
}

/** Parent name for the categories table, "—" for a top-level category. */
export function parentName(category, categories) {
  if (category.parentId === null) return "—";
  return categories.find(other => other.id === category.parentId)?.name ?? `#${category.parentId}`;
}

/** Categories as the select shows them: the tree order is not needed, a parent is shown as "Parent / Child". */
export function categoryOptions(categories) {
  const byId = new Map(categories.map(category => [category.id, category]));
  const path = category => {
    const parent = byId.get(category.parentId);
    return parent ? `${path(parent)} / ${category.name}` : category.name;
  };
  return categories.map(category => ({ id: category.id, label: path(category) }))
    .sort((a, b) => a.label.localeCompare(b.label, "en"));
}

/** Adds or replaces rows by id without changing the order of the rows already shown. */
export function mergeById(previous, next) {
  const map = new Map((previous ?? []).map(item => [item.id, item]));
  for (const item of next) map.set(item.id, item);
  return [...map.values()];
}

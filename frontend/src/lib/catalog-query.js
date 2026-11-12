const FILTER_KEYS = ["categoryId", "brandId", "minPrice", "maxPrice", "search", "sort", "order"];

export function catalogFiltersFromSearchParams(params) {
  const filters = Object.fromEntries(FILTER_KEYS.map(key => [key, params.get(key) ?? ""]));
  return { ...filters, page: params.get("page") ?? "1" };
}

export function updateCatalogSearchParams(params, name, value, resetPage = true) {
  const next = new URLSearchParams(params);
  if (value === "" || value == null) next.delete(name);
  else next.set(name, value);
  if (resetPage && name !== "page") next.delete("page");
  return next;
}

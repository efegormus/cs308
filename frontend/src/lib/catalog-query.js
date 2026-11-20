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

// "Clear filters" on the empty-results state: drops every filter, the sort and the page, and nothing else in the URL
// (a #category hash is not part of the query and stays with the location).
export function clearCatalogSearchParams(params) {
  const next = new URLSearchParams(params);
  for (const key of [...FILTER_KEYS, "page"]) next.delete(key);
  return next;
}

export function hasCatalogFilters({ search = "", categoryId = "", brandId = "", minPrice = "", maxPrice = "" }) {
  return Boolean(String(search).trim() || categoryId || brandId || minPrice || maxPrice);
}

// The sentence under the controls, so the order in use is never a guess. It follows the API: price ascending and
// popularity descending are the defaults, and ties are broken by name.
export function sortDescription(sort, order) {
  if (sort === "price") return order === "desc" ? "Sorted by price, highest first." : "Sorted by price, lowest first.";
  if (sort === "popularity") {
    return order === "asc" ? "Sorted by popularity (units sold), least sold first."
      : "Sorted by popularity (units sold), best sellers first.";
  }
  // The API ignores `order` when no sort is chosen: the list is always by name, A to Z.
  return "Sorted by name, A to Z.";
}

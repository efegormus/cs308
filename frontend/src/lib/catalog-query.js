import { categoryIdFromHash } from "./category-display.js";

// inStock=1 is the "In stock only" box of /products (SCRUM-165); the API has no such parameter, the page applies it.
const FILTER_KEYS = ["categoryId", "brandId", "minPrice", "maxPrice", "search", "sort", "order", "inStock"];

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

export function hasCatalogFilters({ search = "", categoryId = "", brandId = "", minPrice = "", maxPrice = "", inStock = "" }) {
  return Boolean(String(search).trim() || categoryId || brandId || minPrice || maxPrice || isInStockOnly(inStock));
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

// Category links request the selected inventory, independent of the current global page.
export function categoryProductsPath(categoryId, search = "") {
  if (!Number.isSafeInteger(categoryId) || categoryId <= 0) throw new RangeError("Invalid category ID");
  const query = updateCatalogSearchParams(new URLSearchParams(search), "categoryId", String(categoryId));
  return `/products?${query}`;
}

// --- SCRUM-165: the /products sidebar, chips and the header search ---

/** The same limit as GET /api/products (ProductEndpoints.MaxSearchLength). */
export const MAX_SEARCH_LENGTH = 100;

/** Whether the inStock URL value turns "In stock only" on. Only "1" does. */
export function isInStockOnly(value) {
  return value === "1";
}

/** Removes the given URL parameters (a chip's `remove` list) and the page; everything else stays. */
export function removeCatalogParams(params, names) {
  const next = new URLSearchParams(params);
  for (const name of [...names, "page"]) next.delete(name);
  return next;
}

/**
 * An old /products#category-<id> link (saved links, the header's Categories panel before SCRUM-165) as the query it
 * means today:
 * the query string with categoryId set, or null when the hash is not a category link or categoryId is already set.
 * The page replaces the URL with it, so the category is chosen and the hash is gone.
 */
export function queryForCategoryHash(search, hash) {
  const id = categoryIdFromHash(hash);
  const params = new URLSearchParams(search);
  if (id === null || params.has("categoryId")) return null;
  params.set("categoryId", String(id));
  params.delete("page");
  return params.toString();
}

function priceValue(text) {
  const value = String(text ?? "").trim().replace(",", ".");
  if (value === "") return "";
  const number = Number(value);
  return Number.isFinite(number) && number >= 0 ? String(number) : null;
}

/**
 * The query after "Apply" (or Enter) in the price box. Empty fields remove their bound. Returns { params } or
 * { error } with the message to show, so GET /api/products never gets a range it answers 400 to.
 */
export function applyPriceRange(params, minText, maxText) {
  const min = priceValue(minText);
  const max = priceValue(maxText);
  if (min === null || max === null) return { error: "Enter a price of 0 or more." };
  if (min !== "" && max !== "" && Number(min) > Number(max)) return { error: "The minimum price cannot be more than the maximum." };
  let next = updateCatalogSearchParams(params, "minPrice", min);
  next = updateCatalogSearchParams(next, "maxPrice", max);
  return { params: next };
}

/**
 * Where the header search goes on submit: a fresh /products?search=<text> (the other filters are dropped), or
 * /products for an empty field. The text is trimmed and cut at MAX_SEARCH_LENGTH.
 */
export function searchSubmitPath(text) {
  const query = String(text ?? "").trim().slice(0, MAX_SEARCH_LENGTH).trim();
  return query ? `/products?${new URLSearchParams({ search: query })}` : "/products";
}

/**
 * The header search field. draft is what the field shows; submitted is the search the results use. Typing changes
 * only the draft, so results change only on submit (the button or Enter) and on clear (the × button), never while
 * typing. "sync" follows the URL after a navigation.
 */
export function searchFieldReducer(state, action) {
  switch (action?.type) {
    case "type": return { ...state, draft: String(action.value ?? "").slice(0, MAX_SEARCH_LENGTH) };
    case "submit": {
      const text = state.draft.trim();
      return { draft: text, submitted: text };
    }
    case "clear": return { draft: "", submitted: "" };
    case "sync": {
      const text = String(action.value ?? "");
      return { draft: text, submitted: text };
    }
    default: return state;
  }
}

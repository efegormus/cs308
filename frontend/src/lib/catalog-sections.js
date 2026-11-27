// The category sections of /products (SCRUM-165). Pure, so Node can test it.
//
// The page loads every product that matches the search, brand and price (catalog-api.js, fetchMatchingProducts) and
// works out the rest here:
// - Sections: one per category, holding the products whose own category it is. GET /api/products?categoryId=<id>
//   also returns the products of every subcategory, and parent categories have products of their own (Phones and
//   Smartphones), so the sections are grouped here instead of asking the API once per section.
// - Scope: a chosen category shows itself and every category below it, each as its own section.
// - Sidebar counts: a category counts its own products and those of every category below it.
// - "In stock only" has no API parameter; it is applied here.
// - Sorting is per section. The products arrive in the API's name order, which is the base order: price sorts are
//   stable on it (the API breaks price ties by name, then id, too), and popularity uses the order of
//   GET /api/products?sort=popularity, so every order matches the server's.

/** Products a section shows before "View all". */
export const SECTION_PREVIEW = 3;

/** The per-section sort select, in its order. "name" is the default and the API's own order. */
export const SECTION_SORTS = Object.freeze([
  Object.freeze({ value: "name", label: "Name A to Z" }),
  Object.freeze({ value: "price-asc", label: "Price low to high" }),
  Object.freeze({ value: "price-desc", label: "Price high to low" }),
  Object.freeze({ value: "popularity", label: "Popularity" })
]);

const SORT_VALUES = new Set(SECTION_SORTS.map(option => option.value));

export function isSectionSort(value) {
  return SORT_VALUES.has(value);
}

function plural(count, one, many) {
  return `${count} ${count === 1 ? one : many}`;
}

/**
 * The category tree of GET /api/categories. Categories keep the API's order; children are listed under their parent
 * in the same order. A category whose parentId is missing, unknown or part of a loop counts as top-level, as in the
 * header (category-tree.js). Returns { byId, roots, childrenOf(id), parentOf(id), ordered }, where ordered is every
 * category depth first: a top-level category, then everything below it.
 */
export function catalogTree(categories) {
  const valid = [];
  const byId = new Map();
  for (const category of Array.isArray(categories) ? categories : []) {
    if (!Number.isInteger(category?.id) || typeof category.name !== "string" || byId.has(category.id)) continue;
    byId.set(category.id, category);
    valid.push(category);
  }

  function leadsToLoop(category) {
    const seen = new Set([category.id]);
    let current = category;
    while (Number.isInteger(current.parentId) && byId.has(current.parentId)) {
      if (seen.has(current.parentId)) return true;
      seen.add(current.parentId);
      current = byId.get(current.parentId);
    }
    return false;
  }

  const parents = new Map();
  const children = new Map(valid.map(category => [category.id, []]));
  const roots = [];
  for (const category of valid) {
    const parentId = category.parentId;
    if (Number.isInteger(parentId) && parentId !== category.id && byId.has(parentId) && !leadsToLoop(category)) {
      parents.set(category.id, parentId);
      children.get(parentId).push(category.id);
    } else {
      roots.push(category.id);
    }
  }

  const ordered = [];
  const visit = id => {
    ordered.push(id);
    for (const child of children.get(id)) visit(child);
  };
  for (const id of roots) visit(id);

  return {
    byId,
    roots,
    ordered,
    childrenOf: id => children.get(id) ?? [],
    parentOf: id => parents.get(id) ?? null
  };
}

/** The category and every category below it, as a Set of ids. An unknown or empty id means every category. */
export function categoryScope(tree, categoryId) {
  const id = Number(categoryId);
  if (!categoryId || !tree.byId.has(id)) return new Set(tree.ordered);
  const scope = new Set();
  const visit = current => {
    scope.add(current);
    for (const child of tree.childrenOf(current)) visit(child);
  };
  visit(id);
  return scope;
}

/** The chosen category and its parents, top-level first. Empty for no or an unknown category. */
export function categoryPath(tree, categoryId) {
  const id = Number(categoryId);
  if (!categoryId || !tree.byId.has(id)) return [];
  const path = [];
  for (let current = id; current !== null; current = tree.parentOf(current)) path.unshift(tree.byId.get(current));
  return path;
}

/** Only the products in stock when inStockOnly is set; otherwise every product, out of stock included. */
export function filterInStock(products, inStockOnly) {
  const list = Array.isArray(products) ? products : [];
  return inStockOnly ? list.filter(product => product.stockQuantity > 0) : list;
}

/**
 * The sidebar counts: a Map from category id to the number of products in it and in every category below it.
 * Products of a category that is not in the tree are not counted.
 */
export function categoryCounts(tree, products) {
  const counts = new Map(tree.ordered.map(id => [id, 0]));
  for (const product of products) {
    for (let id = tree.byId.has(product.categoryId) ? product.categoryId : null; id !== null; id = tree.parentOf(id)) {
      counts.set(id, counts.get(id) + 1);
    }
  }
  return counts;
}

/**
 * The sections in tree order: the categories in scope that have at least one product, each with its own products in
 * the order they arrived (the API's name order). Returns [{ id, name, description, products }].
 */
export function buildSections(tree, products, { categoryId = "", inStockOnly = false } = {}) {
  const scope = categoryScope(tree, categoryId);
  const grouped = new Map();
  for (const product of filterInStock(products, inStockOnly)) {
    if (!scope.has(product.categoryId)) continue;
    if (!grouped.has(product.categoryId)) grouped.set(product.categoryId, []);
    grouped.get(product.categoryId).push(product);
  }
  return tree.ordered.filter(id => grouped.has(id)).map(id => {
    const category = tree.byId.get(id);
    return { id, name: category.name, description: category.description ?? null, products: grouped.get(id) };
  });
}

/** A Map from product id to its place in a list of ids (fetchPopularityOrder) or of products, for sortSection. */
export function rankById(list) {
  return new Map((Array.isArray(list) ? list : [])
    .map((item, index) => [typeof item === "object" && item !== null ? item.id : item, index]));
}

/**
 * One section's products in the chosen order. The input order is the base (name) order and is never changed.
 * popularityRank is rankById() of the popularity list; without it, "popularity" keeps the base order until it arrives.
 * Products missing from the rank (a product added between the two requests) go last, in base order.
 */
export function sortSection(products, sort = "name", popularityRank = null) {
  const list = Array.isArray(products) ? products : [];
  const base = new Map(list.map((product, index) => [product.id, index]));
  const byBase = (a, b) => base.get(a.id) - base.get(b.id);
  if (sort === "price-asc") return [...list].sort((a, b) => a.price - b.price || byBase(a, b));
  if (sort === "price-desc") return [...list].sort((a, b) => b.price - a.price || byBase(a, b));
  if (sort === "popularity" && popularityRank instanceof Map) {
    const rank = product => popularityRank.get(product.id) ?? Number.POSITIVE_INFINITY;
    return [...list].sort((a, b) => (rank(a) - rank(b)) || byBase(a, b));
  }
  return [...list];
}

/** What a section shows: the first SECTION_PREVIEW products, or all of them once expanded. */
export function visibleProducts(products, expanded, limit = SECTION_PREVIEW) {
  return expanded ? products : products.slice(0, limit);
}

/** The "View all N" / "Show less" toggle of a section, or null when every product already shows. */
export function sectionToggleLabel(count, expanded, limit = SECTION_PREVIEW) {
  if (count <= limit) return null;
  return expanded ? "Show less" : `View all ${count}`;
}

/** The muted line under the heading when there is no search: "8 products in 2 sections". */
export function sectionsStatus(sections) {
  const products = sections.reduce((total, section) => total + section.products.length, 0);
  return `${plural(products, "product", "products")} in ${plural(sections.length, "section", "sections")}`;
}

/**
 * The sidebar's category list: every top-level category, and the categories below the top-level category that holds
 * the chosen one (only there). Each row is { id, name, count, depth, selected }. During a search, categories without a
 * match are left out (the chosen one stays, so it can be seen and changed).
 */
export function sidebarCategories(tree, counts, { categoryId = "", searching = false } = {}) {
  const selected = tree.byId.has(Number(categoryId)) ? Number(categoryId) : null;
  // The chosen category and its parents always stay, so the chosen one is never hidden under a parent without matches.
  const kept = new Set(selected === null ? [] : categoryPath(tree, selected).map(category => category.id));
  const openRoot = selected === null ? null : categoryPath(tree, selected)[0].id;
  const rows = [];
  const add = (id, depth) => {
    const count = counts.get(id) ?? 0;
    if (searching && count === 0 && !kept.has(id)) return;
    rows.push({ id, name: tree.byId.get(id).name, count, depth, selected: id === selected });
    if (id !== openRoot && depth === 0) return;
    for (const child of tree.childrenOf(id)) add(child, depth + 1);
  };
  for (const id of tree.roots) add(id, 0);
  return rows;
}

/**
 * The heading of the page. Without a search: the chosen category's name, or "All products". With a search: the query
 * (shown bold and quoted, then "search results") and "N results". The breadcrumb starts at Home and All products, goes
 * through the chosen category's parents and ends with the category or the quoted query; the last item has no link.
 * Links use the existing ?categoryId= parameter.
 */
export function catalogHeading(tree, { categoryId = "", search = "", resultCount = 0 } = {}) {
  const query = typeof search === "string" ? search.trim() : "";
  const path = categoryPath(tree, categoryId);
  const crumbs = [{ label: "Home", to: "/" }, { label: "All products", to: "/products" }]
    .concat(path.map(category => ({ label: category.name, to: `/products?categoryId=${category.id}` })));
  if (query) crumbs.push({ label: `“${query}”`, to: null });
  else if (path.length === 0) crumbs[1].to = null;
  else crumbs[crumbs.length - 1].to = null;

  if (query) {
    return { kind: "search", query, title: `“${query}” search results`, results: plural(resultCount, "result", "results"), crumbs };
  }
  const category = path.at(-1);
  return { kind: category ? "category" : "all", query: "", title: category ? category.name : "All products", results: null, crumbs };
}

/** The empty search result: the message and the hint line under it. */
export function noResults(query) {
  return {
    message: `No results for “${String(query ?? "").trim()}”.`,
    hint: "Check the spelling, try a shorter or more general word, or clear the filters."
  };
}

/**
 * The removable chips of the active filters, in this order: category, brand, price, in stock. Each chip is
 * { key, label, remove }, where remove lists the URL parameters that removing it deletes. formatAmount formats a price
 * (product-display.js formatPrice on the page).
 */
export function activeFilterChips(tree, { categoryId = "", brandId = "", brands = [], minPrice = "", maxPrice = "", inStock = false } = {},
  formatAmount = String) {
  const chips = [];
  const category = categoryPath(tree, categoryId).at(-1);
  if (category) chips.push({ key: "category", label: category.name, remove: ["categoryId"] });
  if (brandId) {
    const brand = (Array.isArray(brands) ? brands : []).find(item => String(item.id) === String(brandId));
    if (brand) chips.push({ key: "brand", label: brand.name, remove: ["brandId"] });
  }
  if (minPrice !== "" || maxPrice !== "") {
    const label = minPrice !== "" && maxPrice !== "" ? `${formatAmount(Number(minPrice))} – ${formatAmount(Number(maxPrice))}`
      : minPrice !== "" ? `From ${formatAmount(Number(minPrice))}` : `Up to ${formatAmount(Number(maxPrice))}`;
    chips.push({ key: "price", label, remove: ["minPrice", "maxPrice"] });
  }
  if (inStock) chips.push({ key: "inStock", label: "In stock only", remove: ["inStock"] });
  return chips;
}

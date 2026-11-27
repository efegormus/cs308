import test from "node:test";
import assert from "node:assert/strict";
import {
  MAX_SEARCH_LENGTH, applyPriceRange, catalogFiltersFromSearchParams, clearCatalogSearchParams, hasCatalogFilters,
  isInStockOnly, queryForCategoryHash, removeCatalogParams, searchFieldReducer, searchSubmitPath, sortDescription,
  updateCatalogSearchParams
} from "./catalog-query.js";

test("catalog filter state reads every URL filter and defaults the page", () => {
  const filters = catalogFiltersFromSearchParams(new URLSearchParams(
    "search=pixel&categoryId=2&brandId=4&minPrice=10&maxPrice=90&sort=price&order=desc&page=3"
  ));
  assert.deepEqual(filters, {
    search: "pixel", categoryId: "2", brandId: "4", minPrice: "10", maxPrice: "90",
    sort: "price", order: "desc", inStock: "", page: "3"
  });
  assert.equal(catalogFiltersFromSearchParams(new URLSearchParams()).page, "1");
});

test("changing a filter preserves other query state and resets pagination", () => {
  const next = updateCatalogSearchParams(new URLSearchParams("search=pixel&page=4&sort=price"),
    "brandId", "7");
  assert.deepEqual([...next.entries()], [["search", "pixel"], ["sort", "price"], ["brandId", "7"]]);
});

test("changing page preserves filters and removing a value clears its query key", () => {
  const page = updateCatalogSearchParams(new URLSearchParams("search=pixel&page=2"), "page", "3", false);
  const cleared = updateCatalogSearchParams(page, "search", "");
  assert.equal(page.get("search"), "pixel");
  assert.equal(page.get("page"), "3");
  assert.equal(cleared.has("search"), false);
  assert.equal(cleared.has("page"), false);
});

test("clearing the filters drops every filter, the sort and the page", () => {
  const next = clearCatalogSearchParams(new URLSearchParams(
    "search=zzz&categoryId=2&brandId=4&minPrice=10&maxPrice=90&sort=price&order=desc&page=3&utm=keep"));
  assert.deepEqual([...next.entries()], [["utm", "keep"]]);
  assert.deepEqual([...clearCatalogSearchParams(new URLSearchParams()).entries()], []);
});

test("filters count as active for a search word, category, brand or price, but not for sort or page", () => {
  assert.equal(hasCatalogFilters({}), false);
  assert.equal(hasCatalogFilters({ search: "   " }), false);
  for (const filter of [{ search: "pixel" }, { categoryId: "2" }, { brandId: "4" }, { minPrice: "0" }, { maxPrice: "9" }])
    assert.equal(hasCatalogFilters(filter), true, JSON.stringify(filter));
});

test("the sort sentence names the field and the direction, with the API defaults", () => {
  assert.equal(sortDescription("", ""), "Sorted by name, A to Z.");
  // The API orders by name, A to Z, whatever `order` says when there is no sort.
  assert.equal(sortDescription("", "desc"), "Sorted by name, A to Z.");
  assert.equal(sortDescription("", "asc"), "Sorted by name, A to Z.");
  assert.equal(sortDescription("price", ""), "Sorted by price, lowest first.");
  assert.equal(sortDescription("price", "asc"), "Sorted by price, lowest first.");
  assert.equal(sortDescription("price", "desc"), "Sorted by price, highest first.");
  assert.equal(sortDescription("popularity", ""), "Sorted by popularity (units sold), best sellers first.");
  assert.equal(sortDescription("popularity", "desc"), "Sorted by popularity (units sold), best sellers first.");
  assert.equal(sortDescription("popularity", "asc"), "Sorted by popularity (units sold), least sold first.");
});


test("category links reset global pagination and preserve all filters", async () => {
  const { categoryProductsPath } = await import("./catalog-query.js");
  const path = categoryProductsPath(99, "page=3&categoryId=1&search=tablet&sort=price&order=desc&minPrice=10&brandId=4");
  const query = new URL(path, "https://store.example").searchParams;
  assert.equal(query.get("categoryId"), "99");assert.equal(query.has("page"), false);
  for (const [key,value] of Object.entries({search:"tablet",sort:"price",order:"desc",minPrice:"10",brandId:"4"})) assert.equal(query.get(key),value);
});
test("category link does not mutate saved search parameters", async () => {
  const { categoryProductsPath } = await import("./catalog-query.js");
  const source = new URLSearchParams("page=3&categoryId=1");categoryProductsPath(2, source);
  assert.equal(source.toString(), "page=3&categoryId=1");
});
test("direct category link survives serialization and uses no hash anchor", async () => {
  const { categoryProductsPath } = await import("./catalog-query.js");
  const path = categoryProductsPath(99);assert.equal(path,"/products?categoryId=99");
  assert.equal(new URL(path,"https://store.example").hash,"");
});
test("unsafe or invalid category identifiers are rejected", async () => {
  const { categoryProductsPath } = await import("./catalog-query.js");
  for(const id of [0,-1,1.5,Number.MAX_SAFE_INTEGER+1,"99",null]) assert.throws(()=>categoryProductsPath(id),RangeError);
});

// --- SCRUM-165 ---

test("In stock only is the inStock=1 URL value, counts as a filter and is cleared with the others", () => {
  assert.equal(catalogFiltersFromSearchParams(new URLSearchParams("inStock=1")).inStock, "1");
  assert.equal(isInStockOnly("1"), true);
  for (const value of ["", "0", "true", undefined]) assert.equal(isInStockOnly(value), false, String(value));
  assert.equal(hasCatalogFilters({ inStock: "1" }), true);
  assert.equal(hasCatalogFilters({ inStock: "yes" }), false);
  assert.deepEqual([...clearCatalogSearchParams(new URLSearchParams("inStock=1&utm=keep")).entries()], [["utm", "keep"]]);
});

test("removing a chip deletes only its parameters and the page", () => {
  const next = removeCatalogParams(new URLSearchParams("search=nova&categoryId=1&minPrice=5&maxPrice=9&page=2"), ["minPrice", "maxPrice"]);
  assert.equal(next.toString(), "search=nova&categoryId=1");
});

test("an old #category-<id> link becomes the categoryId parameter, unless one is already set", () => {
  assert.equal(queryForCategoryHash("", "#category-11"), "categoryId=11");
  assert.equal(queryForCategoryHash("?search=nova&page=3", "#category-2"), "search=nova&categoryId=2");
  assert.equal(queryForCategoryHash("categoryId=5", "#category-2"), null);
  for (const hash of ["", "#top", "#category-0", "#category-x"]) assert.equal(queryForCategoryHash("", hash), null, hash);
});

test("the price box applies a range, removes empty bounds and refuses a range the API would reject", () => {
  const applied = applyPriceRange(new URLSearchParams("categoryId=1&page=2&maxPrice=50"), " 10,5 ", "");
  assert.equal(applied.params.toString(), "categoryId=1&minPrice=10.5");
  assert.equal(applyPriceRange(new URLSearchParams(), "0", "0").params.toString(), "minPrice=0&maxPrice=0");
  assert.deepEqual(applyPriceRange(new URLSearchParams(), "-1", ""), { error: "Enter a price of 0 or more." });
  assert.deepEqual(applyPriceRange(new URLSearchParams(), "abc", ""), { error: "Enter a price of 0 or more." });
  assert.deepEqual(applyPriceRange(new URLSearchParams(), "90", "10"), { error: "The minimum price cannot be more than the maximum." });
});

test("the header search changes the results only on submit or clear, never while typing", () => {
  let state = searchFieldReducer({ draft: "", submitted: "" }, { type: "sync", value: "pixel" });
  assert.deepEqual(state, { draft: "pixel", submitted: "pixel" });
  for (const value of ["n", "no", "nov", " nova "]) {
    state = searchFieldReducer(state, { type: "type", value });
    assert.equal(state.submitted, "pixel", `typing "${value}" must not search`);
  }
  assert.equal(state.draft, " nova ");
  state = searchFieldReducer(state, { type: "submit" });
  assert.deepEqual(state, { draft: "nova", submitted: "nova" });
  assert.deepEqual(searchFieldReducer(state, { type: "clear" }), { draft: "", submitted: "" });
  assert.equal(searchFieldReducer(state, { type: "type", value: "x".repeat(150) }).draft.length, MAX_SEARCH_LENGTH);
  assert.equal(searchFieldReducer(state, { type: "unknown" }), state);
});

test("a submitted search opens a fresh /products search, and an empty one opens all products", () => {
  assert.equal(searchSubmitPath("  galaxy tab "), "/products?search=galaxy+tab");
  assert.equal(searchSubmitPath("a&b=c"), "/products?search=a%26b%3Dc");
  assert.equal(searchSubmitPath("   "), "/products");
  assert.equal(searchSubmitPath(undefined), "/products");
  assert.equal(new URL(searchSubmitPath("y".repeat(150)), "https://store.example").searchParams.get("search").length, MAX_SEARCH_LENGTH);
});

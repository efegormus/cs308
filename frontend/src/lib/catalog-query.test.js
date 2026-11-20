import test from "node:test";
import assert from "node:assert/strict";
import {
  catalogFiltersFromSearchParams, clearCatalogSearchParams, hasCatalogFilters, sortDescription, updateCatalogSearchParams
} from "./catalog-query.js";

test("catalog filter state reads every URL filter and defaults the page", () => {
  const filters = catalogFiltersFromSearchParams(new URLSearchParams(
    "search=pixel&categoryId=2&brandId=4&minPrice=10&maxPrice=90&sort=price&order=desc&page=3"
  ));
  assert.deepEqual(filters, {
    search: "pixel", categoryId: "2", brandId: "4", minPrice: "10", maxPrice: "90",
    sort: "price", order: "desc", page: "3"
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
  assert.equal(sortDescription("", "desc"), "Sorted by name, Z to A.");
  assert.equal(sortDescription("price", ""), "Sorted by price, lowest first.");
  assert.equal(sortDescription("price", "asc"), "Sorted by price, lowest first.");
  assert.equal(sortDescription("price", "desc"), "Sorted by price, highest first.");
  assert.equal(sortDescription("popularity", ""), "Sorted by popularity (units sold), best sellers first.");
  assert.equal(sortDescription("popularity", "desc"), "Sorted by popularity (units sold), best sellers first.");
  assert.equal(sortDescription("popularity", "asc"), "Sorted by popularity (units sold), least sold first.");
});

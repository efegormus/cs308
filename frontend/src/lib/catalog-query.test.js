import test from "node:test";
import assert from "node:assert/strict";
import { catalogFiltersFromSearchParams, updateCatalogSearchParams } from "./catalog-query.js";

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

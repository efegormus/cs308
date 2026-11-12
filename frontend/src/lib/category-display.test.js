import test from "node:test";
import assert from "node:assert/strict";
import {
  catalogSummary, categoryAnchorId, categoryIdFromHash, productCountLabel, visibleCategories
} from "./category-display.js";

test("categoryAnchorId builds the id the category links point at", () => {
  assert.equal(categoryAnchorId(3), "category-3");
});

test("categoryIdFromHash reads a category hash", () => {
  assert.equal(categoryIdFromHash("#category-3"), 3);
  assert.equal(categoryIdFromHash("#category-1234567890"), 1234567890);
});

test("categoryIdFromHash returns null for any other hash", () => {
  for (const hash of ["", "#", "#top", "#category-", "#category-0", "#category-01", "#category--1", "#category-1.5",
    "#category-3x", "#category-3 ", "category-3", "#Category-3", "#category-9007199254740992", undefined, null]) {
    assert.equal(categoryIdFromHash(hash), null, String(hash));
  }
});

test("productCountLabel uses the singular for 1", () => {
  assert.equal(productCountLabel(0), "0 products");
  assert.equal(productCountLabel(1), "1 product");
  assert.equal(productCountLabel(2), "2 products");
});

test("catalogSummary counts the products and the categories", () => {
  const categories = [{ products: [{}, {}] }, { products: [{}] }];
  assert.equal(catalogSummary(categories), "3 products in 2 categories.");
  assert.equal(catalogSummary([{ products: [{}] }]), "1 product in 1 category.");
});

test("catalogSummary says there are no products, with or without categories", () => {
  const message = "No products available yet. Please check back soon.";
  assert.equal(catalogSummary([]), message);
  assert.equal(catalogSummary([{ products: [] }, { products: [] }]), message);
});

test("search results keep matching categories and omit empty categories", () => {
  const matches = { id: 1, products: [{ id: 10 }] };
  const empty = { id: 2, products: [] };
  assert.deepEqual(visibleCategories([matches, empty], "phone"), [matches]);
});

test("search results show a clear message when no categories match", () => {
  assert.deepEqual(visibleCategories([{ id: 1, products: [] }], "phone"), []);
  assert.equal(catalogSummary([], "phone"), "No products found.");
});

test("search results retain out-of-stock products in their category", () => {
  const outOfStock = { id: 10, stockQuantity: 0 };
  const category = { id: 1, products: [outOfStock] };
  assert.deepEqual(visibleCategories([category], "phone"), [category]);
  assert.equal(catalogSummary([category], "phone"), "1 product in 1 category.");
});

test("normal browsing keeps empty categories", () => {
  const categories = [{ id: 1, products: [] }];
  assert.equal(visibleCategories(categories, "  "), categories);
});

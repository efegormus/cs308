import test from "node:test";
import assert from "node:assert/strict";
import {
  buildCategoryRequest, buildProductRequest, catalogFailureMessage, categoryOptions, EMPTY_PRODUCT_FORM, mergeById,
  parentName, parsePrice, parseStock, productToForm
} from "./catalog-management.js";

const filled = {
  ...EMPTY_PRODUCT_FORM, categoryId: "3", sku: "PX-1", name: "Phone X", model: "X1", serialNumber: "SN-1",
  description: "A phone.", price: "1999,90", stockQuantity: "12", warrantyStatus: "2 years",
  distributorName: "Acme", distributorContact: "acme@example.com"
};

test("parsePrice accepts a dot or comma and at most 2 decimals, from 0 to the limit", () => {
  assert.equal(parsePrice("1999.90"), 1999.9);
  assert.equal(parsePrice("1999,90"), 1999.9);
  assert.equal(parsePrice(" 0 "), 0);
  assert.equal(parsePrice("9999999.99"), 9999999.99);
  for (const bad of ["", "abc", "-1", "1.234", "1,2,3", "10000000", "1e3", null, undefined]) assert.equal(parsePrice(bad), null, String(bad));
});

test("parseStock accepts a whole number from 0 to the limit", () => {
  assert.equal(parseStock("0"), 0);
  assert.equal(parseStock(" 25 "), 25);
  assert.equal(parseStock("1000000"), 1000000);
  for (const bad of ["", "-1", "1.5", "1000001", "x", null]) assert.equal(parseStock(bad), null, String(bad));
});

test("a complete form becomes a create request with the stock count and without isActive", () => {
  const built = buildProductRequest(filled);
  assert.equal(built.ok, true);
  assert.deepEqual(built.request, {
    categoryId: 3, brandId: null, sku: "PX-1", name: "Phone X", model: "X1", serialNumber: "SN-1", description: "A phone.",
    warrantyStatus: "2 years", distributorName: "Acme", distributorContact: "acme@example.com", price: 1999.9, stockQuantity: 12
  });
  assert.equal("isActive" in built.request, false);
});

test("an edit request carries isActive and the brand, and never the stock count", () => {
  const built = buildProductRequest({ ...filled, brandId: 7 }, { editing: true, isActive: false });
  assert.equal(built.ok, true);
  assert.equal(built.request.isActive, false);
  assert.equal(built.request.brandId, 7);
  assert.equal("stockQuantity" in built.request, false);
  // An edit does not need a valid stock field, because it is not sent.
  assert.equal(buildProductRequest({ ...filled, stockQuantity: "" }, { editing: true }).ok, true);
});

test("an empty product form shows an error under every field", () => {
  const built = buildProductRequest(EMPTY_PRODUCT_FORM);
  assert.equal(built.ok, false);
  for (const field of ["categoryId", "sku", "name", "model", "serialNumber", "description", "price", "stockQuantity", "warrantyStatus",
    "distributorName", "distributorContact"]) assert.ok(built.errors[field], field);
});

test("the form rejects a negative price, a SKU with spaces and values over the column limits", () => {
  assert.ok(buildProductRequest({ ...filled, price: "-5" }).errors.price);
  assert.ok(buildProductRequest({ ...filled, sku: "PX 1" }).errors.sku);
  assert.ok(buildProductRequest({ ...filled, sku: "x".repeat(65) }).errors.sku);
  assert.ok(buildProductRequest({ ...filled, name: "x".repeat(201) }).errors.name);
  assert.ok(buildProductRequest({ ...filled, stockQuantity: "-1" }).errors.stockQuantity);
  assert.ok(buildProductRequest({ ...filled, name: "   " }).errors.name);
  assert.equal(buildProductRequest({ ...filled, price: "0" }).ok, true);
});

test("productToForm fills the edit form from a product and keeps its brand", () => {
  const form = productToForm({
    id: 9, categoryId: 3, brandId: 7, sku: "PX-1", name: "Phone X", model: "X1", serialNumber: "SN-1", description: "A phone.",
    price: 1999.9, stockQuantity: 12, warrantyStatus: "2 years", distributorName: "Acme", distributorContact: "c", isActive: true
  });
  assert.equal(form.categoryId, "3");
  assert.equal(form.brandId, 7);
  assert.equal(form.price, "1999.9");
  assert.equal(form.stockQuantity, "12");
  assert.equal(buildProductRequest(form, { editing: true }).ok, true);
});

test("buildCategoryRequest trims, bounds and sends the parent only when creating", () => {
  assert.deepEqual(buildCategoryRequest({ name: "  Phones ", description: "  ", parentId: "4" }).request,
    { name: "Phones", description: null, parentId: 4 });
  assert.deepEqual(buildCategoryRequest({ name: "Phones", description: "All", parentId: "" }).request,
    { name: "Phones", description: "All", parentId: null });
  assert.deepEqual(buildCategoryRequest({ name: "Phones", description: "All", parentId: "4" }, { editing: true }).request,
    { name: "Phones", description: "All" });
  assert.ok(buildCategoryRequest({ name: "  " }).errors.name);
  assert.ok(buildCategoryRequest({ name: "x".repeat(101) }).errors.name);
  assert.ok(buildCategoryRequest({ name: "A", description: "x".repeat(1001) }).errors.description);
});

test("catalogFailureMessage never forwards server text and names the conflict by subject", () => {
  assert.match(catalogFailureMessage({ status: 401 }), /Log in again/);
  assert.match(catalogFailureMessage({ status: 403 }), /product manager/);
  assert.match(catalogFailureMessage({ status: 404 }, "product"), /product no longer exists/);
  assert.match(catalogFailureMessage({ status: 409 }, "product"), /SKU/);
  assert.match(catalogFailureMessage({ status: 409 }, "category"), /category name is taken/);
  assert.match(catalogFailureMessage({ kind: "invalid-response" }), /could not read/);
  assert.match(catalogFailureMessage({ status: 500, message: "<b>secret</b>" }), /couldn't reach the server/);
});

test("parentName and categoryOptions show the tree as Parent / Child", () => {
  const categories = [
    { id: 1, parentId: null, name: "Phones" }, { id: 2, parentId: 1, name: "Accessories" }, { id: 3, parentId: null, name: "Audio" }
  ];
  assert.equal(parentName(categories[0], categories), "—");
  assert.equal(parentName(categories[1], categories), "Phones");
  assert.equal(parentName({ id: 9, parentId: 99, name: "X" }, categories), "#99");
  assert.deepEqual(categoryOptions(categories).map(option => option.label), ["Audio", "Phones", "Phones / Accessories"]);
});

test("mergeById replaces a row in place and appends new rows", () => {
  const merged = mergeById([{ id: 1, v: "a" }, { id: 2, v: "b" }], [{ id: 2, v: "B" }, { id: 3, v: "c" }]);
  assert.deepEqual(merged, [{ id: 1, v: "a" }, { id: 2, v: "B" }, { id: 3, v: "c" }]);
  assert.deepEqual(mergeById(null, [{ id: 1 }]), [{ id: 1 }]);
});

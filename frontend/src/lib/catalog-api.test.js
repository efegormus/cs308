import test from "node:test";
import assert from "node:assert/strict";
import {
  CatalogError, ORDER_OPTIONS, SORT_OPTIONS, fetchCatalog, fetchCategories, fetchProduct, fetchProducts, productsPath
} from "./catalog-api.js";

const phones = { id: 1, name: "Phones" };
const tablets = { id: 2, name: "Tablets" };
const pixel = { id: 10, name: "Pixel 9", price: 43000.5, stockQuantity: 12, categoryId: 1 };
const soldOut = { id: 11, name: "Galaxy S25", price: 48000, stockQuantity: 0, categoryId: 1 };

// Replaces fetch for one test. `routes` maps a path to a body, or to a Response for a failure. Every call is recorded.
function mockFetch(routes) {
  const calls = [];
  globalThis.fetch = async (path, init) => {
    calls.push({ path, init });
    const route = routes[path];
    if (route === undefined) throw new Error(`Unexpected request: ${path}`);
    return route instanceof Response ? route : Response.json(route);
  };
  return calls;
}

test("productsPath is /api/products without filters", () => {
  assert.equal(productsPath(), "/api/products");
  assert.equal(productsPath({}), "/api/products");
});

test("productsPath leaves out empty and blank values", () => {
  assert.equal(productsPath({ categoryId: undefined, search: "", sort: "", order: undefined }), "/api/products");
  assert.equal(productsPath({ search: "   " }), "/api/products");
  assert.equal(productsPath({ search: 42 }), "/api/products");
});

test("productsPath sends every filter", () => {
  assert.equal(productsPath({ categoryId: 2, search: "pixel", sort: "price", order: "desc" }),
    "/api/products?categoryId=2&search=pixel&sort=price&order=desc");
});

test("productsPath trims the search text and encodes it", () => {
  assert.equal(productsPath({ search: "  pixel 9  " }), "/api/products?search=pixel+9");
  assert.equal(productsPath({ search: "50% & more=1" }), "/api/products?search=50%25+%26+more%3D1");
  assert.equal(productsPath({ search: "a/b?c#d" }), "/api/products?search=a%2Fb%3Fc%23d");
});

test("productsPath keeps a category id of 0", () => {
  assert.equal(productsPath({ categoryId: 0 }), "/api/products?categoryId=0");
});

test("the sort and order values match what the API accepts", () => {
  assert.deepEqual(SORT_OPTIONS, ["price", "popularity"]);
  assert.deepEqual(ORDER_OPTIONS, ["asc", "desc"]);
});

test("fetchCategories asks for JSON and returns the rows", async () => {
  const calls = mockFetch({ "/api/categories": [phones, tablets] });
  assert.deepEqual(await fetchCategories(), [phones, tablets]);
  assert.equal(calls.length, 1);
  assert.deepEqual(calls[0].init.headers, { Accept: "application/json" });
});

test("fetchProducts sends the filters to the products API and passes the signal on", async () => {
  const calls = mockFetch({ "/api/products?search=pixel&sort=popularity": [pixel] });
  const controller = new AbortController();
  const rows = await fetchProducts({ search: "pixel", sort: "popularity", signal: controller.signal });
  assert.deepEqual(rows, [pixel]);
  assert.equal(calls[0].init.signal, controller.signal);
});

test("fetchCatalog groups the products by category, keeping empty categories", async () => {
  mockFetch({ "/api/categories": [phones, tablets], "/api/products": [pixel, soldOut] });
  const catalog = await fetchCatalog();
  assert.deepEqual(catalog.map(category => [category.id, category.products.map(product => product.id)]),
    [[1, [10, 11]], [2, []]]);
  assert.equal(catalog[0].name, "Phones");
  assert.equal(catalog[0].products[1].stockQuantity, 0, "sold-out products stay in the list");
});

test("fetchCatalog passes the filters to the products request only", async () => {
  const calls = mockFetch({ "/api/categories": [phones, tablets], "/api/products?search=pixel&order=asc": [pixel] });
  const catalog = await fetchCatalog({ search: "pixel", order: "asc" });
  assert.deepEqual(calls.map(call => call.path).sort(), ["/api/categories", "/api/products?search=pixel&order=asc"]);
  assert.equal(catalog[1].products.length, 0, "a category without a match stays, with no products");
});

test("fetchCatalog rejects a product in an unknown category", async () => {
  mockFetch({ "/api/categories": [phones], "/api/products": [{ ...pixel, categoryId: 99 }] });
  await assert.rejects(fetchCatalog(), error => error instanceof CatalogError && error.message === "Invalid product response.");
});

test("fetchCatalog rejects invalid products", async () => {
  for (const bad of [{ ...pixel, price: -1 }, { ...pixel, price: "9" }, { ...pixel, stockQuantity: -1 },
    { ...pixel, stockQuantity: 1.5 }, { ...pixel, id: "10" }, { ...pixel, name: 7 }, null]) {
    mockFetch({ "/api/categories": [phones], "/api/products": [bad] });
    await assert.rejects(fetchCatalog(), CatalogError, JSON.stringify(bad));
  }
});

test("fetchCatalog rejects invalid and duplicate categories", async () => {
  for (const bad of [[{ id: "1", name: "Phones" }], [{ id: 1, name: 7 }], [phones, phones], [null]]) {
    mockFetch({ "/api/categories": bad, "/api/products": [] });
    await assert.rejects(fetchCatalog(), error => error instanceof CatalogError && error.message === "Invalid category response.");
  }
});

test("a failed request throws a CatalogError with the HTTP status", async () => {
  mockFetch({ "/api/categories": new Response("", { status: 503 }), "/api/products": [] });
  await assert.rejects(fetchCatalog(), error => error instanceof CatalogError && error.status === 503);
  mockFetch({ "/api/products?sort=rating": new Response("", { status: 400 }) });
  await assert.rejects(fetchProducts({ sort: "rating" }), error => error.status === 400);
});

test("a response that is not a list or not JSON throws a CatalogError without a status", async () => {
  mockFetch({ "/api/categories": { not: "a list" } });
  await assert.rejects(fetchCategories(), error => error instanceof CatalogError && error.status === undefined);
  mockFetch({ "/api/categories": new Response("<html>", { status: 200 }) });
  await assert.rejects(fetchCategories(), error => error instanceof CatalogError && error.message === "Invalid catalog response.");
});

test("a network failure rejects with the original error", async () => {
  globalThis.fetch = async () => { throw new TypeError("Failed to fetch"); };
  await assert.rejects(fetchCategories(), TypeError);
});

test("an aborted request rejects with the abort error", async () => {
  const controller = new AbortController();
  globalThis.fetch = (path, { signal }) => new Promise((resolve, reject) => {
    signal.addEventListener("abort", () => reject(signal.reason));
  });
  const pending = fetchCategories({ signal: controller.signal });
  controller.abort(new DOMException("Aborted", "AbortError"));
  await assert.rejects(pending, { name: "AbortError" });
});

test("fetchProduct returns a valid product", async () => {
  const calls = mockFetch({ "/api/products/10": pixel });
  assert.deepEqual(await fetchProduct(10), pixel);
  assert.equal(calls[0].path, "/api/products/10");
});

test("fetchProduct returns null for a missing product and encodes the id", async () => {
  const calls = mockFetch({ "/api/products/a%2Fb": new Response("", { status: 404 }) });
  assert.equal(await fetchProduct("a/b"), null);
  assert.equal(calls[0].path, "/api/products/a%2Fb");
});

test("fetchProduct throws for other failures and invalid products", async () => {
  mockFetch({ "/api/products/10": new Response("", { status: 500 }) });
  await assert.rejects(fetchProduct(10), error => error instanceof CatalogError && error.status === 500);
  mockFetch({ "/api/products/10": { ...pixel, price: -5 } });
  await assert.rejects(fetchProduct(10), error => error.message === "Invalid product response.");
  mockFetch({ "/api/products/10": new Response("nope", { status: 200 }) });
  await assert.rejects(fetchProduct(10), error => error.message === "Invalid product response.");
});

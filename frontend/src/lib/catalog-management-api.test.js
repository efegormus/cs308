import test from "node:test";
import assert from "node:assert/strict";
import {
  createCategory, createProduct, deleteCategory, editProduct, fetchCategories, fetchProducts, isManagedCategory,
  isManagedProduct, isProductPage, removeProduct
} from "./catalog-management-api.js";

const category = { id: 2, parentId: null, name: "Phones", slug: "phones", description: null, isActive: true, productCount: 1, childCount: 0 };
const product = {
  id: 5, categoryId: 2, categoryName: "Phones", brandId: null, sku: "PX-1", name: "Phone X", model: "X1", serialNumber: "SN-1",
  description: "A phone.", price: 1999.9, stockQuantity: 12, warrantyStatus: "2 years", distributorName: "Acme",
  distributorContact: "c", isActive: true
};

function stubFetch(t, status, body) {
  const calls = [];
  t.mock.method(globalThis, "fetch", async (path, init) => {
    calls.push({ path, init });
    return { ok: status >= 200 && status < 300, status, headers: new Headers(), json: async () => { if (body === undefined) throw new Error("no body"); return body; } };
  });
  return calls;
}

test("category and product validation accepts the API shapes and rejects broken ones", () => {
  assert.equal(isManagedCategory(category), true);
  assert.equal(isManagedCategory({ ...category, productCount: -1 }), false);
  assert.equal(isManagedCategory({ ...category, parentId: 0 }), false);
  assert.equal(isManagedProduct(product), true);
  assert.equal(isManagedProduct({ ...product, price: -1 }), false);
  assert.equal(isManagedProduct({ ...product, isActive: "yes" }), false);
  assert.equal(isProductPage({ products: [product], nextAfterId: null }), true);
  assert.equal(isProductPage({ products: [product], nextAfterId: 5 }, 0), true);
  assert.equal(isProductPage({ products: [product], nextAfterId: 4 }, 4), false);
  assert.equal(isProductPage({ products: [{ ...product, id: 3 }], nextAfterId: null }, 3), false);
});

test("fetchCategories and fetchProducts read the manager API and check the answer", async t => {
  const calls = stubFetch(t, 200, [category]);
  assert.deepEqual(await fetchCategories(), [category]);
  assert.equal(calls[0].path, "/api/product-management/categories");
  t.mock.restoreAll();
  const productCalls = stubFetch(t, 200, { products: [product], nextAfterId: null });
  assert.equal((await fetchProducts({ afterId: 4 })).products.length, 1);
  assert.equal(productCalls[0].path, "/api/product-management/products?afterId=4");
  assert.throws(() => fetchProducts({ afterId: -1 }), { status: 400 });
});

test("writes send the CSRF token and a JSON body, and the answer must match what was sent", async t => {
  const calls = stubFetch(t, 201, product);
  const body = { sku: "PX-1", price: 1999.9 };
  assert.deepEqual(await createProduct(body, "tok"), product);
  assert.equal(calls[0].init.method, "POST");
  assert.equal(calls[0].init.headers["X-CSRF-TOKEN"], "tok");
  assert.equal(calls[0].init.body, JSON.stringify(body));
  t.mock.restoreAll();
  stubFetch(t, 201, product);
  await assert.rejects(createProduct({ sku: "OTHER", price: 1999.9 }, "tok"), { kind: "invalid-response" });
  t.mock.restoreAll();
  stubFetch(t, 201, category);
  await assert.rejects(createCategory({ name: "Other" }, "tok"), { kind: "invalid-response" });
});

test("a failed write rejects with the status and never with the server's text", async t => {
  stubFetch(t, 409, { title: "<b>secret</b>" });
  await assert.rejects(createProduct({ sku: "PX-1", price: 1 }, "tok"), error => error.status === 409 && !("message" in error));
});

test("deleting a category resolves with null on 204 and rejects on 409", async t => {
  stubFetch(t, 204);
  assert.equal(await deleteCategory(2, "tok"), null);
  t.mock.restoreAll();
  stubFetch(t, 409);
  await assert.rejects(deleteCategory(2, "tok"), { status: 409 });
  assert.throws(() => deleteCategory(0, "tok"), { status: 400 });
});

test("removing a product needs the product back as off sale, and an edit must keep the isActive it sent", async t => {
  stubFetch(t, 200, { ...product, isActive: false });
  assert.equal((await removeProduct(5, "tok")).isActive, false);
  t.mock.restoreAll();
  stubFetch(t, 200, product);
  await assert.rejects(removeProduct(5, "tok"), { kind: "invalid-response" });
  t.mock.restoreAll();
  stubFetch(t, 200, product);
  assert.equal((await editProduct(5, { isActive: true }, "tok")).id, 5);
  t.mock.restoreAll();
  stubFetch(t, 200, { ...product, isActive: false });
  await assert.rejects(editProduct(5, { isActive: true }, "tok"), { kind: "invalid-response" });
});

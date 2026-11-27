import test from "node:test";
import assert from "node:assert/strict";
import {
  cancelDiscount, createDiscount, fetchDiscounts, fetchSalesProducts, isDiscount, isDiscountPage, isProductPage,
  isSalesProduct, setProductPrice
} from "./sales-pricing-api.js";

const product = { id: 3, name: "Phone", model: "P1", stockQuantity: 5, isActive: true, listPrice: 100, price: 80, discountRate: 0.2 };
const discount = {
  id: 9, name: "Black Friday", rate: 0.2, startsAt: "2026-11-27T00:00:00Z", endsAt: "2026-11-30T23:59:59Z",
  status: "Active", cancelledAt: null, productIds: [3]
};

test("a product row is valid only with sane prices and rate", () => {
  assert.equal(isSalesProduct(product), true);
  assert.equal(isSalesProduct({ ...product, price: 120 }), false); // paying more than list
  assert.equal(isSalesProduct({ ...product, listPrice: -1 }), false);
  assert.equal(isSalesProduct({ ...product, discountRate: 1 }), false);
  assert.equal(isSalesProduct({ ...product, isActive: "yes" }), false);
});

test("a discount is valid only with a known status and a rate between 0 and 1", () => {
  assert.equal(isDiscount(discount), true);
  assert.equal(isDiscount({ ...discount, status: "Paused" }), false);
  assert.equal(isDiscount({ ...discount, rate: 0 }), false);
  assert.equal(isDiscount({ ...discount, startsAt: "soon" }), false);
  assert.equal(isDiscount({ ...discount, productIds: [0] }), false);
});

test("pages must move their cursor forward (products) or backward (discounts)", () => {
  assert.equal(isProductPage({ products: [product], nextAfterId: 4 }, 0), true);
  assert.equal(isProductPage({ products: [product], nextAfterId: 3 }, 3), false);
  assert.equal(isProductPage({ products: [product], nextAfterId: null }, 3), false); // id 3 is not after 3
  assert.equal(isDiscountPage({ discounts: [discount], nextBeforeId: 8 }, 0), true);
  assert.equal(isDiscountPage({ discounts: [discount], nextBeforeId: 10 }, 10), false);
});

test("fetchSalesProducts asks for the cursor page and validates it", async t => {
  t.mock.method(globalThis, "fetch", async (path, init) => {
    assert.equal(path, "/api/sales/products?afterId=2");
    assert.equal(init.method, "GET");
    assert.equal(init.headers["X-CSRF-TOKEN"], undefined);
    return Response.json({ products: [product], nextAfterId: null });
  });
  assert.deepEqual((await fetchSalesProducts({ afterId: 2 })).products, [product]);
});

test("an invalid cursor is refused before any request", () => {
  assert.throws(() => fetchSalesProducts({ afterId: -1 }), { status: 400 });
  assert.throws(() => fetchDiscounts({ beforeId: 1.5 }), { status: 400 });
});

test("setProductPrice sends a PUT with the CSRF token and checks the returned list price", async t => {
  const calls = [];
  t.mock.method(globalThis, "fetch", async (path, init) => {
    calls.push({ path, init });
    return Response.json({ ...product, listPrice: 149.9, price: 149.9, discountRate: 0 });
  });
  const updated = await setProductPrice(3, 149.9, "token-1");
  assert.equal(updated.listPrice, 149.9);
  assert.equal(calls[0].path, "/api/sales/products/3/price");
  assert.equal(calls[0].init.method, "PUT");
  assert.equal(calls[0].init.headers["X-CSRF-TOKEN"], "token-1");
  assert.deepEqual(JSON.parse(calls[0].init.body), { price: 149.9 });
});

test("setProductPrice rejects an answer that does not carry the new price", async t => {
  t.mock.method(globalThis, "fetch", async () => Response.json({ ...product, listPrice: 100 }));
  await assert.rejects(setProductPrice(3, 149.9, "t"), { kind: "invalid-response" });
});

test("createDiscount posts the body with CSRF and returns the campaign", async t => {
  const body = { name: "Black Friday", rate: 0.2, endsAt: "2026-11-30T20:59:00.000Z", productIds: [3] };
  const calls = [];
  t.mock.method(globalThis, "fetch", async (path, init) => {
    calls.push({ path, init });
    return Response.json(discount, { status: 201 });
  });
  assert.deepEqual(await createDiscount(body, "token-2"), discount);
  assert.equal(calls[0].path, "/api/sales/discounts");
  assert.equal(calls[0].init.method, "POST");
  assert.equal(calls[0].init.headers["X-CSRF-TOKEN"], "token-2");
  assert.equal(calls[0].init.headers["Content-Type"], "application/json");
  assert.deepEqual(JSON.parse(calls[0].init.body), body);
});

test("cancelDiscount posts without a body and checks the id", async t => {
  const calls = [];
  t.mock.method(globalThis, "fetch", async (path, init) => {
    calls.push({ path, init });
    return Response.json({ ...discount, status: "Cancelled", cancelledAt: "2026-11-27T12:00:00Z" });
  });
  assert.equal((await cancelDiscount(9, "t")).status, "Cancelled");
  assert.equal(calls[0].path, "/api/sales/discounts/9/cancel");
  assert.equal(calls[0].init.body, undefined);
  assert.throws(() => cancelDiscount(0, "t"), { status: 400 });
});

test("failed writes throw the status and nothing from the body", async t => {
  t.mock.method(globalThis, "fetch", async () => Response.json({ detail: "secret" }, { status: 403 }));
  await assert.rejects(createDiscount({ name: "x", rate: 0.1, endsAt: "2026-12-01T00:00:00Z", productIds: [1] }, "t"),
    error => error.status === 403 && !JSON.stringify(error).includes("secret"));
});

test("a non-JSON success body is an invalid response", async t => {
  t.mock.method(globalThis, "fetch", async () => new Response("<html>", { status: 200 }));
  await assert.rejects(fetchDiscounts(), { kind: "invalid-response" });
});

import test from "node:test";
import assert from "node:assert/strict";
import { CartError, fetchCart, parseCart, removeCartItem } from "./cart-api.js";

const line = {
  productId: 10, name: "Phone Ten", unitPrice: 100.5, quantity: 2, stockQuantity: 5,
  imageUrl: null, lineTotal: 201, available: true
};
const cart = { items: [line], itemCount: 2, total: 201 };

function mockFetch(response) {
  const calls = [];
  globalThis.fetch = async (path, init) => {
    calls.push({ path, init });
    return response;
  };
  return calls;
}

test("parseCart accepts the documented shape, including an empty cart", () => {
  assert.equal(parseCart(cart), cart);
  const empty = { items: [], itemCount: 0, total: 0 };
  assert.equal(parseCart(empty), empty);
});

test("parseCart rejects a body that is not a cart", () => {
  assert.equal(parseCart(null), null);
  assert.equal(parseCart({}), null);
  assert.equal(parseCart({ items: "x", itemCount: 0, total: 0 }), null);
  assert.equal(parseCart({ items: [], itemCount: 1.5, total: 0 }), null);
  assert.equal(parseCart({ items: [], itemCount: 0, total: -1 }), null);
  assert.equal(parseCart({ ...cart, items: [{ ...line, quantity: 0 }] }), null);
  assert.equal(parseCart({ ...cart, items: [{ ...line, available: "yes" }] }), null);
  assert.equal(parseCart({ ...cart, items: [{ ...line, unitPrice: "100" }] }), null);
});

test("fetchCart reads GET /api/cart", async () => {
  const calls = mockFetch(Response.json(cart));
  assert.deepEqual(await fetchCart(), cart);
  assert.equal(calls[0].path, "/api/cart");
  assert.equal(calls[0].init.method, "GET");
});

test("fetchCart reports the HTTP status of a failure", async () => {
  mockFetch(new Response("{}", { status: 503 }));
  await assert.rejects(fetchCart(), error => error instanceof CartError && error.status === 503);
});

test("fetchCart rejects an unusable body without a status", async () => {
  mockFetch(new Response("not json"));
  await assert.rejects(fetchCart(), error => error instanceof CartError && error.status === undefined);
  mockFetch(Response.json({ items: [{}] }));
  await assert.rejects(fetchCart(), error => error instanceof CartError && error.status === undefined);
});

test("removeCartItem sends DELETE with the CSRF token and returns the cart", async () => {
  const calls = mockFetch(Response.json({ items: [], itemCount: 0, total: 0 }));
  const result = await removeCartItem(10, "token-1");
  assert.deepEqual(result, { items: [], itemCount: 0, total: 0 });
  assert.equal(calls[0].path, "/api/cart/items/10");
  assert.equal(calls[0].init.method, "DELETE");
  assert.equal(calls[0].init.headers["X-CSRF-TOKEN"], "token-1");
});

test("removeCartItem refuses a product id that is not a positive integer", () => {
  for (const id of [0, -1, 1.5, "10", NaN]) {
    assert.throws(() => removeCartItem(id, "t"), CartError);
  }
});

import test from "node:test";
import assert from "node:assert/strict";
import { addWishlistItem, fetchWishlist, isWishlistItem, removeWishlistItem, WishlistError } from "./wishlist-api.js";

const item = {
  productId: 7, addedAt: "2026-11-12T10:00:00Z",
  product: { id: 7, name: "Phone", price: 99, stockQuantity: 1 }
};

test("wishlist item validation requires owner-safe public product data", () => {
  assert.equal(isWishlistItem(item), true);
  assert.equal(isWishlistItem({ ...item, productId: 0 }), false);
  assert.equal(isWishlistItem({ ...item, addedAt: "invalid" }), false);
  assert.equal(isWishlistItem({ ...item, product: { ...item.product, price: -1 } }), false);
});

test("fetchWishlist reads and validates the current customer's items", async t => {
  t.mock.method(globalThis, "fetch", async (path, init) => {
    assert.equal(path, "/api/customer/wishlist");
    assert.deepEqual(init.headers, { Accept: "application/json" });
    return Response.json([item]);
  });
  assert.deepEqual(await fetchWishlist(), [item]);
});

test("wishlist writes send CSRF protection and keep authorization errors", async t => {
  const calls = [];
  t.mock.method(globalThis, "fetch", async (path, init) => {
    calls.push({ path, init });
    return new Response(null, { status: 204 });
  });
  await addWishlistItem(7, "token");
  await removeWishlistItem(7, "token");
  assert.deepEqual(calls.map(call => [call.path, call.init.method]), [
    ["/api/customer/wishlist/7", "POST"], ["/api/customer/wishlist/7", "DELETE"]
  ]);
  assert.equal(calls[0].init.headers["X-CSRF-TOKEN"], "token");
  globalThis.fetch.mock.mockImplementation(async () => new Response(null, { status: 403 }));
  await assert.rejects(fetchWishlist(), error => error instanceof WishlistError && error.status === 403);
});

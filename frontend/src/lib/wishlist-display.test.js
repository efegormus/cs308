import test from "node:test";
import assert from "node:assert/strict";
import { newestFirst, removeButtonLabel, removeErrorMessage, wishlistCountLabel } from "./wishlist-display.js";

test("wishlistCountLabel says empty, one product or many", () => {
  assert.equal(wishlistCountLabel(0), "Your wishlist is empty.");
  assert.equal(wishlistCountLabel(1), "1 product saved.");
  assert.equal(wishlistCountLabel(3), "3 products saved.");
});

test("newestFirst orders by the time the product was added, newest first", () => {
  const items = [
    { productId: 1, addedAt: "2026-11-01T10:00:00Z" },
    { productId: 2, addedAt: "2026-11-03T10:00:00Z" },
    { productId: 3, addedAt: "2026-11-02T10:00:00Z" }
  ];
  assert.deepEqual(newestFirst(items).map(item => item.productId), [2, 3, 1]);
});

test("newestFirst keeps the API order for equal times and does not change its input", () => {
  const items = [
    { productId: 1, addedAt: "2026-11-01T10:00:00Z" },
    { productId: 2, addedAt: "2026-11-01T10:00:00Z" },
    { productId: 3, addedAt: "2026-11-01T10:00:00Z" }
  ];
  const copy = structuredClone(items);
  assert.deepEqual(newestFirst(items).map(item => item.productId), [1, 2, 3]);
  assert.deepEqual(items, copy);
  assert.deepEqual(newestFirst([]), []);
});

test("newestFirst puts an unreadable time last instead of failing", () => {
  const items = [
    { productId: 1, addedAt: "not a date" },
    { productId: 2, addedAt: "2026-11-01T10:00:00Z" }
  ];
  assert.deepEqual(newestFirst(items).map(item => item.productId), [2, 1]);
});

test("removeButtonLabel shows progress only while removing", () => {
  assert.equal(removeButtonLabel(false), "Remove");
  assert.equal(removeButtonLabel(true), "Removing…");
});

test("removeErrorMessage asks to log in again only for an expired session", () => {
  assert.match(removeErrorMessage(401), /Log in again/);
  for (const status of [undefined, 403, 404, 500]) assert.match(removeErrorMessage(status), /try again/);
});

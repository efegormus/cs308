import test from "node:test";
import assert from "node:assert/strict";
import { addToCartError, clampQuantity, maxAddQuantity } from "./add-to-cart.js";

test("maxAddQuantity is the stock, capped at 99, and 0 when there is nothing to add", () => {
  assert.equal(maxAddQuantity(5), 5);
  assert.equal(maxAddQuantity(500), 99);
  assert.equal(maxAddQuantity(0), 0);
  assert.equal(maxAddQuantity(-3), 0);
  assert.equal(maxAddQuantity(undefined), 0);
  assert.equal(maxAddQuantity(2.5), 0);
});

test("clampQuantity keeps the box between 1 and the maximum", () => {
  assert.equal(clampQuantity("3", 5), 3);
  assert.equal(clampQuantity("9", 5), 5);
  assert.equal(clampQuantity("0", 5), 1);
  assert.equal(clampQuantity("-2", 5), 1);
  assert.equal(clampQuantity("", 5), 1);
  assert.equal(clampQuantity("abc", 5), 1);
  assert.equal(clampQuantity("2.9", 5), 2);
});

test("addToCartError gives a fixed sentence per status", () => {
  assert.match(addToCartError(409), /stock/i);
  assert.match(addToCartError(404), /no longer available/i);
  assert.match(addToCartError(403), /Staff/);
  assert.match(addToCartError(500), /could not be added/i);
  assert.match(addToCartError(undefined), /could not be added/i);
});

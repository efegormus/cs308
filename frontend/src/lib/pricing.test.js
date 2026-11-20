import test from "node:test";
import assert from "node:assert/strict";
import { hasDiscount } from "./pricing.js";

test("a lower paid price shows both list and sale prices", () => assert.equal(hasDiscount(90, 100), true));
test("a free discounted item still shows the original price", () => assert.equal(hasDiscount(0, 100), true));
test("unchanged and old responses show a single price", () => {
  for (const value of [100, null, undefined]) assert.equal(hasDiscount(100, value), false);
});
test("invalid metadata never becomes a sale label", () => {
  for (const [price, original] of [[-1, 100], [90, "100"], [90, Infinity], [NaN, 100], [100, 90]])
    assert.equal(hasDiscount(price, original), false);
});

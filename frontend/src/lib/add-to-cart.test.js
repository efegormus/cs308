import test from "node:test";
import assert from "node:assert/strict";
import { addToCartError, clampQuantity, maxAddQuantity, withCsrfRetry } from "./add-to-cart.js";

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

function failure(status) {
  return Object.assign(new Error("failed"), { status });
}

test("withCsrfRetry sends once with the current token", async () => {
  const sent = [];
  const result = await withCsrfRetry(async token => { sent.push(token); return "cart"; },
    async () => "token-1", async () => assert.fail("no refresh needed"));
  assert.equal(result, "cart");
  assert.deepEqual(sent, ["token-1"]);
});

test("withCsrfRetry gets a new token after a 400 and tries once more", async () => {
  let token = "old";
  const sent = [];
  const result = await withCsrfRetry(async value => {
    sent.push(value);
    if (value === "old") throw failure(400);
    return "cart";
  }, async () => token, async () => { token = "new"; });
  assert.equal(result, "cart");
  assert.deepEqual(sent, ["old", "new"]);
});

test("withCsrfRetry throws a second 400 and never retries other failures", async () => {
  let refreshes = 0;
  await assert.rejects(withCsrfRetry(async () => { throw failure(400); }, async () => "t", async () => { refreshes += 1; }),
    error => error.status === 400);
  assert.equal(refreshes, 1);
  let calls = 0;
  await assert.rejects(withCsrfRetry(async () => { calls += 1; throw failure(409); }, async () => "t",
    async () => assert.fail("no refresh for 409")), error => error.status === 409);
  assert.equal(calls, 1);
});

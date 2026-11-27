import test, { afterEach } from "node:test";
import assert from "node:assert/strict";
import { cancelErrorMessage, cancelOrder, canCancelOrder, isCancelledOrder } from "./cancel-order.js";

const oldFetch = globalThis.fetch; afterEach(() => { globalThis.fetch = oldFetch; });
const answer = { orderId: 11, status: "Cancelled", cancelledAt: "2026-11-27T10:00:00Z", alreadyCancelled: false, itemsRestored: 2, refunded: 200 };

test("only a paid Processing order shows the Cancel button", () => {
  assert.equal(canCancelOrder({ status: "Processing", paymentStatus: "Confirmed" }), true);
  for (const status of ["InTransit", "Delivered", "Cancelled"]) assert.equal(canCancelOrder({ status, paymentStatus: "Confirmed" }), false);
  assert.equal(canCancelOrder({ status: "Processing", paymentStatus: "Pending" }), false);
  assert.equal(canCancelOrder(undefined), false);
});

test("the cancellation answer must match the order and the documented shape", () => {
  assert.equal(isCancelledOrder(answer, 11), true);
  assert.equal(isCancelledOrder(answer, 12), false);
  assert.equal(isCancelledOrder({ ...answer, status: "Processing" }, 11), false);
  assert.equal(isCancelledOrder({ ...answer, cancelledAt: "soon" }, 11), false);
  assert.equal(isCancelledOrder({ ...answer, itemsRestored: -1 }, 11), false);
  assert.equal(isCancelledOrder({ ...answer, alreadyCancelled: "no" }, 11), false);
});

test("cancelling posts to the order with the CSRF token and no body", async () => {
  let seen;
  globalThis.fetch = async (url, options) => { seen = { url, options }; return Response.json(answer); };
  const result = await cancelOrder(11, "token-1");
  assert.equal(seen.url, "/api/customer/orders/11/cancel");
  assert.equal(seen.options.method, "POST");
  assert.equal(seen.options.headers["X-CSRF-TOKEN"], "token-1");
  assert.equal(seen.options.body, undefined);
  assert.equal(result.itemsRestored, 2);
});

test("a repeated cancellation (alreadyCancelled) is accepted as success", async () => {
  globalThis.fetch = async () => Response.json({ ...answer, alreadyCancelled: true, itemsRestored: 0, refunded: 0 });
  assert.equal((await cancelOrder(11, "t")).alreadyCancelled, true);
});

test("an error keeps only the status and an invalid answer is rejected", async () => {
  globalThis.fetch = async () => new Response("SECRET detail", { status: 409 });
  await assert.rejects(cancelOrder(11, "t"), error => error.status === 409 && !JSON.stringify(error).includes("SECRET"));
  globalThis.fetch = async () => Response.json({ orderId: 99, status: "Cancelled" });
  await assert.rejects(cancelOrder(11, "t"), { kind: "invalid-response" });
  globalThis.fetch = async () => new Response("not json", { status: 200 });
  await assert.rejects(cancelOrder(11, "t"), { kind: "invalid-response" });
});

test("an unusable order id is refused before any request", async () => {
  globalThis.fetch = async () => { throw new Error("must not be called"); };
  for (const bad of [0, -1, 1.5, "11", Number.MAX_SAFE_INTEGER + 1]) await assert.rejects(cancelOrder(bad, "t"), { kind: "invalid-request" });
});

test("each failure has its own plain message", () => {
  assert.match(cancelErrorMessage({ status: 409 }), /no longer be cancelled/);
  assert.match(cancelErrorMessage({ status: 404 }), /could not be found/);
  assert.match(cancelErrorMessage({ status: 400 }), /try again/);
  assert.match(cancelErrorMessage({ status: 401 }), /log in again/);
  assert.match(cancelErrorMessage({ kind: "invalid-response" }), /could not be cancelled/);
});

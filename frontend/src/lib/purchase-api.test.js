import test from "node:test";
import assert from "node:assert/strict";
import { PROFILE_INCOMPLETE_TITLE, fetchQuote, fetchReceipt, recoverPurchase, submitCheckout, fetchDeliveries, transitionDelivery, mergeGuestCart, isQuote, isReceipt, needsCompleteProfile } from "./purchase-api.js";
import { normalizeRequestError } from "./request-errors.js";
const line = { productId: 1, name: "<script>literal text</script>", quantity: 1, unitPrice: 10, total: 10 };
const quote = { cartId: 1, quoteToken: "A".repeat(64), currency: "TRY", name: "Customer", taxId: "Tax", email: "test@example.com", address: "Address", items: [line], total: 10 };
const receipt = { orderId: 1, cartId: 1, currency: "TRY", status: "Processing", paymentStatus: "Confirmed", purchasedAt: "2026-01-01T12:00:00Z", deliveredAt: null, name: "Customer", taxId: "Tax", address: "Address", items: [line], total: 10, invoiceStatus: "Pending" };
const delivery = { id: 1, orderId: 1, customerId: 7, address: "Address", status: "Processing", paymentStatus: "Confirmed", createdAt: receipt.purchasedAt, deliveredAt: null, version: "123456", total: 10, canShip: true, canDeliver: false, items: [{ productId: 1, name: "Product", quantity: 1, total: 10 }] };

test("quote, receipt and pagination reject malformed success bodies", async t => {
  assert.equal(isQuote(quote), true); assert.equal(isReceipt(receipt), true);
  for (const bad of [{ ...quote, total: -1 }, { ...quote, items: [{ ...line, quantity: 0 }] }, { ...quote, quoteToken: "x" }]) assert.equal(isQuote(bad), false);
  t.mock.method(globalThis, "fetch", async () => new Response(JSON.stringify({ unexpected: "object" })));
  await assert.rejects(fetchQuote(), error => error.kind === "invalid-response");
  await assert.rejects(fetchReceipt(1), error => error.kind === "invalid-response");
});
test("lost-result recovery treats only 404 as absent, preserving other errors safely", async t => {
  t.mock.method(globalThis, "fetch", async () => new Response("not found", { status: 404 }));
  assert.equal(await recoverPurchase("00000000-0000-0000-0000-000000000000"), null);
  globalThis.fetch.mock.mockImplementation(async () => new Response("private server stack and payment data", { status: 503 }));
  await assert.rejects(fetchQuote(), error => error.status === 503 && !normalizeRequestError(error).message.includes("private"));
});
test("checkout sends only authoritative references and bounded card fields with CSRF", async t => {
  let sent;
  t.mock.method(globalThis, "fetch", async (url, init) => { sent = { url, ...init };return new Response(JSON.stringify(receipt)); });
  await submitCheckout({ ...quote, key: "00000000-0000-0000-0000-000000000000", paid: true, userId: 42 },
    { number: "test-input", expiryMonth: 12, expiryYear: 2030, securityCode: "123", paid: true }, "csrf");
  const body = JSON.parse(sent.body);
  assert.deepEqual(Object.keys(body).sort(), ["cartId", "idempotencyKey", "payment", "quoteToken"]);
  assert.deepEqual(Object.keys(body.payment).sort(), ["expiryMonth", "expiryYear", "number", "securityCode"]);
  assert.equal(sent.headers["X-CSRF-TOKEN"], "csrf");assert.equal(sent.method, "POST");
});
test("delivery transitions send the current version once and validate pagination cursors", async t => {
  const calls = [];
  t.mock.method(globalThis, "fetch", async (url, init) => { calls.push({ url, ...init });return new Response(JSON.stringify(delivery)); });
  await transitionDelivery(delivery, "InTransit", "csrf");
  assert.equal(calls.length, 1);
  assert.deepEqual(JSON.parse(calls[0].body), { status: "InTransit", version: delivery.version });
  globalThis.fetch.mock.mockImplementation(async () => new Response(JSON.stringify({ items: [delivery], nextAfterId: 2 })));
  await assert.rejects(fetchDeliveries(2), error => error.kind === "invalid-response");
});
test("guest merge preserves reported stock/full-cart conflicts", async t => {
  const data = { cart: { items: [], itemCount: 0, total: 0 }, merged: true,
    conflicts: [{ productId: 7, name: "Kept selection", reason: "CartFull", quantity: 1, stockQuantity: 3 }] };
  t.mock.method(globalThis, "fetch", async () => new Response(JSON.stringify(data)));
  assert.deepEqual((await mergeGuestCart("csrf")).conflicts, data.conflicts);
});
test("only the incomplete-profile 409 is marked for the profile redirect; the title is never forwarded", async t => {
  const problem = title => new Response(JSON.stringify({ status: 409, title }), { status: 409, headers: { "Content-Type": "application/problem+json" } });
  t.mock.method(globalThis, "fetch", async () => problem(PROFILE_INCOMPLETE_TITLE));
  await assert.rejects(fetchQuote(), error => needsCompleteProfile(error) && !JSON.stringify(error).includes("Complete your"));
  await assert.rejects(submitCheckout({ ...quote, key: "00000000-0000-0000-0000-000000000000" },
    { number: "4242424242424242", expiryMonth: 12, expiryYear: 2030, securityCode: "123" }, "csrf"), needsCompleteProfile);
  for (const other of ["Your cart is empty. Add products before checkout.", "Your carts need to be merged before checkout.", "The order changed. Reload before trying again."]) {
    globalThis.fetch.mock.mockImplementation(async () => problem(other));
    await assert.rejects(fetchQuote(), error => error.status === 409 && !needsCompleteProfile(error));
  }
  globalThis.fetch.mock.mockImplementation(async () => new Response("not json", { status: 409 }));
  await assert.rejects(fetchQuote(), error => error.status === 409 && !needsCompleteProfile(error));
  assert.equal(needsCompleteProfile({ status: 400, reason: "profile-incomplete" }), false);
});

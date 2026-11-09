import test from "node:test";
import assert from "node:assert/strict";
import { fetchOrderHistory, isOrderHistoryPage, isOrderSummary } from "./order-history-api.js";

const order = {
  orderId: 7, status: "InTransit", paymentStatus: "Confirmed", purchasedAt: "2026-11-05T09:30:00Z", deliveredAt: null,
  itemCount: 3, total: 209.99, invoiceStatus: "Ready", invoiceUrl: "/api/customer/orders/7/invoice"
};
const page = { orders: [order], nextBeforeId: null };

test("isOrderSummary accepts the documented shape and rejects broken ones", () => {
  assert.equal(isOrderSummary(order), true);
  assert.equal(isOrderSummary({ ...order, invoiceStatus: "Pending", invoiceUrl: null }), true);
  for (const bad of [null, {}, { ...order, orderId: 0 }, { ...order, status: "Shipped" }, { ...order, total: -1 },
    { ...order, itemCount: 1.5 }, { ...order, purchasedAt: "not a date" }, { ...order, invoiceStatus: "Ready", invoiceUrl: 3 }]) {
    assert.equal(isOrderSummary(bad), false, JSON.stringify(bad));
  }
});

test("isOrderHistoryPage needs a list of orders and a valid cursor", () => {
  assert.equal(isOrderHistoryPage(page), true);
  assert.equal(isOrderHistoryPage({ orders: [], nextBeforeId: 5 }), true);
  assert.equal(isOrderHistoryPage({ orders: [], nextBeforeId: 0 }), false);
  assert.equal(isOrderHistoryPage({ orders: "x", nextBeforeId: null }), false);
  assert.equal(isOrderHistoryPage({ orders: [{}], nextBeforeId: null }), false);
});

test("fetchOrderHistory asks for the first page without a cursor and the next one with beforeId", async t => {
  const urls = [];
  t.mock.method(globalThis, "fetch", async url => { urls.push(url); return new Response(JSON.stringify(page)); });
  assert.deepEqual(await fetchOrderHistory(), page);
  await fetchOrderHistory(12);
  assert.deepEqual(urls, ["/api/customer/orders", "/api/customer/orders?beforeId=12"]);
});

test("fetchOrderHistory keeps the HTTP status and rejects an unusable body", async t => {
  t.mock.method(globalThis, "fetch", async () => new Response("private details", { status: 401 }));
  await assert.rejects(fetchOrderHistory(), error => error.status === 401);
  globalThis.fetch.mock.mockImplementation(async () => new Response(JSON.stringify({ orders: "x" })));
  await assert.rejects(fetchOrderHistory(), error => error.kind === "invalid-response");
  await assert.rejects(fetchOrderHistory(-1), error => error.status === 400);
});

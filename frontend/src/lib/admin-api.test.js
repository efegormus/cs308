import test from "node:test";
import assert from "node:assert/strict";
import { fetchProductManagementDashboard, fetchSalesDashboard, isProductManagementDashboard, isSalesDashboard, salesPeriod } from "./admin-api.js";

const sales = () => ({
  from: "2026-11-01T00:00:00Z", to: "2026-11-10T00:00:00Z", currencyCode: "TRY",
  grossRevenue: 1000, refunds: 100, netRevenue: 900,
  recentInvoices: [{ orderId: 801, invoiceNumber: "INV-2026-000801", issuedAt: "2026-11-09T12:00:00Z", total: 1000, currencyCode: "TRY" }],
  activeDiscounts: [{ id: 4, name: "November", rate: 0.1, startsAt: "2026-11-01T00:00:00Z", endsAt: "2026-12-01T00:00:00Z", productCount: 3 }]
});
const product = () => ({
  lowStockProducts: [{ id: 15, sku: "EXAMPLE-15", name: "Example product", stockQuantity: 2 }],
  pendingDeliveries: [{ id: 21, orderId: 801, status: "InTransit", createdAt: "2026-11-09T12:00:00Z" }],
  commentsWaitingForApproval: [{ id: 31, productId: 15, productName: "Example product", body: "Customer review", createdAt: "2026-11-09T13:00:00Z" }]
});

test("the documented sales dashboard is accepted, including a negative net revenue and empty lists", () => {
  assert.equal(isSalesDashboard(sales()), true);
  assert.equal(isSalesDashboard({ ...sales(), netRevenue: -50, recentInvoices: [], activeDiscounts: [] }), true);
});

test("a malformed sales dashboard is refused", () => {
  for (const change of [{ grossRevenue: "1000" }, { refunds: -1 }, { netRevenue: NaN }, { from: "yesterday" },
    { recentInvoices: [{ orderId: 0 }] }, { activeDiscounts: [{ ...sales().activeDiscounts[0], rate: 1.5 }] }, { activeDiscounts: null }]) {
    assert.equal(isSalesDashboard({ ...sales(), ...change }), false, JSON.stringify(change));
  }
  assert.equal(isSalesDashboard(null), false);
});

test("the documented product-management dashboard is accepted and a malformed one refused", () => {
  assert.equal(isProductManagementDashboard(product()), true);
  assert.equal(isProductManagementDashboard({ lowStockProducts: [], pendingDeliveries: [], commentsWaitingForApproval: [] }), true);
  for (const change of [{ lowStockProducts: [{ id: 1, name: "x", stockQuantity: 1 }] }, { pendingDeliveries: [{ id: 1 }] },
    { commentsWaitingForApproval: [{ ...product().commentsWaitingForApproval[0], body: 5 }] }, { commentsWaitingForApproval: undefined }]) {
    assert.equal(isProductManagementDashboard({ ...product(), ...change }), false, JSON.stringify(change));
  }
});

test("the sales period is the last 30 days up to now, as UTC timestamps", () => {
  assert.deepEqual(salesPeriod(new Date("2026-11-13T10:00:00Z")), { from: "2026-10-14T10:00:00.000Z", to: "2026-11-13T10:00:00.000Z" });
  assert.deepEqual(salesPeriod(new Date("2026-11-13T10:00:00Z"), 7).from, "2026-11-06T10:00:00.000Z");
});

test("the clients call the documented paths and keep only the status of a failure", async t => {
  const calls = [];
  t.mock.method(globalThis, "fetch", async (url, init) => {
    calls.push([url, init.headers.Accept]);
    return url.startsWith("/api/sales")
      ? { ok: true, json: async () => sales() }
      : { ok: false, status: 403, headers: {}, json: async () => ({ title: "SECRET" }) };
  });
  const data = await fetchSalesDashboard({ from: "2026-10-14T10:00:00.000Z", to: "2026-11-13T10:00:00.000Z" });
  assert.equal(data.netRevenue, 900);
  await assert.rejects(fetchProductManagementDashboard(), error => error.status === 403 && !JSON.stringify(error).includes("SECRET"));
  assert.deepEqual(calls, [
    ["/api/sales/dashboard?from=2026-10-14T10%3A00%3A00.000Z&to=2026-11-13T10%3A00%3A00.000Z", "application/json"],
    ["/api/product-management/dashboard", "application/json"]
  ]);
});

test("an invalid response body is reported as invalid, not shown", async t => {
  t.mock.method(globalThis, "fetch", async () => ({ ok: true, json: async () => ({ lowStockProducts: "nope" }) }));
  await assert.rejects(fetchProductManagementDashboard(), { kind: "invalid-response" });
});

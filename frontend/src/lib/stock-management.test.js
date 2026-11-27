import test from "node:test";
import assert from "node:assert/strict";
import {
  buildStockRequest, isLowStock, LOW_STOCK_THRESHOLD, parseStockInput, stockFailureMessage, stockLabel, visibleStockProducts
} from "./stock-management.js";

const product = (id, name, stockQuantity, extra = {}) => ({ id, name, model: `M${id}`, sku: `SKU-${id}`, stockQuantity, isActive: true, ...extra });

test("parseStockInput accepts a whole number from 0 and nothing else", () => {
  assert.equal(parseStockInput("0"), 0);
  assert.equal(parseStockInput(" 42 "), 42);
  for (const bad of ["", " ", "-1", "1.5", "1,5", "abc", "1000001", null, undefined]) assert.equal(parseStockInput(bad), null, String(bad));
});

test("buildStockRequest sends the new count with the count the manager saw", () => {
  assert.deepEqual(buildStockRequest(product(1, "A", 12), "40"), { ok: true, request: { quantity: 40, expectedQuantity: 12 } });
  assert.deepEqual(buildStockRequest(product(1, "A", 12), "0"), { ok: true, request: { quantity: 0, expectedQuantity: 12 } });
});

test("buildStockRequest refuses a negative or broken count and an unchanged one", () => {
  for (const draft of ["-3", "", "x", "2.5"]) {
    const built = buildStockRequest(product(1, "A", 12), draft);
    assert.equal(built.ok, false, draft);
    assert.match(built.error, /below zero/);
  }
  assert.match(buildStockRequest(product(1, "A", 12), "12").error, /already the current/);
});

test("low stock means an active product with 5 or fewer, like the dashboard", () => {
  assert.equal(LOW_STOCK_THRESHOLD, 5);
  assert.equal(isLowStock(product(1, "A", 5)), true);
  assert.equal(isLowStock(product(1, "A", 6)), false);
  assert.equal(isLowStock(product(1, "A", 0)), true);
  assert.equal(isLowStock(product(1, "A", 1, { isActive: false })), false);
});

test("visibleStockProducts lists low stock first, then the lowest count, then the name", () => {
  const list = [product(1, "Zeta", 50), product(2, "Alpha", 3), product(3, "Beta", 0), product(4, "Gamma", 3), product(5, "Off sale", 1, { isActive: false })];
  assert.deepEqual(visibleStockProducts(list).map(p => p.id), [3, 2, 4, 5, 1]);
});

test("visibleStockProducts searches name, model and SKU and can show only low stock", () => {
  const list = [product(1, "Phone X", 50), product(2, "Tablet", 3), product(3, "Charger", 2)];
  assert.deepEqual(visibleStockProducts(list, { search: "  phone " }).map(p => p.id), [1]);
  assert.deepEqual(visibleStockProducts(list, { search: "m2" }).map(p => p.id), [2]);
  assert.deepEqual(visibleStockProducts(list, { search: "sku-3" }).map(p => p.id), [3]);
  assert.deepEqual(visibleStockProducts(list, { lowOnly: true }).map(p => p.id), [3, 2]);
  assert.deepEqual(visibleStockProducts(list, { search: "zzz" }), []);
});

test("visibleStockProducts does not change the list it is given", () => {
  const list = [product(2, "B", 9), product(1, "A", 1)];
  const copy = structuredClone(list);
  visibleStockProducts(list);
  assert.deepEqual(list, copy);
});

test("stockLabel names out of stock, low and in stock", () => {
  assert.equal(stockLabel(product(1, "A", 0)), "Out of stock");
  assert.equal(stockLabel(product(1, "A", 4)), "Low");
  assert.equal(stockLabel(product(1, "A", 30)), "In stock");
});

test("stockFailureMessage explains a conflict and never forwards server text", () => {
  assert.match(stockFailureMessage({ status: 409 }), /changed in the meantime/);
  assert.match(stockFailureMessage({ status: 401 }), /Log in again/);
  assert.match(stockFailureMessage({ status: 403 }), /product manager/);
  assert.match(stockFailureMessage({ status: 404 }), /no longer exists/);
  assert.match(stockFailureMessage({ kind: "invalid-response" }), /could not read/);
  assert.match(stockFailureMessage({ status: 500, message: "<b>secret</b>" }), /couldn't reach the server/);
});

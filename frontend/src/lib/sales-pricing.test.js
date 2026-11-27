import test from "node:test";
import assert from "node:assert/strict";
import {
  buildDiscountRequest, discountedPrice, isUnpriced, MAX_DISCOUNT_PRODUCTS, parsePercent, parsePrice, previewRows,
  pricingFailureMessage, toggleSelection, visibleProducts
} from "./sales-pricing.js";

const NOW = new Date("2026-11-27T10:00:00");
const product = (id, name, listPrice, model = "M1") => ({ id, name, model, listPrice, price: listPrice, discountRate: 0 });

test("a product is unpriced only when its list price is 0", () => {
  assert.equal(isUnpriced(product(1, "A", 0)), true);
  assert.equal(isUnpriced(product(1, "A", 0.01)), false);
  assert.equal(isUnpriced(undefined), false);
});

test("parsePrice accepts a positive price with at most 2 decimals, dot or comma", () => {
  assert.equal(parsePrice("149.90"), 149.9);
  assert.equal(parsePrice(" 149,9 "), 149.9);
  assert.equal(parsePrice("0.01"), 0.01);
  assert.equal(parsePrice("9999999.99"), 9999999.99);
});

test("parsePrice rejects zero, negatives, too many decimals, too large and non-numbers", () => {
  for (const bad of ["0", "0.00", "-5", "1.999", "10000000", "abc", "", "1e3", "1.2.3", null, undefined]) {
    assert.equal(parsePrice(bad), null, String(bad));
  }
});

test("parsePercent turns a percentage into a rate", () => {
  assert.equal(parsePercent("20"), 0.2);
  assert.equal(parsePercent("12,5"), 0.125);
  assert.equal(parsePercent("0.01"), 0.0001);
  assert.equal(parsePercent("99.99"), 0.9999);
});

test("parsePercent rejects 0, 100, more than 2 decimals and junk", () => {
  for (const bad of ["0", "100", "150", "-10", "12.345", "x", "", "%20"]) assert.equal(parsePercent(bad), null, bad);
});

test("discountedPrice rounds half a cent up like the server", () => {
  assert.equal(discountedPrice(100, 0.2), 80);
  assert.equal(discountedPrice(19.99, 0.15), 16.99); // 16.9915
  assert.equal(discountedPrice(0.05, 0.5), 0.03); // 0.025 rounds up
  assert.equal(discountedPrice(10.01, 0.5), 5.01); // 5.005 rounds up
  assert.equal(discountedPrice(1299.99, 0.0001), 1299.86); // 1299.859999
});

test("discountedPrice is exact where float math drifts", () => {
  assert.equal(discountedPrice(1.15, 0.1), 1.04); // 1.035 exactly
  assert.equal(discountedPrice(8.33, 0), 8.33);
});

test("previewRows shows list and new price for the selected products only, in list order", () => {
  const products = [product(1, "A", 100), product(2, "B", 50), product(3, "C", 10)];
  assert.deepEqual(previewRows(products, [3, 1], 0.2), [
    { id: 1, name: "A", listPrice: 100, newPrice: 80 },
    { id: 3, name: "C", listPrice: 10, newPrice: 8 }
  ]);
});

test("previewRows leaves the new price empty until the rate is valid", () => {
  assert.equal(previewRows([product(1, "A", 100)], [1], null)[0].newPrice, null);
});

test("visibleProducts filters the unpriced view and searches name and model", () => {
  const products = [product(1, "Galaxy Phone", 0, "S1"), product(2, "Galaxy Tab", 300, "T9"), product(3, "Pixel", 0, "P7")];
  assert.deepEqual(visibleProducts(products, { view: "unpriced" }).map(p => p.id), [1, 3]);
  assert.deepEqual(visibleProducts(products, { view: "all", search: "galaxy" }).map(p => p.id), [1, 2]);
  assert.deepEqual(visibleProducts(products, { view: "unpriced", search: " p7 " }).map(p => p.id), [3]);
  assert.deepEqual(visibleProducts(products).map(p => p.id), [1, 2, 3]);
});

test("toggleSelection adds, removes and never duplicates", () => {
  assert.deepEqual(toggleSelection([], 4), [4]);
  assert.deepEqual(toggleSelection([4, 5], 4), [5]);
  assert.deepEqual(toggleSelection(toggleSelection([1], 2), 2), [1]);
});

const form = overrides => ({ name: "Black Friday", percent: "20", startsAt: "", endsAt: "2026-11-30T23:59", productIds: [7, 3], ...overrides });

test("buildDiscountRequest builds the API body: rate, UTC end, sorted ids, no start when empty", () => {
  const result = buildDiscountRequest(form(), NOW);
  assert.equal(result.ok, true);
  assert.deepEqual(Object.keys(result.request).sort(), ["endsAt", "name", "productIds", "rate"]);
  assert.equal(result.request.rate, 0.2);
  assert.deepEqual(result.request.productIds, [3, 7]);
  assert.equal(result.request.endsAt, new Date("2026-11-30T23:59").toISOString());
});

test("buildDiscountRequest includes a scheduled start and trims the name", () => {
  const result = buildDiscountRequest(form({ name: "  Sale  ", startsAt: "2026-11-28T08:00" }), NOW);
  assert.equal(result.request.name, "Sale");
  assert.equal(result.request.startsAt, new Date("2026-11-28T08:00").toISOString());
});

test("buildDiscountRequest reports every invalid field", () => {
  const result = buildDiscountRequest({ name: " ", percent: "100", startsAt: "", endsAt: "", productIds: [] }, NOW);
  assert.equal(result.ok, false);
  assert.deepEqual(Object.keys(result.errors).sort(), ["endsAt", "name", "productIds", "rate"]);
});

test("buildDiscountRequest rejects an end in the past or before the start", () => {
  assert.match(buildDiscountRequest(form({ endsAt: "2026-11-27T09:00" }), NOW).errors.endsAt, /future/);
  assert.match(buildDiscountRequest(form({ startsAt: "2026-12-01T00:00" }), NOW).errors.endsAt, /after it starts/);
});

test("buildDiscountRequest rejects a bad start, a long name and too many products", () => {
  assert.ok(buildDiscountRequest(form({ startsAt: "tomorrow" }), NOW).errors.startsAt);
  assert.ok(buildDiscountRequest(form({ name: "x".repeat(101) }), NOW).errors.name);
  const many = Array.from({ length: MAX_DISCOUNT_PRODUCTS + 1 }, (_, i) => i + 1);
  assert.ok(buildDiscountRequest(form({ productIds: many }), NOW).errors.productIds);
});

test("pricingFailureMessage never forwards the server's body", () => {
  assert.match(pricingFailureMessage({ status: 401 }), /Log in again/);
  assert.match(pricingFailureMessage({ status: 403 }), /sales manager/);
  assert.match(pricingFailureMessage({ status: 404, detail: "secret" }), /no longer exists/);
  assert.doesNotMatch(pricingFailureMessage({ status: 500, detail: "secret" }), /secret/);
});

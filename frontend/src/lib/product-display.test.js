import test from "node:test";
import assert from "node:assert/strict";
import { formatPrice, safeImageUrl, specRow, stockLabel, stockStatus } from "./product-display.js";

const base = "http://localhost:5173/products";

test("formatPrice shows Turkish lira", () => {
  const text = formatPrice(43000.5);
  assert.ok(text.includes("43.000,50"), text);
  assert.ok(text.includes("₺"), text);
  assert.ok(formatPrice(0).includes("0,00"));
});

test("safeImageUrl keeps absolute http and https URLs", () => {
  assert.equal(safeImageUrl("https://cdn.example.com/pixel.jpg", base), "https://cdn.example.com/pixel.jpg");
  assert.equal(safeImageUrl("http://cdn.example.com/pixel.jpg", base), "http://cdn.example.com/pixel.jpg");
});

test("safeImageUrl resolves a relative URL against the page", () => {
  assert.equal(safeImageUrl("/images/pixel.jpg", base), "http://localhost:5173/images/pixel.jpg");
  assert.equal(safeImageUrl("pixel.jpg", base), "http://localhost:5173/pixel.jpg");
});

test("safeImageUrl rejects other schemes", () => {
  for (const value of ["javascript:alert(1)", "JaVaScRiPt:alert(1)", "data:image/svg+xml,<svg/>", "file:///etc/passwd",
    "ftp://example.com/a.jpg", "blob:http://localhost/1"]) {
    assert.equal(safeImageUrl(value, base), null, value);
  }
});

test("safeImageUrl returns null for a missing or unusable value", () => {
  for (const value of [undefined, null, "", 42, {}]) assert.equal(safeImageUrl(value, base), null);
  assert.equal(safeImageUrl("http://", base), null);
  assert.equal(safeImageUrl("/relative.jpg", "not a base"), null, "a relative URL needs a valid base");
});

test("stockLabel says how many are available", () => {
  assert.deepEqual(stockLabel(12), { inStock: true, text: "In stock · 12 available" });
  assert.deepEqual(stockLabel(1), { inStock: true, text: "In stock · 1 available" });
});

test("stockLabel says out of stock for 0", () => {
  assert.deepEqual(stockLabel(0), { inStock: false, text: "Out of stock" });
});

test("stockStatus gives the card's status without the count", () => {
  assert.deepEqual(stockStatus(12), { inStock: true, text: "In stock" });
  assert.deepEqual(stockStatus(1), { inStock: true, text: "In stock" });
  assert.deepEqual(stockStatus(0), { inStock: false, text: "Out of stock" });
  assert.deepEqual(stockStatus(undefined), { inStock: false, text: "Out of stock" });
});

test("specRow prefers the brand", () => {
  assert.deepEqual(specRow({ brandName: "Nova", description: "A phone" }), { kind: "brand", text: "Nova" });
});

test("specRow falls back to the first line of the description", () => {
  assert.deepEqual(specRow({ brandName: null, description: "  6.1-inch screen\r\nSecond line" }),
    { kind: "description", text: "6.1-inch screen" });
  assert.deepEqual(specRow({ description: "One line" }), { kind: "description", text: "One line" });
});

test("specRow is null without a brand or a description", () => {
  assert.equal(specRow({ brandName: null, description: null }), null);
  assert.equal(specRow({ brandName: "", description: "   " }), null);
  assert.equal(specRow({}), null);
});

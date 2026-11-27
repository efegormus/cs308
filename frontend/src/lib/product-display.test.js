import test from "node:test";
import assert from "node:assert/strict";
import { formatPrice, ratingSummary, safeImageUrl, specRow, stockLabel, stockStatus } from "./product-display.js";

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

test("ratingSummary shows the average and the count of a rated product", () => {
  const summary = ratingSummary({ averageRating: 4.3, ratingCount: 3 });
  assert.equal(summary.rated, true);
  assert.equal(summary.text, "4.3 (3)");
  assert.equal(summary.label, "Rated 4.3 out of 5 from 3 ratings");
});

test("ratingSummary uses the singular for one rating and keeps a trailing zero", () => {
  const summary = ratingSummary({ averageRating: 5, ratingCount: 1 });
  assert.equal(summary.text, "5.0 (1)");
  assert.equal(summary.label, "Rated 5.0 out of 5 from 1 rating");
});

test("ratingSummary says so when nobody rated yet", () => {
  for (const product of [{ averageRating: null, ratingCount: 0 }, { ratingCount: 0 }]) {
    assert.deepEqual(ratingSummary(product), { rated: false, count: 0, average: null, text: "No ratings yet", label: "No ratings yet" });
  }
});

test("ratingSummary returns null for data that is not a rating", () => {
  for (const product of [undefined, {}, { averageRating: 4, ratingCount: -1 }, { averageRating: 4, ratingCount: 1.5 },
    { averageRating: null, ratingCount: 2 }, { averageRating: 0, ratingCount: 2 }, { averageRating: 5.1, ratingCount: 2 },
    { averageRating: "4", ratingCount: 2 }]) {
    assert.equal(ratingSummary(product), null, JSON.stringify(product));
  }
});

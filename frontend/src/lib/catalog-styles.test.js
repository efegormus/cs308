import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

// SCRUM-134 PR 6: the catalog base uses the type scale and tokens, and the /products controls are 44px touch targets.
const read = path => readFileSync(new URL(`../${path}`, import.meta.url), "utf8");
const css = read("styles/products.css").replace(/\/\*[\s\S]*?\*\//g, "");

test("the catalog uses the type scale, not Arial, and no fixed colors", () => {
  assert.doesNotMatch(css, /Arial/);
  assert.match(css, /\.catalog-page \{[^}]*font: var\(--type-body-md\)/);
  assert.match(css, /\.catalog-page h1 \{[^}]*font: var\(--type-heading-lg\)/);
  assert.deepEqual(css.match(/#[0-9a-fA-F]{3,8}\b|\b(?:white|black)\b(?!-)|rgba?\(/g) ?? [], []);
});

test("filter fields and category links are 44px, and no plain-button copy is left", () => {
  assert.match(css, /\.product-filters input, \.catalog-page \.product-filters select \{[^}]*min-height: var\(--touch-target\)/);
  assert.match(css, /\.category-nav a \{[^}]*min-height: var\(--touch-target\)/);
  // Every button on a .catalog-page is the shared Button now, so the old plain-button rule is gone.
  assert.doesNotMatch(css, /button:not\(\.ui-button\)/);
});

test("the product card title link has a 44px hit area without moving the title", () => {
  assert.match(css, /\.product-card h3 a \{ display: inline-block; padding-block: 10px; margin-block: -10px;/);
});

test("the /products buttons are the shared secondary Button", () => {
  const page = read("pages/ProductsPage.jsx");
  assert.doesNotMatch(page, /<button/);
  assert.equal((page.match(/<Button variant="secondary"/g) ?? []).length, 4);
});

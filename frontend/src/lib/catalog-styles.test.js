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

test("the sidebar links, chips, check box row and breadcrumb links are 44px, and the old filter rules are gone", () => {
  for (const selector of [/\.catalog-category-link \{/, /\.catalog-chip \{/, /\.catalog-check \{/, /\.catalog-breadcrumb a \{/]) {
    const rule = css.slice(css.search(selector));
    assert.match(rule.slice(0, rule.indexOf("}")), /min-height: var\(--touch-target\)/, String(selector));
  }
  assert.doesNotMatch(css, /\.product-filters|\.category-nav|\.catalog-pagination|\.sort-note/);
  // Every button on a .catalog-page is the shared Button or the filter chip, so the old plain-button rule is gone.
  assert.doesNotMatch(css, /button:not\(\.ui-button\)/);
});

test("the sidebar is 264px beside the sections and goes above them up to 768px", () => {
  assert.match(css, /--catalog-sidebar-width: 264px/);
  assert.match(css, /\.catalog-layout \{[^}]*grid-template-columns: var\(--catalog-sidebar-width\) minmax\(0, 1fr\)/);
  assert.match(css, /@media \(max-width: 768px\) \{\s*\.catalog-page \.catalog-layout \{ grid-template-columns: minmax\(0, 1fr\); \}/);
  assert.match(css, /\.product-grid \{[^}]*repeat\(3, minmax\(0, 1fr\)\)/);
});

test("the product card title link has a 44px hit area without moving the title", () => {
  assert.match(css, /\.product-card h3 a \{ display: inline-block; padding-block: 10px; margin-block: -10px;/);
});

test("/products uses the shared Button and Input, with no old filter form, global sort or pagination", () => {
  const page = read("pages/ProductsPage.jsx");
  const sidebar = read("components/CatalogSidebar.jsx");
  const section = read("components/CategorySection.jsx");
  // The only plain <button> is the removable filter chip.
  assert.equal((page.match(/<button/g) ?? []).length, 1);
  assert.match(page, /className="catalog-chip"/);
  assert.doesNotMatch(sidebar + section, /<button|<select/);
  assert.doesNotMatch(page, /product-filters|CategoryNav|catalog-pagination|"Sort by"|>Order</);
  assert.match(sidebar, /<Button type="submit" variant="secondary" className="catalog-price-apply">Apply<\/Button>/);
  assert.match(sidebar, /<Button variant="secondary" className="clear-filters"/);
  assert.match(section, /<Input select className="category-section-sort"/);
  assert.match(section, /<Button variant="secondary" className="category-section-toggle"/);
});

test("the header's category links filter /products with ?categoryId= instead of scrolling to #category-<id>", () => {
  const menu = read("components/CategoryMenu.jsx");
  assert.match(menu, /const categoryHref = id => categoryProductsPath\(id\);/);
  assert.doesNotMatch(menu, /#\$\{categoryAnchorId|#category-|categoryIdFromHash/);
});

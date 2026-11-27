import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

// SCRUM-134 PR 5: the product page, wishlist, dashboard, support page and the shared error box use the design-system
// tokens and shared parts only.
const read = path => readFileSync(new URL(`../${path}`, import.meta.url), "utf8").replace(/\/\*[\s\S]*?\*\//g, "");
const SHEETS = ["product-detail", "wishlist", "role-home", "add-to-cart", "request-states"];

test("the stylesheets use theme tokens only, without fallback colors", () => {
  for (const name of SHEETS) {
    const css = read(`styles/${name}.css`);
    assert.deepEqual(css.match(/#[0-9a-fA-F]{3,8}\b|\b(?:white|black)\b(?!-)|rgba?\(|var\(--[\w-]+, /g) ?? [], [], name);
  }
});

test("the product page has its two-column layout back, one column up to 768px", () => {
  const css = read("styles/product-detail.css");
  assert.match(css, /\.product-page \.product-detail \{[^}]*grid-template-columns: minmax\(0, 1fr\) minmax\(0, 1fr\)/);
  assert.match(css, /@media \(max-width: 768px\) \{\s*\.product-page \.product-detail \{[^}]*grid-template-columns: minmax\(0, 1fr\)/);
  assert.match(css, /\.product-page \.product-detail \.price \{[^}]*font: var\(--type-price\)/);
  assert.match(read("pages/ProductPage.jsx"), /className="route-page catalog-page product-page"/);
});

test("stand-alone links on the product page, wishlist and dashboard are 44px touch targets", () => {
  assert.match(read("styles/product-detail.css"), /:is\(\.back-link, \.product-category, \.wishlist-login\) a \{[^}]*min-height: var\(--touch-target\)/);
  assert.match(read("styles/wishlist.css"), /\.wishlist-card h2 a \{[^}]*min-height: var\(--touch-target\)/);
  assert.match(read("styles/role-home.css"), /:is\(\.dash-section-head, \.dash-order\) > a \{[^}]*min-height: var\(--touch-target\)/);
});

test("an unavailable Button is gray at full opacity (open question 17, option A); busy buttons are not", () => {
  assert.match(read("components/ui/Button.css"), /\.ui-button--unavailable:disabled \{ background: var\(--line\); color: var\(--muted\);[^}]*opacity: 1;/);
  assert.match(read("components/ProductCard.jsx"), /<Button unavailable aria-describedby=\{nameId\}>Out of stock<\/Button>/);
  assert.match(read("components/AddToCart.jsx"), /<Button unavailable>Out of stock<\/Button>/);
  assert.match(read("components/AddToCart.jsx"), /<Button onClick=\{add\} disabled=\{busy\}>/);
});

test("the wishlist rules left products.css", () => {
  assert.doesNotMatch(read("styles/products.css"), /wishlist-(grid|card)/);
});

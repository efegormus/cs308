import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

// The purchase pages (checkout, orders, deliveries, order confirmation) must follow the light/dark theme,
// so purchases.css may only use the design-system tokens and no fixed colors (SCRUM-134).
const css = readFileSync(new URL("../styles/purchases.css", import.meta.url), "utf8");

test("purchases.css uses theme tokens and no fixed colors", () => {
  const fixed = css.match(/#[0-9a-fA-F]{3,8}\b|\b(?:white|black)\b(?!-)/g) ?? [];
  assert.deepEqual(fixed, []);
});

test("purchase cards and fields take their colors from the theme", () => {
  assert.match(css, /\.purchase-card \{[^}]*background: var\(--surface\)/);
  assert.match(css, /\.purchase-fields input \{[^}]*color: var\(--fg\)[^}]*background: var\(--surface\)/);
});

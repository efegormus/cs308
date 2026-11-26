import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

// SCRUM-137: the reviews section follows the light/dark theme, so reviews.css may only use design-system tokens.
const css = readFileSync(new URL("../styles/reviews.css", import.meta.url), "utf8");

test("reviews.css has no fixed colors", () => {
  assert.deepEqual(css.match(/#[0-9a-fA-F]{3,8}\b|\b(?:white|black)\b(?!-)/g) ?? [], []);
});
test("comments and the form are cards that use the theme surface", () => {
  assert.match(css, /\.review-comment \{[^}]*background: var\(--surface\)/);
  assert.match(css, /\.review-form \{[^}]*background: var\(--surface\)/);
});
test("fields take their colors and focus ring from the theme", () => {
  assert.match(css, /\.review-form select, \.review-form textarea \{[^}]*color: var\(--fg\)[^}]*border: 1px solid var\(--field-line\)/);
  assert.match(css, /focus-visible \{ outline: 3px solid var\(--focus\)/);
});
test("the submit button is full width on phones", () => {
  assert.match(css, /max-width: 480px\) \{ \.review-submit \{ width: 100%/);
});

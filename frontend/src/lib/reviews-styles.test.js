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
// SCRUM-134: the rating select and the comment textarea are the shared Input, which takes its colors, border and focus
// ring from the theme (ui/Input.css), so reviews.css no longer styles them.
test("fields are the shared Input, not page rules", () => {
  const reviews = readFileSync(new URL("../components/ProductReviews.jsx", import.meta.url), "utf8");
  assert.match(reviews, /<Input select label="Rating" id="review-rating"/);
  assert.match(reviews, /<Input multiline label="Comment \(optional\)" id="review-comment"/);
  assert.doesNotMatch(reviews, /<select|<textarea|<label/);
  assert.doesNotMatch(css, /\.review-form (select|textarea)/);
});

test("spacing, corners and type come from the tokens, without fallbacks", () => {
  assert.doesNotMatch(css, /\d*\.?\d+rem|var\(--[\w-]+, /);
  assert.match(css, /\.review-status \{[^}]*font: var\(--type-badge\)/);
  assert.match(css, /\.product-reviews h2 \{[^}]*font: var\(--type-heading-md\); \}/);
});
test("the submit button is full width on phones", () => {
  assert.match(css, /max-width: 480px\) \{ \.review-submit \{ width: 100%/);
});

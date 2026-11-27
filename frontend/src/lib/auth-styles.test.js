import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

// SCRUM-134: login, register and profile use the design-system tokens and the shared Card, Input and Button. auth.css
// loads on every page, so it must stay scoped, and it keeps the login and register motion unchanged.
const read = path => readFileSync(new URL(path, import.meta.url), "utf8").replace(/\/\*[\s\S]*?\*\//g, "");
const auth = read("../styles/auth.css");

test("auth.css is scoped to .auth-page and uses no fixed colors", () => {
  assert.doesNotMatch(auth, /#[0-9a-f]{3,8}\b|\b(?:white|black)\b(?!-)/i);
  for (const [, , selectors] of auth.matchAll(/(^|})\s*([^\s@{}][^{}]*)\{/g)) {
    if (/^\s*(from|to|\d+%)/.test(selectors)) continue;
    // Split on the commas between selectors, not on those inside :is(...).
    for (const selector of selectors.split(/,(?![^(]*\))/)) assert.match(selector.trim(), /(^|\s)\.auth-page\b/, selector);
  }
});

test("headings, text and button follow the type scale and the 44px button", () => {
  assert.match(auth, /\.auth-page h1 \{[^}]*font: var\(--type-heading-lg\)/);
  assert.match(auth, /\.auth-page \{[^}]*font: var\(--type-body-md\)/);
  assert.doesNotMatch(auth, /height: 48px|font-size: 28px|border-radius: 6px/);
  // The field, card and textarea copies are gone: the shared Input and Card draw them.
  assert.doesNotMatch(auth, /\.field input|\.field textarea|\.card h1/);
});

test("the login and register motion is unchanged", () => {
  assert.match(auth, /animation: auth-reveal-up 0\.85s cubic-bezier\(0\.22, 1, 0\.36, 1\) both;\s*animation-delay: calc\(120ms \+ var\(--i, 0\) \* 110ms\)/);
  assert.match(auth, /\.auth-page \.shake \{ animation: auth-shake 0\.45s cubic-bezier\(0\.36, 0\.07, 0\.19, 0\.97\) both; \}/);
  assert.match(auth, /animation: auth-fill-x 0\.95s cubic-bezier\(0\.4, 0, 0\.2, 1\) forwards/);
  assert.match(auth, /radial-gradient\(circle 90px at var\(--mx, 50%\) var\(--my, 50%\), var\(--btn-glow\), transparent\)/);
  assert.match(auth, /@media \(prefers-reduced-motion: reduce\)[\s\S]*\.auth-page \.play \.r:not\(\.done\) \{ overflow: visible; \}/);
});

test("the shared form card and textarea carry the values the auth pages had", () => {
  const card = read("../components/ui/Card.css");
  assert.match(card, /\.ui-card--form \{[^}]*padding: var\(--space-form-card-y\) var\(--space-xxl\);[^}]*border-radius: var\(--radius-card\)/);
  assert.match(card, /max-width: 480px\) \{ \.ui-card--form \{ padding: 28px var\(--space-lg\); \}/);
  const input = read("../components/ui/Input.css");
  assert.match(input, /\.ui-input--multiline \{[^}]*min-height: 88px/);
  assert.match(input, /\.ui-input\[readonly\] \{ background: var\(--bg\); color: var\(--muted\); \}/);
});

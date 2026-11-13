import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { MemoryRouter } from "react-router";
import { INCOMPLETE_MESSAGE, LOADING_LABEL, ProfileLoad, ProfileNotice, SavedStatus } from "./ProfileStates.js";
import { continueLink } from "../lib/profile-form.js";
import { saveSuccessSteps } from "../lib/auth-utils.js";

const render = (component, props) =>
  renderToStaticMarkup(createElement(MemoryRouter, null, createElement(component, props)));

test("while the profile loads the page shows a status line", () => {
  const html = render(ProfileLoad, { loading: true, error: null, onRetry: () => {} });
  assert.match(html, /role="status"/);
  assert.match(html, new RegExp(LOADING_LABEL));
  assert.equal(LOADING_LABEL, "Loading your profile…");
});

test("a failed load shows a safe alert with Try again, never the server's text", () => {
  const html = render(ProfileLoad, { loading: false, error: { status: 503, problem: { title: "SECRET" } }, onRetry: () => {} });
  assert.match(html, /role="alert"/);
  assert.match(html, /Try again/);
  assert.doesNotMatch(html, /SECRET/);
  assert.equal(render(ProfileLoad, { loading: false, error: null, onRetry: () => {} }), "");
});

test("an incomplete profile gets the note as a masked reveal line; a complete one gets none", () => {
  const html = render(ProfileNotice, { incomplete: true, index: 2 });
  assert.equal(INCOMPLETE_MESSAGE, "Add your tax ID and home address before you pay.");
  assert.match(html, /class="r" style="--i:2"/);
  assert.match(html, /class="ri profile-notice" role="status"/);
  assert.match(html, /Add your tax ID and home address before you pay\./);
  assert.equal(render(ProfileNotice, { incomplete: false, index: 2 }), "");
});

test("after a save the status line links back to a safe returnUrl, and to nothing otherwise", () => {
  const withLink = render(SavedStatus, { saved: true, link: continueLink("/checkout") });
  assert.match(withLink, /role="status"/);
  assert.match(withLink, /Your changes are saved\./);
  assert.match(withLink, /<a [^>]*href="\/checkout"[^>]*>Continue to checkout<\/a>/);
  const external = render(SavedStatus, { saved: true, link: continueLink("https://evil.example/checkout") });
  assert.doesNotMatch(external, /<a /);
  assert.equal(render(SavedStatus, { saved: false, link: continueLink("/checkout") }), "");
});

test("the save button plays fill, Saved and back to idle; reduced motion skips the fill", () => {
  assert.deepEqual(saveSuccessSteps({ reduced: false, fillMs: 950, holdMs: 1800 }).map(step => step.state), ["fill", "ok", "idle"]);
  assert.deepEqual(saveSuccessSteps({ reduced: false, fillMs: 950, holdMs: 1800 })[1], { state: "ok", ms: 1800 });
  assert.deepEqual(saveSuccessSteps({ reduced: true, fillMs: 950, holdMs: 1800 }), [{ state: "ok", ms: 1800 }, { state: "idle", ms: 0 }]);
});

test("profile.css is scoped, uses tokens only and turns its card entrance off for reduced motion", () => {
  const css = readFileSync(new URL("../styles/profile.css", import.meta.url), "utf8");
  const rules = css.replace(/\/\*[\s\S]*?\*\//g, "");
  assert.doesNotMatch(rules, /#[0-9a-f]{3,8}\b|rgba?\(/i, "colors come from tokens");
  for (const [, , selectors] of rules.matchAll(/(^|})\s*([^\s@{}][^{}]*)\{/g)) {
    if (/^\s*(from|to|\d+%)/.test(selectors)) continue;
    for (const selector of selectors.split(",")) assert.match(selector.trim(), /^\.profile-page /, selector);
  }
  assert.match(rules, /@media \(prefers-reduced-motion: reduce\)\s*\{\s*\.profile-page \.play \.profile-card \{ animation: none; \}/);
  assert.match(rules, /animation-delay: calc\(120ms \+ var\(--card, 0\) \* 140ms\)/);
});

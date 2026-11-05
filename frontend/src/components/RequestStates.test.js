import assert from "node:assert/strict";
import test from "node:test";
import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { ErrorState, LoadingState } from "./RequestStates.js";
import { normalizeRequestError } from "../lib/request-errors.js";

const render = (component, props) => renderToStaticMarkup(createElement(component, props));

test("loading uses a polite status announcement with an accessible label", () => {
  const html = render(LoadingState, { label: "Loading products…" });
  assert.match(html, /role="status"/);
  assert.match(html, /aria-live="polite"/);
  assert.match(html, /Loading products/);
});

test("errors announce only safe messages, never server HTML, validation values or stack traces", () => {
  const html = render(ErrorState, { error: {
    status: 500, message: "SECRET", stack: "SECRET", problem: { title: "<b>SECRET</b>", detail: "SECRET" }
  } });
  assert.match(html, /role="alert"/);
  assert.doesNotMatch(html, /SECRET|<b>|<button/);
  assert.equal(render(ErrorState, {}), "");
  assert.equal(render(ErrorState, { error: new DOMException("Cancelled", "AbortError") }), "");
});

test("retry appears only when supplied, is a non-submit button, and never runs during render", () => {
  let calls = 0;
  const onRetry = () => calls++;
  const props = { error: { status: 503 }, onRetry };
  assert.match(render(ErrorState, props), /<button type="button">Try again<\/button>/);
  assert.match(render(ErrorState, { ...props, retrying: true }), /disabled=""/);
  assert.equal(calls, 0);
  // Invoke the button's actual event handler, as a click would; nothing schedules retries.
  ErrorState(props).props.children[1].props.onClick();
  assert.equal(calls, 1);
});

test("rate-limit information survives passage from the hook to the component", () => {
  const error = normalizeRequestError({ status: 429, headers: { "Retry-After": "30" } });
  assert.match(render(ErrorState, { error }), /Try again in 30 seconds/);
});

import assert from "node:assert/strict";
import test from "node:test";
import { FOCUSABLE, isBackdropClick, nextFocusIndex } from "./modal-focus.js";

test("Tab moves forward and wraps from the last element to the first", () => {
  assert.equal(nextFocusIndex(3, 0), 1);
  assert.equal(nextFocusIndex(3, 1), 2);
  assert.equal(nextFocusIndex(3, 2), 0);
});

test("Shift+Tab moves back and wraps from the first element to the last", () => {
  assert.equal(nextFocusIndex(3, 2, true), 1);
  assert.equal(nextFocusIndex(3, 0, true), 2);
});

test("focus outside the list goes to the first element, or the last one backwards", () => {
  assert.equal(nextFocusIndex(3, -1), 0);
  assert.equal(nextFocusIndex(3, -1, true), 2);
  assert.equal(nextFocusIndex(3, 7), 0);
});

test("one element keeps the focus and none gives -1", () => {
  assert.equal(nextFocusIndex(1, 0), 0);
  assert.equal(nextFocusIndex(1, 0, true), 0);
  assert.equal(nextFocusIndex(0, -1), -1);
  assert.equal(nextFocusIndex(Number.NaN, 0), -1);
});

test("only a press and release on the dialog itself is a backdrop click", () => {
  const dialog = {}, panel = {}, field = {};
  assert.equal(isBackdropClick(dialog, dialog, dialog), true);
  assert.equal(isBackdropClick(dialog, panel, dialog), false, "a drag that starts in the panel");
  assert.equal(isBackdropClick(dialog, dialog, field), false);
  assert.equal(isBackdropClick(null, null, null), false);
});

test("disabled, hidden and tabindex=-1 elements are not focusable", () => {
  assert.match(FOCUSABLE, /button:not\(\[disabled\]\)/);
  assert.match(FOCUSABLE, /input:not\(\[disabled\]\):not\(\[type=hidden\]\)/);
  assert.match(FOCUSABLE, /\[tabindex\]:not\(\[tabindex='-1'\]\)/);
});

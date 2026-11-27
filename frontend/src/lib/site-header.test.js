import test from "node:test";
import assert from "node:assert/strict";
import { COMPACT_AFTER, headerScroll, initials, listIndexAfterKey } from "./site-header.js";

test("the header shrinks when scrolling down past 72px and grows again when scrolling up", () => {
  let state = { compact: false, y: 0 };
  state = headerScroll(state, 40);
  assert.equal(state.compact, false);
  state = headerScroll(state, 300);
  assert.equal(state.compact, true);
  state = headerScroll(state, 303); // Jitter under 6px changes nothing.
  assert.deepEqual(state, { compact: true, y: 300 });
  state = headerScroll(state, 250);
  assert.equal(state.compact, false);
  state = headerScroll(state, 400);
  assert.equal(state.compact, true);
  assert.deepEqual(headerScroll(state, COMPACT_AFTER), { compact: false, y: COMPACT_AFTER });
});

test("a missing or odd scroll value counts as the top of the page", () => {
  assert.deepEqual(headerScroll(undefined, Number.NaN), { compact: false, y: 0 });
  assert.deepEqual(headerScroll({ compact: true, y: 500 }, -20), { compact: false, y: 0 });
});

test("initials take the first and last word, in Turkish upper case, and never come back empty", () => {
  assert.equal(initials("Ada Lovelace"), "AL");
  assert.equal(initials("  ilker  can  yılmaz "), "İY");
  assert.equal(initials("Cher"), "C");
  assert.equal(initials(""), "?");
  assert.equal(initials(null), "?");
  assert.equal(initials("😀 Smile"), "😀S");
});

test("arrow keys move through the list and wrap; Home and End jump; other keys are ignored", () => {
  assert.equal(listIndexAfterKey(0, "ArrowDown", 3), 1);
  assert.equal(listIndexAfterKey(2, "ArrowDown", 3), 0);
  assert.equal(listIndexAfterKey(0, "ArrowUp", 3), 2);
  assert.equal(listIndexAfterKey(1, "Home", 3), 0);
  assert.equal(listIndexAfterKey(1, "End", 3), 2);
  assert.equal(listIndexAfterKey(1, "Enter", 3), null);
  assert.equal(listIndexAfterKey(7, "ArrowDown", 3), 1);
  assert.equal(listIndexAfterKey(0, "ArrowDown", 0), null);
});

import test from "node:test";
import assert from "node:assert/strict";
import { CATEGORY_HINTS, HINT_START, HINT_TIMING, PRODUCT_HINTS, hintStep, hintText, searchHintTerms } from "./search-hints.js";

test("the hint mixes product and category terms, one after the other, using the real category names", () => {
  assert.deepEqual(searchHintTerms(["Phones", "Tablets", "Laptops"]),
    ["iPhone 16", "Phones", "Samsung Galaxy S25", "Tablets", "iPad Air", "Laptops"]);
  assert.deepEqual(searchHintTerms(["Audio"], { products: ["A", "B"], limit: 10 }), ["A", "Audio", "B"]);
});

test("without categories the hint falls back to the constant list; blanks, duplicates and long names are dropped", () => {
  assert.deepEqual(searchHintTerms([]).slice(0, 2), [PRODUCT_HINTS[0], CATEGORY_HINTS[0]]);
  assert.deepEqual(searchHintTerms(undefined).length, 6);
  assert.deepEqual(searchHintTerms(["  ", null, "ipad air", "x".repeat(40), " Gaming "], { products: ["iPad Air"], limit: 5 }),
    ["iPad Air", "Gaming"]);
});

test("the typewriter types at 65ms a character, holds 1.5s, deletes at 28ms a character, then moves on", () => {
  const terms = ["Abc", "Xy"];
  const delays = [];
  const texts = [];
  let state = HINT_START;
  for (let step = 0; step < 9; step += 1) {
    const next = hintStep(state, terms);
    state = next.state;
    delays.push(next.delay);
    texts.push(hintText(state, terms));
  }
  assert.deepEqual(texts, ["A", "Ab", "Abc", "Ab", "A", "", "X", "Xy", "X"]);
  assert.deepEqual(delays, [65, 65, HINT_TIMING.hold, 28, 28, 65, 65, 1500, 28]);
  assert.equal(state.index, 1);
});

test("the last term wraps to the first, and a one-character term works", () => {
  let next = hintStep({ index: 1, length: 1, phase: "deleting" }, ["Abc", "Z"]);
  assert.deepEqual(next.state, { index: 0, length: 0, phase: "typing" });
  next = hintStep({ index: 1, length: 0, phase: "typing" }, ["Abc", "Z"]);
  assert.deepEqual(next, { state: { index: 1, length: 1, phase: "holding" }, delay: 1500 });
});

test("characters are code points, an empty list stops the hint, and a broken state starts over", () => {
  assert.equal(hintText({ index: 0, length: 2, phase: "typing" }, ["a😀b"]), "a😀");
  assert.deepEqual(hintStep(HINT_START, []), { state: HINT_START, delay: null });
  assert.equal(hintText(HINT_START, []), "");
  assert.deepEqual(hintStep({ index: -3, length: "x" }, ["Ab"]).state, { index: 0, length: 1, phase: "typing" });
});

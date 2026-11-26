import test from "node:test";
import assert from "node:assert/strict";
import { starCount, starRow } from "./stars.js";

test("starRow always has five stars", () => {
  for (const value of [0, 1, 2.4, 3, 4.6, 5]) assert.equal([...starRow(value)].length, 5);
});
test("the average is rounded to the nearest whole star", () => {
  assert.equal(starRow(4.4), "★★★★☆");
  assert.equal(starRow(4.5), "★★★★★");
  assert.equal(starRow(2), "★★☆☆☆");
});
test("out of range values are clamped", () => {
  assert.equal(starCount(7), 5);
  assert.equal(starCount(-3), 0);
  assert.equal(starRow(0), "☆☆☆☆☆");
});
test("values that are not numbers show no filled star", () => {
  for (const value of [undefined, null, NaN, "abc", Infinity]) assert.equal(starCount(value), 0);
});
test("numeric strings from the form are accepted", () => {
  assert.equal(starRow("3"), "★★★☆☆");
});

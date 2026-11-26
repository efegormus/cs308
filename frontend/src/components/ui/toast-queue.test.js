import assert from "node:assert/strict";
import test from "node:test";
import { addToast, dismissToast, remainingAfterPause, TOAST_DURATION, TOAST_LIMIT } from "./toast-queue.js";

test("a new toast goes last with the default tone and duration", () => {
  const toasts = addToast([], { id: "a", message: "Saved." });
  assert.deepEqual(toasts, [{ id: "a", message: "Saved.", tone: "info", duration: TOAST_DURATION }]);
});

test("past the limit the oldest toast goes", () => {
  let toasts = [];
  for (const id of ["a", "b", "c", "d"]) toasts = addToast(toasts, { id, message: id });
  assert.equal(TOAST_LIMIT, 3);
  assert.deepEqual(toasts.map(toast => toast.id), ["b", "c", "d"]);
});

test("the same id replaces its toast and moves it last", () => {
  let toasts = addToast([], { id: "a", message: "One" });
  toasts = addToast(toasts, { id: "b", message: "Two" });
  toasts = addToast(toasts, { id: "a", message: "One again", tone: "success" });
  assert.deepEqual(toasts.map(toast => [toast.id, toast.message, toast.tone]), [["b", "Two", "info"], ["a", "One again", "success"]]);
});

test("an unknown tone is info and a bad duration is the default; 0 keeps the toast open", () => {
  assert.equal(addToast([], { id: "a", message: "x", tone: "warning" })[0].tone, "info");
  assert.equal(addToast([], { id: "a", message: "x", duration: -5 })[0].duration, TOAST_DURATION);
  assert.equal(addToast([], { id: "a", message: "x", duration: "soon" })[0].duration, TOAST_DURATION);
  assert.equal(addToast([], { id: "a", message: "x", duration: 0 })[0].duration, Infinity);
  assert.equal(addToast([], { id: "a", message: "x", duration: 8000 })[0].duration, 8000);
});

test("an empty message adds nothing", () => {
  const toasts = [];
  assert.equal(addToast(toasts, { id: "a", message: "  " }), toasts);
  assert.equal(addToast(toasts, { id: "a" }), toasts);
});

test("dismiss removes one toast and keeps the list when the id is unknown", () => {
  const toasts = addToast(addToast([], { id: "a", message: "A" }), { id: "b", message: "B" });
  assert.deepEqual(dismissToast(toasts, "a").map(toast => toast.id), ["b"]);
  assert.equal(dismissToast(toasts, "zzz"), toasts);
});

test("a pause keeps the time that was left", () => {
  assert.equal(remainingAfterPause(5000, 1000, 3000), 3000);
  assert.equal(remainingAfterPause(5000, 1000, 9000), 0);
  assert.equal(remainingAfterPause(5000, 1000, 500), 5000, "a clock that went back does not add time");
  assert.equal(remainingAfterPause(Infinity, 0, 99999), Infinity);
});

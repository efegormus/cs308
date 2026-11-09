import test from "node:test";
import assert from "node:assert/strict";
import { ORDER_STEPS, orderStatusLabel, stepStates } from "./order-status.js";

test("the steps are processing, in transit and delivered, in that order", () => {
  assert.deepEqual(ORDER_STEPS, ["Processing", "InTransit", "Delivered"]);
  assert.deepEqual(stepStates("Processing").map(step => step.label), ["Processing", "In transit", "Delivered"]);
});

test("orderStatusLabel shows readable text and keeps an unknown status visible", () => {
  assert.equal(orderStatusLabel("InTransit"), "In transit");
  assert.equal(orderStatusLabel("Delivered"), "Delivered");
  assert.equal(orderStatusLabel("Cancelled"), "Cancelled");
  assert.equal(orderStatusLabel("Strange"), "Strange");
});

test("stepStates marks finished, current and coming steps", () => {
  assert.deepEqual(stepStates("Processing").map(step => step.state), ["current", "todo", "todo"]);
  assert.deepEqual(stepStates("InTransit").map(step => step.state), ["done", "current", "todo"]);
  assert.deepEqual(stepStates("Delivered").map(step => step.state), ["done", "done", "current"]);
});

test("a cancelled order is not on the way to delivery", () => {
  assert.deepEqual(stepStates("Cancelled").map(step => step.state), ["todo", "todo", "todo"]);
});

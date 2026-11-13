import test from "node:test";
import assert from "node:assert/strict";
import { APPROVE_ERROR, approvalButton, approvalReducer, initialApproval, runApproval } from "./approval-row.js";
import { MOTION } from "./motion.js";

// Runs the approval with a recording dispatch and a fake wait, and returns the phases and waits in order.
async function run({ onApprove = async () => {}, reduced = false } = {}) {
  let state = initialApproval;
  const phases = [];
  const waits = [];
  const result = await runApproval({
    onApprove, reduced,
    dispatch: event => { state = approvalReducer(state, event); phases.push(state.phase); },
    wait: async ms => { waits.push([state.phase, ms]); }
  });
  return { result, phases, waits, state };
}

test("a successful approval goes idle -> pending -> approved -> closing -> removed with the motion timings", async () => {
  const { result, phases, waits, state } = await run();
  assert.equal(result, "removed");
  assert.deepEqual(phases, ["pending", "approved", "closing", "removed"]);
  assert.deepEqual(waits, [["approved", MOTION.approveHold], ["closing", MOTION.approveCollapse]]);
  assert.equal(waits[0][1], 800);
  assert.equal(waits[1][1], 250);
  assert.equal(state.error, "");
});

test("the animation starts only after onApprove resolves", async () => {
  let release;
  let state = initialApproval;
  const phases = [];
  const running = runApproval({
    onApprove: () => new Promise(resolve => { release = resolve; }),
    dispatch: event => { state = approvalReducer(state, event); phases.push(state.phase); },
    wait: async () => {}
  });
  await Promise.resolve();
  assert.deepEqual(phases, ["pending"]);
  assert.equal(approvalButton(state).approved, false);
  release();
  await running;
  assert.deepEqual(phases, ["pending", "approved", "closing", "removed"]);
});

test("reduced motion keeps the 800ms hold but closes the row with no collapse time", async () => {
  const { phases, waits } = await run({ reduced: true });
  assert.deepEqual(phases, ["pending", "approved", "closing", "removed"]);
  assert.deepEqual(waits, [["approved", 800], ["closing", 0]]);
});

test("a failed approval puts the button back and shows an error; nothing is removed", async () => {
  const { result, phases, waits, state } = await run({ onApprove: async () => { throw new Error("boom"); } });
  assert.equal(result, "failed");
  assert.deepEqual(phases, ["pending", "idle"]);
  assert.deepEqual(waits, []);
  assert.equal(state.error, APPROVE_ERROR);
  assert.deepEqual(approvalButton(state), { label: "Approve", ariaLabel: "Approve review", blocked: false, busy: false, approved: false });
  // A new click clears the error.
  assert.equal(approvalReducer(state, { type: "click" }).error, "");
});

test("only an idle row accepts a click, so a double click cannot approve twice", () => {
  for (const phase of ["pending", "approved", "closing", "removed"]) {
    const state = { phase, error: "" };
    assert.equal(approvalReducer(state, { type: "click" }), state, phase);
    assert.equal(approvalButton(state).blocked, true, phase);
  }
  assert.equal(approvalButton(initialApproval).blocked, false);
});

test("events out of order are ignored", () => {
  assert.equal(approvalReducer(initialApproval, { type: "approved" }), initialApproval);
  assert.equal(approvalReducer(initialApproval, { type: "hold-done" }), initialApproval);
  assert.equal(approvalReducer(initialApproval, { type: "collapse-done" }), initialApproval);
  const approved = { phase: "approved", error: "" };
  assert.equal(approvalReducer(approved, { type: "failed" }), approved);
  assert.equal(approvalReducer(approved, { type: "collapse-done" }), approved);
  assert.equal(approvalReducer(approved, { type: "unknown" }), approved);
});

test("the button says 'Review approved' from the moment it turns green until the row is gone", () => {
  assert.equal(approvalButton(initialApproval).ariaLabel, "Approve review");
  assert.equal(approvalButton({ phase: "pending", error: "" }).busy, true);
  for (const phase of ["approved", "closing", "removed"]) {
    const view = approvalButton({ phase, error: "" });
    assert.equal(view.ariaLabel, "Review approved", phase);
    assert.equal(view.approved, true, phase);
  }
});

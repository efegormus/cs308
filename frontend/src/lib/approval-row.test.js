import test from "node:test";
import assert from "node:assert/strict";
import {
  APPROVE_ERROR, CONFLICT_ERROR, EXPIRED_ERROR, FORBIDDEN_ERROR, REJECT_ERROR, approvalButton, approvalReducer, failureOutcome,
  initialApproval, rejectButton, runApproval
} from "./approval-row.js";
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

// ---- SCRUM-141: real decisions --------------------------------------------------------------------------------------

const httpError = status => Object.assign(new Error("hidden"), { status });

test("Reject runs the same flow as Approve and only the Reject button turns into 'Review rejected'", async () => {
  let state = initialApproval;
  const phases = [];
  const result = await runApproval({
    decision: "Reject", onApprove: async () => {},
    dispatch: event => { state = approvalReducer(state, event); phases.push(state.phase); },
    wait: async () => {}
  });
  assert.equal(result, "removed");
  assert.deepEqual(phases, ["pending", "approved", "closing", "removed"]);
  assert.deepEqual(rejectButton(state), { label: "Reject", ariaLabel: "Review rejected", blocked: true, busy: false, rejected: true });
  assert.equal(approvalButton(state).approved, false);
  assert.equal(approvalButton(state).ariaLabel, "Approve review");
});

test("while a decision is pending, both buttons are blocked and only the clicked one is busy", () => {
  const pending = approvalReducer(initialApproval, { type: "click", decision: "Reject" });
  assert.deepEqual(pending, { phase: "pending", error: "", decision: "Reject" });
  assert.equal(approvalButton(pending).blocked, true);
  assert.equal(approvalButton(pending).busy, false);
  assert.equal(rejectButton(pending).blocked, true);
  assert.equal(rejectButton(pending).busy, true);
  // A second click on either button does nothing.
  assert.equal(approvalReducer(pending, { type: "click", decision: "Approve" }), pending);
  assert.equal(approvalReducer(pending, { type: "click", decision: "Reject" }), pending);
});

test("409 (another manager decided first) puts the row back with the conflict message and is not retried", async () => {
  let calls = 0;
  const { result, phases, waits, state } = await run({ onApprove: async () => { calls += 1; throw httpError(409); } });
  assert.equal(result, "failed");
  assert.equal(calls, 1);
  assert.deepEqual(phases, ["pending", "idle"]);
  assert.deepEqual(waits, []);
  assert.equal(state.error, CONFLICT_ERROR);
  assert.equal(approvalButton(state).blocked, false);
});

test("403 shows the 'not allowed' message and 401 the session message; the row stays", async () => {
  const forbidden = await run({ onApprove: async () => { throw httpError(403); } });
  assert.deepEqual([forbidden.result, forbidden.state.phase, forbidden.state.error], ["failed", "idle", FORBIDDEN_ERROR]);
  const expired = await run({ onApprove: async () => { throw httpError(401); } });
  assert.deepEqual([expired.result, expired.state.phase, expired.state.error], ["failed", "idle", EXPIRED_ERROR]);
  assert.equal(failureOutcome({ status: 401 }), "expired");
  assert.equal(failureOutcome({ status: 403 }), "forbidden");
});

test("404 (the comment is gone) closes the row without the approved hold", async () => {
  const { result, phases, waits, state } = await run({ onApprove: async () => { throw httpError(404); } });
  assert.equal(result, "gone");
  assert.deepEqual(phases, ["pending", "closing", "removed"]);
  assert.deepEqual(waits, [["closing", MOTION.approveCollapse]]);
  assert.equal(state.error, "");
  assert.equal(approvalButton(state).approved, false, "a gone comment is not shown as approved");
  assert.equal(rejectButton(state).rejected, false);
  const reduced = await run({ reduced: true, onApprove: async () => { throw httpError(404); } });
  assert.deepEqual(reduced.waits, [["closing", 0]]);
});

test("400, a network error, a timeout or an invalid response leave the row with the decision's own retry message", async () => {
  for (const error of [httpError(400), new TypeError("Failed to fetch"), { name: "TimeoutError" }, { kind: "invalid-response" }]) {
    const approve = await run({ onApprove: async () => { throw error; } });
    assert.deepEqual([approve.result, approve.state.phase, approve.state.error], ["failed", "idle", APPROVE_ERROR]);
    let state = initialApproval;
    await runApproval({ decision: "Reject", onApprove: async () => { throw error; }, wait: async () => {},
      dispatch: event => { state = approvalReducer(state, event); } });
    assert.deepEqual([state.phase, state.error], ["idle", REJECT_ERROR]);
    assert.equal(rejectButton(state).blocked, false, "the button comes back");
  }
});

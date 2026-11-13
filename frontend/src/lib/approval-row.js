// The state of one "Approve" row in the reviews list (SCRUM-124). Pure, so Node can test every step.
//
//   idle --click--> pending --approved--> approved --hold done--> closing --collapse done--> removed
//                      \--failed--> idle (with an error message)
//
// The animation starts only after onApprove resolves. The real approval API comes with SCRUM-81; until then the page
// passes a placeholder that resolves at once.
import { MOTION } from "./motion.js";

export const APPROVE_ERROR = "Could not approve this review. Please try again.";
export const initialApproval = Object.freeze({ phase: "idle", error: "" });

export function approvalReducer(state, event) {
  switch (event?.type) {
    case "click": return state.phase === "idle" ? { phase: "pending", error: "" } : state;
    case "approved": return state.phase === "pending" ? { phase: "approved", error: "" } : state;
    case "failed": return state.phase === "pending" ? { phase: "idle", error: APPROVE_ERROR } : state;
    case "hold-done": return state.phase === "approved" ? { ...state, phase: "closing" } : state;
    case "collapse-done": return state.phase === "closing" ? { ...state, phase: "removed" } : state;
    default: return state;
  }
}

/** What the button shows in each phase. Only idle accepts a click. */
export function approvalButton(state) {
  const done = state.phase === "approved" || state.phase === "closing" || state.phase === "removed";
  return {
    label: "Approve",
    ariaLabel: done ? "Review approved" : "Approve review",
    blocked: state.phase !== "idle",
    busy: state.phase === "pending",
    approved: done
  };
}

const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));

/**
 * Runs one approval from the click to the removal. dispatch receives the events above, in order. With reduced motion
 * the row still waits approveHold, then closes with no collapse time. Returns "removed" or "failed".
 */
export async function runApproval({ onApprove, dispatch, reduced = false, wait = sleep, timing = MOTION }) {
  dispatch({ type: "click" });
  try {
    await onApprove();
  } catch {
    dispatch({ type: "failed" });
    return "failed";
  }
  dispatch({ type: "approved" });
  await wait(timing.approveHold);
  dispatch({ type: "hold-done" });
  await wait(reduced ? 0 : timing.approveCollapse);
  dispatch({ type: "collapse-done" });
  return "removed";
}

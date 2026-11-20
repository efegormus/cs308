// The state of one review row in the moderation list (SCRUM-124, decisions since SCRUM-141). Pure, so Node can test
// every step.
//
//   idle --click(Approve|Reject)--> pending --approved--> approved --hold done--> closing --collapse done--> removed
//                                      |--gone (404)----------------------------> closing
//                                      \--failed--> idle (with a message for the failure)
//
// "approved" means the server accepted the decision, whichever it was; state.decision says which button was used.
// The animation starts only after onApprove resolves, so nothing changes on the client before the server's 200.
import { MOTION } from "./motion.js";

export const APPROVE_ERROR = "Could not approve this review. Please try again.";
export const REJECT_ERROR = "Could not reject this review. Please try again.";
export const FORBIDDEN_ERROR = "You are not allowed to moderate reviews. A product manager account is required.";
export const CONFLICT_ERROR = "Another manager already decided this review. The list was refreshed.";
export const EXPIRED_ERROR = "Your session ended. Log in again before continuing.";
export const initialApproval = Object.freeze({ phase: "idle", error: "", decision: null });

/** What a failed decision means for the row: 404 removes it, 409 refreshes the list, the rest leave it in place. */
export function failureOutcome(error) {
  switch (error?.status) {
    case 401: return "expired";
    case 403: return "forbidden";
    case 404: return "gone";
    case 409: return "conflict";
    default: return "retry";
  }
}

function failureMessage(outcome, decision) {
  if (outcome === "expired") return EXPIRED_ERROR;
  if (outcome === "forbidden") return FORBIDDEN_ERROR;
  if (outcome === "conflict") return CONFLICT_ERROR;
  return decision === "Reject" ? REJECT_ERROR : APPROVE_ERROR;
}

export function approvalReducer(state, event) {
  switch (event?.type) {
    case "click": return state.phase === "idle" ? { phase: "pending", error: "", decision: event.decision ?? "Approve" } : state;
    case "approved": return state.phase === "pending" ? { ...state, phase: "approved", error: "" } : state;
    case "failed": return state.phase === "pending"
      ? { phase: "idle", error: failureMessage(event.outcome, state.decision), decision: null } : state;
    case "gone": return state.phase === "pending" ? { ...state, phase: "closing", error: "", gone: true } : state;
    case "hold-done": return state.phase === "approved" ? { ...state, phase: "closing" } : state;
    case "collapse-done": return state.phase === "closing" ? { ...state, phase: "removed" } : state;
    default: return state;
  }
}

// A gone comment (404) closes without turning either button into a decision.
const done = state => !state.gone && (state.phase === "approved" || state.phase === "closing" || state.phase === "removed");
const decided = (state, decision) => (state.decision ?? "Approve") === decision;

/** What the Approve button shows in each phase. Only idle accepts a click, on either button. */
export function approvalButton(state) {
  const mine = decided(state, "Approve");
  return {
    label: "Approve",
    ariaLabel: done(state) && mine ? "Review approved" : "Approve review",
    blocked: state.phase !== "idle",
    busy: state.phase === "pending" && mine,
    approved: done(state) && mine
  };
}

/** The same for the Reject button. */
export function rejectButton(state) {
  const mine = state.decision === "Reject";
  return {
    label: "Reject",
    ariaLabel: done(state) && mine ? "Review rejected" : "Reject review",
    blocked: state.phase !== "idle",
    busy: state.phase === "pending" && mine,
    rejected: done(state) && mine
  };
}

const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));

/**
 * Runs one decision from the click to the removal. dispatch receives the events above, in order. With reduced motion
 * the row still waits approveHold, then closes with no collapse time. A 404 (the comment is gone) closes the row
 * without the hold. Returns "removed", "gone" or "failed". It never retries.
 */
export async function runApproval({ onApprove, dispatch, decision = "Approve", reduced = false, wait = sleep, timing = MOTION }) {
  dispatch({ type: "click", decision });
  try {
    await onApprove();
  } catch (error) {
    const outcome = failureOutcome(error);
    if (outcome !== "gone") {
      dispatch({ type: "failed", outcome });
      return "failed";
    }
    dispatch({ type: "gone" });
    await wait(reduced ? 0 : timing.approveCollapse);
    dispatch({ type: "collapse-done" });
    return "gone";
  }
  dispatch({ type: "approved" });
  await wait(timing.approveHold);
  dispatch({ type: "hold-done" });
  await wait(reduced ? 0 : timing.approveCollapse);
  dispatch({ type: "collapse-done" });
  return "removed";
}

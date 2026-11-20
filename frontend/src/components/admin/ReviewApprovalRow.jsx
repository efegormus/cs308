import { useEffect, useLayoutEffect, useReducer, useRef } from "react";
import Button from "../ui/Button.jsx";
import { approvalButton, approvalReducer, initialApproval, rejectButton, runApproval } from "../../lib/approval-row.js";
import { reducedMotion } from "../../lib/motion.js";

const date = value => new Date(value).toLocaleDateString("tr-TR");

function Rating({ value }) {
  if (value === null) return <span className="admin-review-rating">No rating</span>;
  return <span className="admin-review-rating">
    <span aria-hidden="true">{"★".repeat(value)}{"☆".repeat(5 - value)}</span>
    <span className="admin-sr">{value} out of 5 stars</span>
  </span>;
}

// One review waiting for a decision (SCRUM-124, real decisions since SCRUM-141). The flow and its timings live in
// lib/approval-row.js and lib/motion.js: Approve or Reject -> onDecide(review, decision) -> the clicked button turns
// into a circle with a mark -> the row waits, then fades and closes -> onRemoved(id, result). Nothing changes before
// onDecide resolves, that is before the server's 200. A 404 closes the row at once; any other failure brings the
// buttons back and shows a message under the row. Comment and author are rendered as text, never as HTML.
export default function ReviewApprovalRow({ review, onDecide, onDecided, onFailed, onRemoved }) {
  const [state, dispatch] = useReducer(approvalReducer, initialApproval);
  const row = useRef(null);
  const mounted = useRef(true);
  const approve = approvalButton(state);
  const reject = rejectButton(state);

  useEffect(() => () => { mounted.current = false; }, []);

  // Closing (the admin-review--closing class) starts max-height from the row's real height, so it animates over the
  // full collapse time instead of jumping from "none".
  useLayoutEffect(() => {
    const element = row.current;
    if (state.phase !== "closing" || !element) return;
    element.style.maxHeight = `${element.scrollHeight}px`;
    void element.offsetHeight;
    element.style.maxHeight = "0px";
  }, [state.phase]);

  useEffect(() => {
    if (state.phase === "approved") onDecided?.(review, state.decision);
    if (state.phase === "removed") onRemoved?.(review.id, state.gone ? "gone" : "decided");
  }, [state.phase]);

  function decide(decision) {
    if (approve.blocked) return; // a second click on either button while the flow runs does nothing
    void runApproval({
      decision,
      onApprove: async () => {
        try {
          await onDecide(review, decision);
        } catch (error) {
          if (mounted.current) onFailed?.(review, error);
          throw error;
        }
      },
      dispatch: event => { if (mounted.current) dispatch(event); },
      reduced: reducedMotion()
    });
  }

  const errorId = `review-${review.id}-error`;
  // Both buttons share the Approve morph (lib/motion.js); Reject is the neutral, secondary one.
  const props = (view, done, className, doneClass) => ({
    className: done ? `admin-approve ${className} ${doneClass}` : `admin-approve ${className}`,
    "aria-label": view.ariaLabel, "aria-disabled": view.blocked || undefined, "aria-busy": view.busy || undefined,
    "aria-describedby": state.error ? errorId : undefined
  });
  const chosen = approve.approved ? " is-approved" : reject.rejected ? " is-rejected" : "";
  return <li ref={row} className={`admin-review admin-review--${state.phase}`} data-id={review.id}>
    <div className="admin-review-inner">
      <div className="admin-review-text">
        <h3 className="admin-review-product">{review.productName}</h3>
        <p className="admin-review-body">{review.body}</p>
        <p className="admin-review-meta">{review.authorName} · <Rating value={review.rating} /> · {date(review.createdAt)}</p>
      </div>
      <div className={`admin-review-actions${chosen}`}>
        <Button {...props(approve, approve.approved, "admin-accept", "is-approved")} onClick={() => decide("Approve")}>
          <span className="admin-approve-label">{approve.label}</span>
          <svg className="admin-approve-tick" viewBox="0 0 24 24" width="24" height="24" aria-hidden="true" focusable="false">
            <path d="M5 12.5l4.5 4.5L19 7.5" pathLength="1" />
          </svg>
        </Button>
        <Button {...props(reject, reject.rejected, "admin-reject", "is-rejected")} onClick={() => decide("Reject")}>
          <span className="admin-approve-label">{reject.label}</span>
          <svg className="admin-approve-tick" viewBox="0 0 24 24" width="24" height="24" aria-hidden="true" focusable="false">
            <path d="M7 7l10 10M17 7L7 17" pathLength="1" />
          </svg>
        </Button>
      </div>
    </div>
    {state.error && <p id={errorId} className="admin-review-error" role="alert">{state.error}</p>}
  </li>;
}

import { useEffect, useLayoutEffect, useReducer, useRef } from "react";
import Button from "../ui/Button.jsx";
import { approvalButton, approvalReducer, initialApproval, runApproval } from "../../lib/approval-row.js";
import { reducedMotion } from "../../lib/motion.js";

const date = value => new Date(value).toLocaleDateString("tr-TR");

// One review waiting for approval (SCRUM-124). The flow and its timings live in lib/approval-row.js and lib/motion.js:
// Approve -> onApprove() -> the button turns into a green circle with a tick -> the row waits, then fades and closes
// -> onRemoved(id). If onApprove fails the button comes back and an error shows under the row.
// onApprove is the real approval call once SCRUM-81 adds the API; until then the page passes a placeholder.
export default function ReviewApprovalRow({ review, onApprove, onApproved, onRemoved }) {
  const [state, dispatch] = useReducer(approvalReducer, initialApproval);
  const row = useRef(null);
  const mounted = useRef(true);
  const button = approvalButton(state);

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
    if (state.phase === "approved") onApproved?.(review);
    if (state.phase === "removed") onRemoved?.(review.id);
  }, [state.phase]);

  function approve() {
    if (button.blocked) return; // a second click while the flow runs does nothing
    void runApproval({
      onApprove: () => onApprove(review),
      dispatch: event => { if (mounted.current) dispatch(event); },
      reduced: reducedMotion()
    });
  }

  const errorId = `review-${review.id}-error`;
  return <li ref={row} className={`admin-review admin-review--${state.phase}`} data-id={review.id}>
    <div className="admin-review-inner">
      <div className="admin-review-text">
        <h3 className="admin-review-product">{review.productName}</h3>
        <p className="admin-review-body">{review.body}</p>
        <p className="admin-review-meta">Product #{review.productId} · {date(review.createdAt)}</p>
      </div>
      <Button className={button.approved ? "admin-approve is-approved" : "admin-approve"}
        aria-label={button.ariaLabel} aria-disabled={button.blocked || undefined} aria-busy={button.busy || undefined}
        aria-describedby={state.error ? errorId : undefined} onClick={approve}>
        <span className="admin-approve-label">{button.label}</span>
        <svg className="admin-approve-tick" viewBox="0 0 24 24" width="24" height="24" aria-hidden="true" focusable="false">
          <path d="M5 12.5l4.5 4.5L19 7.5" pathLength="1" />
        </svg>
      </Button>
    </div>
    {state.error && <p id={errorId} className="admin-review-error" role="alert">{state.error}</p>}
  </li>;
}

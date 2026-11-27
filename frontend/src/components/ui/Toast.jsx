import { useCallback, useEffect, useRef, useState } from "react";
import { addToast, dismissToast, remainingAfterPause } from "./toast-queue.js";
import "./Toast.css";

// Short notices that close by themselves (DESIGN.md, Components > Toast). A page keeps the list with useToasts() and
// renders <ToastRegion> once:
//   const toasts = useToasts();
//   toasts.show("Saved.", { tone: "success" });
//   <ToastRegion toasts={toasts.items} onDismiss={toasts.dismiss} />
// The region is a polite live region that stays on the page (empty when there is nothing to say), so screen readers
// hear every new notice; an "error" toast's text has role="alert" and is announced at once. Each toast has a 44px close button and closes after its duration,
// which pauses while the pointer is over it or focus is in it.
let nextId = 0;

export function useToasts() {
  const [items, setItems] = useState([]);
  const show = useCallback((message, options = {}) => {
    const id = options.id ?? `toast-${++nextId}`;
    setItems(previous => addToast(previous, { ...options, id, message }));
    return id;
  }, []);
  const dismiss = useCallback(id => setItems(previous => dismissToast(previous, id)), []);
  return { items, show, dismiss };
}

function ToastItem({ toast, onDismiss, closeLabel }) {
  const remaining = useRef(toast.duration);
  const started = useRef(0);
  const timer = useRef(null);

  const stop = useCallback(() => {
    if (timer.current === null) return;
    clearTimeout(timer.current);
    timer.current = null;
    remaining.current = remainingAfterPause(remaining.current, started.current, Date.now());
  }, []);
  const start = useCallback(() => {
    if (timer.current !== null || remaining.current === Infinity) return;
    started.current = Date.now();
    timer.current = setTimeout(() => onDismiss(toast.id), remaining.current);
  }, [onDismiss, toast.id]);

  useEffect(() => { start(); return stop; }, [start, stop]);

  return (
    <li className={`ui-toast ui-toast--${toast.tone}`}
      onPointerEnter={stop} onPointerLeave={start} onFocus={stop}
      onBlur={event => { if (!event.currentTarget.contains(event.relatedTarget)) start(); }}>
      <p className="ui-toast-message" role={toast.tone === "error" ? "alert" : undefined}>{toast.message}</p>
      <button type="button" className="ui-toast-close" aria-label={closeLabel} onClick={() => onDismiss(toast.id)}>
        <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" focusable="false">
          <path d="M6 6l12 12M18 6L6 18" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
        </svg>
      </button>
    </li>
  );
}

export function ToastRegion({ toasts, onDismiss, closeLabel = "Dismiss notification" }) {
  return (
    <div className="ui-toast-region" role="status" aria-live="polite" aria-relevant="additions text">
      {toasts.length > 0 && <ol className="ui-toast-list">
        {toasts.map(toast => <ToastItem key={toast.id} toast={toast} onDismiss={onDismiss} closeLabel={closeLabel} />)}
      </ol>}
    </div>
  );
}

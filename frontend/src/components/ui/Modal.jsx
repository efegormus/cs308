import { useEffect, useId, useRef } from "react";
import { FOCUSABLE, isBackdropClick, nextFocusIndex } from "./modal-focus.js";
import "./Modal.css";

// Modal dialog (DESIGN.md, Components > Modal). Built on <dialog>.showModal(), so the page behind is inert. The parent
// owns `open` and closes it in onClose, which runs on the close button, Escape and a click on the backdrop. Tab and
// Shift+Tab stay inside the dialog, and focus goes back to the element that opened it. `footer` holds the actions.
export default function Modal({ open, onClose, title, children, footer, closeLabel = "Close", className = "" }) {
  const dialog = useRef(null);
  const opener = useRef(null);
  const pressTarget = useRef(null);
  const titleId = useId();

  useEffect(() => {
    const element = dialog.current;
    if (!element) return undefined;
    if (open && !element.open) {
      opener.current = document.activeElement;
      element.showModal();
      // The first field or action gets focus; the close button only when there is nothing else.
      const focusable = [...element.querySelectorAll(FOCUSABLE)];
      (focusable.find(item => !item.classList.contains("ui-modal-close")) ?? focusable[0] ?? element).focus();
    } else if (!open && element.open) {
      element.close();
    }
    if (open) return undefined;
    // Back to the opener once the dialog is shut, when it is still on the page.
    const back = opener.current;
    opener.current = null;
    if (back && back.isConnected && typeof back.focus === "function") back.focus();
    return undefined;
  }, [open]);

  // Unmounting while open: close it so the page is not left inert, and give the focus back.
  useEffect(() => () => {
    const element = dialog.current;
    if (element?.open) element.close();
    if (opener.current?.isConnected) opener.current.focus();
  }, []);

  function keyDown(event) {
    if (event.key !== "Tab") return;
    const focusable = [...dialog.current.querySelectorAll(FOCUSABLE)].filter(item => item.getClientRects().length > 0);
    const next = nextFocusIndex(focusable.length, focusable.indexOf(document.activeElement), event.shiftKey);
    event.preventDefault();
    (next === -1 ? dialog.current : focusable[next]).focus();
  }

  return (
    <dialog ref={dialog} className={className ? `ui-modal ${className}` : "ui-modal"} aria-modal="true"
      aria-labelledby={titleId} onKeyDown={keyDown}
      // Escape fires "cancel": keep the dialog open and let the parent close it, so its state stays the truth.
      onCancel={event => { event.preventDefault(); onClose?.(); }}
      onPointerDown={event => { pressTarget.current = event.target; }}
      onClick={event => { if (isBackdropClick(dialog.current, pressTarget.current, event.target)) onClose?.(); }}>
      {open && <div className="ui-modal-panel">
        <header className="ui-modal-head">
          <h2 id={titleId} className="ui-modal-title">{title}</h2>
          <button type="button" className="ui-modal-close" aria-label={closeLabel} onClick={() => onClose?.()}>
            <svg viewBox="0 0 24 24" width="20" height="20" aria-hidden="true" focusable="false">
              <path d="M6 6l12 12M18 6L6 18" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
            </svg>
          </button>
        </header>
        <div className="ui-modal-body">{children}</div>
        {footer && <div className="ui-modal-footer">{footer}</div>}
      </div>}
    </dialog>
  );
}

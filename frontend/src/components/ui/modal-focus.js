// Pure helpers of the shared Modal (SCRUM-134), kept apart so Node can test them without a browser.

// Elements that can take focus with Tab. Disabled, hidden and tabindex="-1" elements are left out.
export const FOCUSABLE = [
  "a[href]", "area[href]", "button:not([disabled])", "input:not([disabled]):not([type=hidden])",
  "select:not([disabled])", "textarea:not([disabled])", "summary", "iframe",
  "[contenteditable]:not([contenteditable=false])", "[tabindex]:not([tabindex='-1'])"
].join(", ");

/**
 * Where Tab (or Shift+Tab when backwards) moves inside a modal with `count` focusable elements, when the element at
 * `index` has focus (-1: focus is on none of them, for example on the dialog itself). Focus wraps around, so it never
 * leaves the modal. Returns -1 when there is nothing to focus.
 */
export function nextFocusIndex(count, index, backwards = false) {
  if (!Number.isInteger(count) || count <= 0) return -1;
  if (!Number.isInteger(index) || index < 0 || index >= count) return backwards ? count - 1 : 0;
  return backwards ? (index - 1 + count) % count : (index + 1) % count;
}

/**
 * A click closes the modal only when both the press and the release landed on the backdrop, that is on the <dialog>
 * element itself and not on its panel. A text selection that starts in a field and ends outside does not close it.
 */
export function isBackdropClick(dialog, pressTarget, releaseTarget) {
  return Boolean(dialog) && pressTarget === dialog && releaseTarget === dialog;
}

// Pure state of the shared Toast (SCRUM-134), kept apart so Node can test it without a browser.

export const TOAST_DURATION = 5000;
export const TOAST_LIMIT = 3;
export const TOAST_TONES = ["info", "success", "error"];

/**
 * Adds a toast to the list. The newest is last; past `limit` the oldest goes. An unknown tone becomes "info", a
 * duration that is not a positive number becomes the default, and `0` (or Infinity) keeps the toast until it is closed.
 */
export function addToast(toasts, { id, message, tone = "info", duration = TOAST_DURATION }, limit = TOAST_LIMIT) {
  if (typeof message !== "string" || message.trim() === "") return toasts;
  const toast = {
    id, message,
    tone: TOAST_TONES.includes(tone) ? tone : "info",
    duration: duration === 0 || duration === Infinity ? Infinity
      : Number.isFinite(duration) && duration > 0 ? duration : TOAST_DURATION
  };
  return [...toasts.filter(item => item.id !== id), toast].slice(-limit);
}

export function dismissToast(toasts, id) {
  const rest = toasts.filter(item => item.id !== id);
  return rest.length === toasts.length ? toasts : rest;
}

/**
 * The time a toast has left after it was paused (pointer over it or focus in it) at `pausedAt`, when it was shown
 * at `startedAt` for `remaining` ms. Never below 0.
 */
export function remainingAfterPause(remaining, startedAt, pausedAt) {
  if (remaining === Infinity) return Infinity;
  return Math.max(0, remaining - Math.max(0, pausedAt - startedAt));
}

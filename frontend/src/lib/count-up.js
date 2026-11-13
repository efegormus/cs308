// KPI values that count from 0 to their value (SCRUM-124). Pure apart from the frame scheduler, which is passed in,
// so Node can drive it with a fake clock.
import { MOTION, easeOutCubic } from "./motion.js";

const countFormat = new Intl.NumberFormat("tr-TR", { maximumFractionDigits: 0 });
const moneyFormat = new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY" });

/** A KPI value in tr-TR: "count" as a whole number, "money" as Turkish lira. */
export function formatKpi(value, kind = "count") {
  return kind === "money" ? moneyFormat.format(value) : countFormat.format(Math.round(value));
}

/** The value shown `elapsed` ms into a count of `duration` ms from 0 to target. */
export function countUpValue(target, elapsed, duration = MOTION.countUp) {
  if (!(duration > 0) || elapsed >= duration) return target;
  return target * easeOutCubic(elapsed / duration);
}

/**
 * Counts from 0 to target with requestAnimationFrame and calls onUpdate(value) each frame; the last call is exactly
 * target. With reduced motion (or no frame scheduler) it calls onUpdate(target) once, at once. Returns a cancel function.
 */
export function startCountUp(target, onUpdate, {
  duration = MOTION.countUp,
  reduced = false,
  requestFrame = globalThis.requestAnimationFrame?.bind(globalThis),
  cancelFrame = globalThis.cancelAnimationFrame?.bind(globalThis)
} = {}) {
  if (reduced || typeof requestFrame !== "function" || !Number.isFinite(target) || target === 0) {
    onUpdate(target);
    return () => {};
  }
  let start = null;
  let handle = null;
  let stopped = false;
  const frame = time => {
    if (stopped) return;
    if (start === null) start = time;
    const elapsed = time - start;
    onUpdate(countUpValue(target, elapsed, duration));
    if (elapsed < duration) handle = requestFrame(frame);
  };
  handle = requestFrame(frame);
  return () => {
    stopped = true;
    if (handle !== null && typeof cancelFrame === "function") cancelFrame(handle);
  };
}

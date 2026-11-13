// Motion constants of the admin panel (SCRUM-124). Components and CSS read their timings from here: the panel puts
// them on its root element as CSS custom properties (motionVars), so no duration is written twice.
// prefers-reduced-motion: reduce turns all of it off (CSS media query, and reducedMotion() for the JS timers).

/** The project's one entrance curve (DESIGN.md, Motion and reduced motion). */
export const EASE_OUT = "cubic-bezier(0.22, 1, 0.36, 1)";

/** Durations and delays in milliseconds. */
export const MOTION = Object.freeze({
  sectionEnter: 220,     // a section's content: 8px from above, opacity 0 -> 1
  menuLine: 200,         // the 3px left line on a menu item
  kpiRise: 220,          // one KPI card rising in
  kpiStagger: 80,        // the gap between two KPI cards
  countUp: 700,          // a KPI value counting from 0
  shimmer: 1200,         // one pass of the skeleton gradient
  approveMorph: 200,     // the Approve button shrinking to a green circle
  approveTickDelay: 120, // before the tick starts to draw
  approveTickDraw: 250,  // the tick's stroke being drawn
  approveHold: 800,      // the approved row waits before it closes
  approveCollapse: 250   // the row fading and closing
});

/** easeOutCubic for t in [0, 1]. */
export function easeOutCubic(t) {
  const clamped = Math.min(Math.max(t, 0), 1);
  return 1 - (1 - clamped) ** 3;
}

/** True when the visitor asked for reduced motion. False where there is no matchMedia (Node, old browsers). */
export function reducedMotion(view = globalThis) {
  return typeof view?.matchMedia === "function" && view.matchMedia("(prefers-reduced-motion: reduce)").matches;
}

/** The constants as CSS custom properties for a style prop, for example { "--admin-section-ms": "220ms" }. */
export function motionVars() {
  return {
    "--admin-ease": EASE_OUT,
    "--admin-section-ms": `${MOTION.sectionEnter}ms`,
    "--admin-menu-line-ms": `${MOTION.menuLine}ms`,
    "--admin-kpi-rise-ms": `${MOTION.kpiRise}ms`,
    "--admin-kpi-stagger-ms": `${MOTION.kpiStagger}ms`,
    "--admin-shimmer-ms": `${MOTION.shimmer}ms`,
    "--admin-approve-morph-ms": `${MOTION.approveMorph}ms`,
    "--admin-approve-tick-delay-ms": `${MOTION.approveTickDelay}ms`,
    "--admin-approve-tick-ms": `${MOTION.approveTickDraw}ms`,
    "--admin-approve-collapse-ms": `${MOTION.approveCollapse}ms`
  };
}

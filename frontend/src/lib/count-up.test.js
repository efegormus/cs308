import test from "node:test";
import assert from "node:assert/strict";
import { countUpValue, formatKpi, startCountUp } from "./count-up.js";
import { EASE_OUT, MOTION, easeOutCubic, motionVars, reducedMotion } from "./motion.js";

// A fake requestAnimationFrame: each step() runs the queued callback at the given time.
function fakeFrames() {
  let queued = null;
  let cancelled = 0;
  return {
    requestFrame: callback => { queued = callback; return 1; },
    cancelFrame: () => { cancelled++; queued = null; },
    step(time) { const callback = queued; queued = null; callback?.(time); return callback !== null; },
    get pending() { return queued !== null; },
    get cancelled() { return cancelled; }
  };
}

test("the motion constants match the agreed timings", () => {
  assert.equal(EASE_OUT, "cubic-bezier(0.22, 1, 0.36, 1)");
  assert.deepEqual({ ...MOTION }, {
    sectionEnter: 220, menuLine: 200, kpiRise: 220, kpiStagger: 80, countUp: 700, shimmer: 1200,
    approveMorph: 200, approveTickDelay: 120, approveTickDraw: 250, approveHold: 800, approveCollapse: 250
  });
  assert.equal(Object.isFrozen(MOTION), true);
  const vars = motionVars();
  assert.equal(vars["--admin-section-ms"], "220ms");
  assert.equal(vars["--admin-approve-morph-ms"], "200ms");
  assert.equal(vars["--admin-approve-collapse-ms"], "250ms");
  assert.equal(vars["--admin-ease"], EASE_OUT);
});

test("easeOutCubic starts at 0, ends at 1, rises fast and clamps its input", () => {
  assert.equal(easeOutCubic(0), 0);
  assert.equal(easeOutCubic(1), 1);
  assert.equal(easeOutCubic(0.5), 0.875);
  assert.equal(easeOutCubic(-1), 0);
  assert.equal(easeOutCubic(2), 1);
});

test("countUpValue follows easeOutCubic over 700ms and holds the target afterwards", () => {
  assert.equal(countUpValue(1000, 0), 0);
  assert.equal(countUpValue(1000, 350), 875);
  assert.equal(countUpValue(1000, 700), 1000);
  assert.equal(countUpValue(1000, 5000), 1000);
  assert.equal(countUpValue(-200, 350), -175);
});

test("a count runs on animation frames for 700ms, never overshoots and ends on exactly the target", () => {
  const frames = fakeFrames();
  const seen = [];
  startCountUp(1234.5, value => seen.push(value), frames);
  for (let time = 1000; frames.pending; time += 16) frames.step(time);
  assert.ok(seen.length >= 40, `about 44 frames, got ${seen.length}`);
  assert.equal(seen[0], 0);
  assert.equal(seen.at(-1), 1234.5);
  for (let index = 1; index < seen.length; index++) {
    assert.ok(seen[index] >= seen[index - 1] && seen[index] <= 1234.5, `frame ${index}`);
  }
});

test("reduced motion shows the final value at once, with no animation frame", () => {
  const frames = fakeFrames();
  const seen = [];
  startCountUp(42, value => seen.push(value), { ...frames, reduced: true });
  assert.deepEqual(seen, [42]);
  assert.equal(frames.pending, false);
  // Without a frame scheduler (old browser, Node) the value is final at once too.
  const plain = [];
  startCountUp(7, value => plain.push(value), { requestFrame: undefined });
  assert.deepEqual(plain, [7]);
});

test("cancelling a count stops further updates", () => {
  const frames = fakeFrames();
  const seen = [];
  const cancel = startCountUp(100, value => seen.push(value), frames);
  frames.step(0);
  frames.step(100);
  cancel();
  assert.equal(frames.cancelled, 1);
  assert.equal(frames.step(200), false);
  assert.equal(seen.length, 2);
});

test("reducedMotion reads the media query and is false without matchMedia", () => {
  assert.equal(reducedMotion({ matchMedia: query => ({ matches: query === "(prefers-reduced-motion: reduce)" }) }), true);
  assert.equal(reducedMotion({ matchMedia: () => ({ matches: false }) }), false);
  assert.equal(reducedMotion({}), false);
});

test("KPI values use the tr-TR format: whole counts and Turkish lira", () => {
  // Intl uses a no-break space in currency output; compare with plain spaces.
  const plain = value => value.replace(/\s/g, " ");
  assert.equal(formatKpi(1234567), "1.234.567");
  assert.equal(formatKpi(12.6), "13");
  assert.equal(plain(formatKpi(1234.5, "money")), "₺1.234,50");
  assert.equal(plain(formatKpi(-100, "money")), "-₺100,00");
  assert.equal(formatKpi(0), "0");
});

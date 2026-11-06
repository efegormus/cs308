// The math behind the home page coverflow. Pure functions, so Node can test them.
// Every value comes from the approved prototype, docs/design/home-coverflow-prototype.html.

const TILT_DEGREES = 55;
const CARD_GAP = 12;
const NARROW_BELOW = 700;
const FLICK_MS = 160;
const MAX_FLICK = 2;
const RUBBER = 0.3;
const WHEEL_OVERSCROLL = 0.6;

export const WHEEL_SETTLE_MS = 140;
export const DRAG_THRESHOLD_PX = 6;
export const SORT_STAGGER_MS = 45;

export const DEFAULT_SORT = "best";

// What each choice in the sort dropdown asks GET /api/products for. Popularity only exists on the server.
export const SORT_CHOICES = [
  { value: "best", label: "Best sellers", query: { sort: "popularity" } },
  { value: "price-asc", label: "Price: low to high", query: { sort: "price", order: "asc" } },
  { value: "price-desc", label: "Price: high to low", query: { sort: "price", order: "desc" } }
];

export function sortQuery(value) {
  return (SORT_CHOICES.find(choice => choice.value === value) ?? SORT_CHOICES[0]).query;
}

export function clamp(value, low, high) {
  return Math.max(low, Math.min(high, value));
}

export function lerp(from, to, amount) {
  return from + (to - from) * amount;
}

// The sizes of a row whose track is this many pixels wide.
export function geometry(width) {
  const narrow = width < NARROW_BELOW;
  const cardW = narrow ? 168 : 220;
  const cardH = narrow ? 252 : 312;
  return { width, narrow, cardW, cardH, side: cardW + CARD_GAP, spacing: narrow ? 40 : 72, rest: narrow ? 16 : 290 };
}

// Where the front card sits. It starts at rest beside the category panel and reaches the center at the second product.
export function anchorAt(pos, g) {
  const center = Math.round((g.width - g.cardW) / 2);
  return lerp(g.rest, center, clamp(pos, 0, 1));
}

// Where a card sits when it is d places from the front. d is fractional while the row follows a finger.
export function place(d, anchor, g, reduced = false) {
  const distance = Math.abs(d);
  const direction = Math.sign(d);
  const x = distance <= 1
    ? anchor + d * g.side
    : anchor + direction * (g.side + (distance - 1) * g.spacing);
  const turn = Math.min(distance, 1);
  const transform = reduced
    ? `scale(${1 - 0.08 * turn})`
    : `rotateY(${-direction * TILT_DEGREES * turn}deg) scale(${1 - 0.04 * turn})`;
  const light = clamp(1 - 0.06 * turn - 0.035 * Math.max(distance - 1, 0), 0.76, 1);
  return {
    x,
    transform,
    origin: d >= 0 ? "left center" : "right center",
    z: 1000 - Math.round(distance * 20),
    light
  };
}

// Past either end the row gives a little.
export function rubber(raw, last) {
  if (raw < 0) return raw * RUBBER;
  if (raw > last) return last + (raw - last) * RUBBER;
  return raw;
}

// The product a drag lands on. A quick flick carries on, by at most two products from where the drag started.
export function settleIndex(pos, velocity, side, start, last) {
  const projected = pos - (velocity * FLICK_MS) / side;
  return clamp(Math.round(clamp(projected, start - MAX_FLICK, start + MAX_FLICK)), 0, last);
}

// The sideways distance of a wheel event in pixels, or 0 when it should scroll the page instead.
export function wheelDeltaPx({ deltaX = 0, deltaY = 0, shiftKey = false, deltaMode = 0 }) {
  const delta = Math.abs(deltaX) > Math.abs(deltaY) ? deltaX : (shiftKey ? deltaY : 0);
  return delta * (deltaMode === 1 ? 16 : 1);
}

// The next raw wheel position. One product is one card width plus the gap.
export function wheelTarget(raw, px, side, last) {
  return clamp(raw + px / side, -WHEEL_OVERSCROLL, last + WHEEL_OVERSCROLL);
}

// The category panel fades as the cards cover it.
export function panelOpacity(cover) {
  return clamp(1 - cover * 1.6, 0, 1);
}

// The small heading takes the panel's place.
export function subOpacity(cover) {
  return clamp((cover - 0.25) * 2, 0, 1);
}

export function cardEnterDelay(rowIndex, cardIndex) {
  return 420 + rowIndex * 160 + cardIndex * 80;
}

// Which icon a category gets. A category the prototype does not know gets a neutral box.
export function categoryIconKind(name) {
  const lower = String(name).toLowerCase();
  if (lower.includes("tablet")) return "tablet";
  if (lower.includes("phone")) return "phone";
  return "other";
}

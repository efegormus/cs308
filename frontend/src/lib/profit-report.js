// Client and chart geometry for the revenue / cost / profit chart (SCRUM-87; API: docs/profit-report-api.md, SCRUM-152).
// Pure, so Node can test it. Responses are checked before they are drawn; nothing from an error body is forwarded.
const text = value => typeof value === "string";
const amount = value => Number.isFinite(value);
const instant = value => text(value) && Number.isFinite(Date.parse(value));
const isoDay = value => text(value) && /^\d{4}-\d{2}-\d{2}$/.test(value) && Number.isFinite(Date.parse(`${value}T00:00:00Z`));

export const PROFIT_DAYS = 30;
const cents = value => Math.round(value * 100);

export const isProfitDay = value => isoDay(value?.date) && amount(value.revenue) && amount(value.cost) && amount(value.profit)
  && cents(value.revenue) - cents(value.cost) === cents(value.profit);

/** The report as the API returns it: days in order, and totals that equal the sum of the days. */
export function isProfitReport(value) {
  if (!(instant(value?.from) && instant(value.to) && text(value.currencyCode)
    && amount(value.revenue) && amount(value.cost) && amount(value.profit)
    && Array.isArray(value.days) && value.days.length > 0 && value.days.length <= 367 && value.days.every(isProfitDay))) return false;
  const inOrder = value.days.every((day, index) => index === 0 || day.date > value.days[index - 1].date);
  const sum = key => value.days.reduce((total, day) => total + cents(day[key]), 0);
  return inOrder && sum("revenue") === cents(value.revenue) && sum("cost") === cents(value.cost) && sum("profit") === cents(value.profit);
}

/** The last `days` UTC days including today, as the half-open range the API expects: [midnight, next midnight). */
export function profitPeriod(now = new Date(), days = PROFIT_DAYS) {
  const dayMs = 24 * 60 * 60 * 1000;
  const today = Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate());
  return { from: new Date(today - (days - 1) * dayMs).toISOString(), to: new Date(today + dayMs).toISOString() };
}

export async function fetchProfitReport({ from, to, signal } = {}) {
  signal?.throwIfAborted();
  const query = new URLSearchParams({ from, to });
  const response = await fetch(`/api/sales/reports/profit?${query}`, { signal, headers: { Accept: "application/json" } });
  if (!response.ok) throw { status: response.status, headers: response.headers };
  let data;
  try { data = await response.json(); } catch { throw { kind: "invalid-response" }; }
  if (!isProfitReport(data)) throw { kind: "invalid-response" };
  return data;
}

/** Round axis values (1, 2 or 5 times a power of ten) that cover [min, max] and always include 0. */
export function niceTicks(min, max, target = 5) {
  const low = Math.min(min, 0);
  const high = Math.max(max, 0);
  if (low === high) return [0, 1];
  const rough = (high - low) / Math.max(1, target - 1);
  const magnitude = 10 ** Math.floor(Math.log10(rough));
  const step = [1, 2, 5, 10].map(factor => factor * magnitude).find(candidate => candidate >= rough);
  const ticks = [];
  for (let value = Math.floor(low / step) * step; value <= Math.ceil(high / step) * step + step / 1e6; value += step) {
    ticks.push(Math.round(value / step) * step);
  }
  return ticks;
}

export const SERIES = Object.freeze([
  { key: "revenue", label: "Revenue" },
  { key: "cost", label: "Cost" },
  { key: "profit", label: "Profit" }
]);

/** Scales for an SVG of the given size: x by day index, y by the ticks that cover every series. */
export function chartScales(days, { width, height, left = 56, right = 16, top = 12, bottom = 28 }) {
  const values = days.flatMap(day => SERIES.map(series => day[series.key]));
  const ticks = niceTicks(Math.min(...values), Math.max(...values));
  const low = ticks[0];
  const high = ticks[ticks.length - 1];
  const plotWidth = width - left - right;
  const plotHeight = height - top - bottom;
  return {
    ticks, left, right, top, bottom, plotWidth, plotHeight,
    x: index => left + (days.length === 1 ? plotWidth / 2 : (index / (days.length - 1)) * plotWidth),
    y: value => top + (1 - (value - low) / (high - low)) * plotHeight
  };
}

/** The "M x y L x y ..." path of one series. */
export function linePath(days, key, scales) {
  return days.map((day, index) => `${index === 0 ? "M" : "L"}${scales.x(index).toFixed(1)} ${scales.y(day[key]).toFixed(1)}`).join(" ");
}

/** The day nearest to a pointer x position (in SVG units), so a hover or a key press picks one column. */
export function nearestDayIndex(days, pointerX, scales) {
  if (days.length === 1) return 0;
  const raw = Math.round(((pointerX - scales.left) / scales.plotWidth) * (days.length - 1));
  return Math.min(days.length - 1, Math.max(0, raw));
}

/** Which days get an x-axis label, at most `limit`, always the first and last. */
export function labelledDays(count, limit = 6) {
  if (count <= limit) return Array.from({ length: count }, (_, index) => index);
  const step = Math.ceil((count - 1) / (limit - 1));
  const indexes = [];
  for (let index = 0; index < count - 1; index += step) indexes.push(index);
  indexes.push(count - 1);
  return indexes;
}

/** The headline of the period: the best and worst profit day, or null when no day had any sales. */
export function profitHighlights(days) {
  const active = days.filter(day => day.revenue !== 0 || day.cost !== 0);
  if (active.length === 0) return null;
  const best = active.reduce((a, b) => b.profit > a.profit ? b : a);
  const worst = active.reduce((a, b) => b.profit < a.profit ? b : a);
  return { best, worst };
}

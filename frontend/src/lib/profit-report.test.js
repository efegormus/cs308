import test from "node:test";
import assert from "node:assert/strict";
import {
  chartScales, fetchProfitReport, isProfitReport, labelledDays, linePath, nearestDayIndex, niceTicks, profitHighlights, profitPeriod
} from "./profit-report.js";

const day = (date, revenue, cost) => ({ date, revenue, cost, profit: Math.round((revenue - cost) * 100) / 100 });
const report = {
  from: "2026-11-09T00:00:00+00:00", to: "2026-11-12T00:00:00+00:00", currencyCode: "TRY", revenue: 200, cost: 120, profit: 80,
  days: [day("2026-11-09", 300, 180), day("2026-11-10", -100, -60), day("2026-11-11", 0, 0)]
};

test("a report is valid when its days add up to its totals and profit = revenue - cost", () => {
  assert.equal(isProfitReport(report), true);
  assert.equal(isProfitReport({ ...report, revenue: 201 }), false);
  assert.equal(isProfitReport({ ...report, days: [{ ...report.days[0], profit: 1 }, ...report.days.slice(1)] }), false);
});

test("a report needs ordered, real days", () => {
  assert.equal(isProfitReport({ ...report, days: [] }), false);
  assert.equal(isProfitReport({ ...report, days: [...report.days].reverse() }), false);
  assert.equal(isProfitReport({ ...report, days: [{ ...report.days[0], date: "09-11-2026" }] }), false);
  assert.equal(isProfitReport({ ...report, days: [report.days[0], report.days[0]] }), false);
  assert.equal(isProfitReport({ ...report, from: "later" }), false);
});

test("profitPeriod is the last 30 UTC days up to the end of today, half-open", () => {
  const { from, to } = profitPeriod(new Date("2026-11-27T22:30:00Z"));
  assert.equal(from, "2026-10-29T00:00:00.000Z");
  assert.equal(to, "2026-11-28T00:00:00.000Z");
  assert.equal((Date.parse(to) - Date.parse(from)) / 86400000, 30);
});

test("fetchProfitReport reads the report endpoint and validates it", async t => {
  t.mock.method(globalThis, "fetch", async (path, init) => {
    assert.equal(path, "/api/sales/reports/profit?from=2026-11-09T00%3A00%3A00.000Z&to=2026-11-12T00%3A00%3A00.000Z");
    assert.deepEqual(init.headers, { Accept: "application/json" });
    return Response.json(report);
  });
  assert.deepEqual(await fetchProfitReport({ from: "2026-11-09T00:00:00.000Z", to: "2026-11-12T00:00:00.000Z" }), report);
});

test("fetchProfitReport reports the status of a failure and rejects a bad body", async t => {
  t.mock.method(globalThis, "fetch", async () => new Response("{}", { status: 403 }));
  await assert.rejects(fetchProfitReport({ from: "a", to: "b" }), { status: 403 });
  t.mock.restoreAll();
  t.mock.method(globalThis, "fetch", async () => Response.json({ ...report, profit: 0 }));
  await assert.rejects(fetchProfitReport({ from: "a", to: "b" }), { kind: "invalid-response" });
});

test("niceTicks are round, ascending and always include zero", () => {
  assert.deepEqual(niceTicks(0, 300), [0, 100, 200, 300]);
  assert.deepEqual(niceTicks(-100, 300), [-100, 0, 100, 200, 300]);
  assert.deepEqual(niceTicks(0, 0), [0, 1]);
  assert.ok(niceTicks(12, 87).includes(0));
  const ticks = niceTicks(-3.2, 9.7);
  assert.deepEqual([...ticks].sort((a, b) => a - b), ticks);
  assert.ok(ticks[0] <= -3.2 && ticks[ticks.length - 1] >= 9.7);
});

test("chartScales maps the first and last day to the plot edges and zero inside the plot", () => {
  const scales = chartScales(report.days, { width: 400, height: 200 });
  assert.equal(scales.x(0), scales.left);
  assert.equal(scales.x(2), scales.left + scales.plotWidth);
  assert.ok(scales.y(0) > scales.top && scales.y(0) < scales.top + scales.plotHeight); // negative values need room below zero
  assert.ok(scales.y(300) < scales.y(0));
});

test("a single day is drawn in the middle", () => {
  const scales = chartScales([day("2026-11-09", 10, 5)], { width: 400, height: 200 });
  assert.equal(scales.x(0), scales.left + scales.plotWidth / 2);
});

test("linePath starts with M and has one point per day", () => {
  const scales = chartScales(report.days, { width: 400, height: 200 });
  const path = linePath(report.days, "revenue", scales);
  assert.ok(path.startsWith("M"));
  assert.equal(path.split(/[ML]/).filter(Boolean).length, 3);
});

test("nearestDayIndex snaps the pointer to a day and clamps at the ends", () => {
  const scales = chartScales(report.days, { width: 400, height: 200 });
  assert.equal(nearestDayIndex(report.days, scales.x(1) + 3, scales), 1);
  assert.equal(nearestDayIndex(report.days, -50, scales), 0);
  assert.equal(nearestDayIndex(report.days, 9999, scales), 2);
});

test("labelledDays keeps the first and last day and at most the limit", () => {
  assert.deepEqual(labelledDays(3), [0, 1, 2]);
  const many = labelledDays(30);
  assert.equal(many[0], 0);
  assert.equal(many[many.length - 1], 29);
  assert.ok(many.length <= 7);
});

test("profitHighlights picks the best and worst day among days with activity", () => {
  const found = profitHighlights(report.days);
  assert.equal(found.best.date, "2026-11-09");
  assert.equal(found.worst.date, "2026-11-10");
  assert.equal(profitHighlights([day("2026-11-09", 0, 0)]), null);
});

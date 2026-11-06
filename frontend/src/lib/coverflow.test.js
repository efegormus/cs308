import test from "node:test";
import assert from "node:assert/strict";
import {
  anchorAt, cardEnterDelay, categoryIconKind, clamp, DEFAULT_SORT, geometry, lerp, panelOpacity, place, rubber,
  settleIndex, sortQuery, SORT_CHOICES, subOpacity, wheelDeltaPx, wheelTarget
} from "./coverflow.js";

const wide = geometry(1246);
const narrow = geometry(341);

test("geometry switches to the narrow layout below a 700px track", () => {
  assert.deepEqual(geometry(700), { width: 700, narrow: false, cardW: 220, cardH: 312, side: 232, spacing: 72, rest: 290 });
  assert.deepEqual(geometry(699), { width: 699, narrow: true, cardW: 168, cardH: 252, side: 180, spacing: 40, rest: 16 });
});

test("the anchor moves from rest to the center over the first product only", () => {
  const center = Math.round((1246 - 220) / 2);
  assert.equal(anchorAt(0, wide), 290);
  assert.equal(anchorAt(0.5, wide), (290 + center) / 2);
  assert.equal(anchorAt(1, wide), center);
  assert.equal(anchorAt(4, wide), center);
  assert.equal(anchorAt(-0.2, wide), 290);
  assert.equal(anchorAt(0, narrow), 16);
});

test("place keeps the front card flat and at the anchor", () => {
  const card = place(0, 100, wide);
  assert.equal(card.x, 100);
  assert.equal(card.transform, "rotateY(0deg) scale(1)");
  assert.equal(card.origin, "left center");
  assert.equal(card.z, 1000);
  assert.equal(card.light, 1);
});

test("place turns the neighbours away from the front by the full tilt", () => {
  const right = place(1, 100, wide);
  assert.equal(right.x, 100 + 232);
  assert.equal(right.transform, "rotateY(-55deg) scale(0.96)");
  assert.equal(right.origin, "left center");
  assert.equal(right.z, 980);
  assert.ok(Math.abs(right.light - 0.94) < 1e-9);

  const left = place(-1, 100, wide);
  assert.equal(left.x, 100 - 232);
  assert.equal(left.transform, "rotateY(55deg) scale(0.96)");
  assert.equal(left.origin, "right center");
});

test("place stacks cards further away at the side spacing", () => {
  const right = place(2.5, 100, wide);
  assert.equal(right.x, 100 + 232 + 1.5 * 72);
  assert.equal(right.transform, "rotateY(-55deg) scale(0.96)");
  assert.equal(right.z, 950);
  assert.ok(Math.abs(right.light - (1 - 0.06 - 0.035 * 1.5)) < 1e-9);

  const left = place(-2.5, 100, wide);
  assert.equal(left.x, 100 - 232 - 1.5 * 72);
  assert.equal(left.origin, "right center");
});

test("place uses the narrow spacing on a narrow track", () => {
  assert.equal(place(1, 16, narrow).x, 16 + 180);
  assert.equal(place(2.5, 16, narrow).x, 16 + 180 + 1.5 * 40);
});

test("place scales instead of turning with reduced motion", () => {
  assert.equal(place(0, 100, wide, true).transform, "scale(1)");
  assert.equal(place(1, 100, wide, true).transform, "scale(0.92)");
  assert.equal(place(-2.5, 100, wide, true).transform, "scale(0.92)");
});

test("place never darkens a card below the floor", () => {
  assert.equal(place(40, 100, wide).light, 0.76);
});

test("rubber resists past either end and leaves the middle alone", () => {
  assert.equal(rubber(2.25, 4), 2.25);
  assert.equal(rubber(0, 4), 0);
  assert.equal(rubber(4, 4), 4);
  assert.ok(Math.abs(rubber(-0.5, 4) - -0.15) < 1e-9);
  assert.ok(Math.abs(rubber(4.5, 4) - 4.15) < 1e-9);
});

test("settleIndex rounds to the nearest product without a flick", () => {
  assert.equal(settleIndex(1.4, 0, 232, 1, 4), 1);
  assert.equal(settleIndex(1.6, 0, 232, 1, 4), 2);
});

test("settleIndex follows the flick, by at most two products from the start", () => {
  // A leftward flick (negative velocity) moves forward through the row.
  assert.equal(settleIndex(1.1, -1, 232, 1, 4), 2);
  assert.equal(settleIndex(1.1, 1, 232, 1, 4), 0);
  assert.equal(settleIndex(1, -100, 232, 1, 4), 3);
  assert.equal(settleIndex(3, 100, 232, 3, 4), 1);
});

test("settleIndex stays inside the row", () => {
  assert.equal(settleIndex(-0.2, 5, 232, 0, 4), 0);
  assert.equal(settleIndex(4.2, -5, 232, 4, 4), 4);
  assert.equal(Object.is(settleIndex(-0.2, 0, 232, 0, 4), -0), false);
});

test("wheelDeltaPx only takes sideways movement", () => {
  assert.equal(wheelDeltaPx({ deltaX: 30, deltaY: 4 }), 30);
  assert.equal(wheelDeltaPx({ deltaX: 4, deltaY: 30 }), 0);
  assert.equal(wheelDeltaPx({ deltaX: 0, deltaY: 30, shiftKey: true }), 30);
  assert.equal(wheelDeltaPx({ deltaX: 3, deltaMode: 1 }), 48);
  assert.equal(wheelDeltaPx({}), 0);
});

test("wheelTarget moves one product per card width and stops 0.6 past the ends", () => {
  assert.equal(wheelTarget(1, 232, 232, 4), 2);
  assert.equal(wheelTarget(0, -9999, 232, 4), -0.6);
  assert.equal(wheelTarget(4, 9999, 232, 4), 4.6);
});

test("the panel and the small heading cross-fade", () => {
  assert.equal(panelOpacity(0), 1);
  assert.equal(panelOpacity(0.25), 0.6);
  assert.equal(panelOpacity(1), 0);
  assert.equal(subOpacity(0), 0);
  assert.equal(subOpacity(0.25), 0);
  assert.equal(subOpacity(0.5), 0.5);
  assert.equal(subOpacity(1), 1);
});

test("the sort choices ask the server for the right order", () => {
  assert.equal(DEFAULT_SORT, "best");
  assert.deepEqual(SORT_CHOICES.map(choice => choice.label), ["Best sellers", "Price: low to high", "Price: high to low"]);
  assert.deepEqual(sortQuery("best"), { sort: "popularity" });
  assert.deepEqual(sortQuery("price-asc"), { sort: "price", order: "asc" });
  assert.deepEqual(sortQuery("price-desc"), { sort: "price", order: "desc" });
  assert.deepEqual(sortQuery("unknown"), { sort: "popularity" });
});

test("cards enter one after another, later rows later", () => {
  assert.equal(cardEnterDelay(0, 0), 420);
  assert.equal(cardEnterDelay(0, 2), 580);
  assert.equal(cardEnterDelay(1, 0), 580);
  assert.equal(cardEnterDelay(1, 4), 900);
});

test("a category without its own icon gets the neutral one", () => {
  assert.equal(categoryIconKind("Phones"), "phone");
  assert.equal(categoryIconKind("Tablets"), "tablet");
  assert.equal(categoryIconKind("Laptops"), "other");
});

test("clamp and lerp", () => {
  assert.equal(clamp(5, 0, 1), 1);
  assert.equal(clamp(-5, 0, 1), 0);
  assert.equal(lerp(10, 20, 0.5), 15);
});

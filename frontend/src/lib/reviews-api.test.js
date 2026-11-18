import test, { afterEach } from "node:test";
import assert from "node:assert/strict";
import { fetchProductReviews, isProductReviews } from "./reviews-api.js";

const original = globalThis.fetch;
afterEach(() => { globalThis.fetch = original; });
const empty = { productId: 1, averageRating: null, ratingCount: 0, comments: [], nextAfterId: null };
const rated = { ...empty, averageRating: 4.5, ratingCount: 2,
  comments: [{ id: 3, authorName: "Buyer", body: "Approved", createdAt: "2026-11-18T12:00:00Z" }] };

test("accepts empty and rated product review pages", () => {
  assert.ok(isProductReviews(empty)); assert.ok(isProductReviews(rated));
});
test("rejects invalid averages and counts", () => {
  for (const value of [null, {}, { ...rated, averageRating: 6 }, { ...rated, ratingCount: -1 },
    { ...rated, averageRating: null }, { ...empty, averageRating: 0 }]) assert.equal(isProductReviews(value), false);
});
test("rejects malformed comments and cursors", () => {
  assert.equal(isProductReviews({ ...rated, comments: [{ id: 3, body: "text" }] }), false);
  assert.equal(isProductReviews({ ...rated, nextAfterId: -1 }), false);
  assert.equal(isProductReviews({ ...rated, comments: [{ ...rated.comments[0], createdAt: "invalid" }] }), false);
});
test("fetches the requested product and approved-comment cursor", async () => {
  globalThis.fetch = async (url, options) => {
    assert.equal(url, "/api/products/1/reviews?afterId=3");
    assert.equal(options.headers.Accept, "application/json");
    return new Response(JSON.stringify(rated));
  };
  assert.deepEqual(await fetchProductReviews(1, { afterId: 3 }), rated);
});
test("does not expose server error bodies", async () => {
  globalThis.fetch = async () => new Response("PRIVATE", { status: 503 });
  await assert.rejects(fetchProductReviews(1), error => error.status === 503 && !JSON.stringify(error).includes("PRIVATE"));
});
test("rejects malformed JSON and the wrong product", async () => {
  globalThis.fetch = async () => new Response("<html>");
  await assert.rejects(fetchProductReviews(1), { kind: "invalid-response" });
  globalThis.fetch = async () => new Response(JSON.stringify({ ...rated, productId: 2 }));
  await assert.rejects(fetchProductReviews(1), { kind: "invalid-response" });
});
test("refuses unsafe ids and an already aborted read without fetching", async () => {
  globalThis.fetch = () => { throw new Error("must not fetch"); };
  await assert.rejects(fetchProductReviews(-1), { kind: "invalid-request" });
  await assert.rejects(fetchProductReviews(1, { signal: AbortSignal.abort() }), error => error.name === "AbortError");
});

import test from "node:test";
import assert from "node:assert/strict";
import { decideComment, fetchComments, isModerationComment, isModerationPage } from "./moderation-api.js";

const comment = (changes = {}) => ({
  id: 31, productId: 15, productName: "Example product", authorName: "Ayşe K.", rating: 4, body: "Customer review",
  status: "Pending", createdAt: "2026-11-09T13:00:00Z", moderatedAt: null, moderationNote: null, ...changes
});
const page = (changes = {}) => ({ status: "Pending", comments: [comment()], nextAfterId: null, ...changes });
const ok = data => ({ ok: true, status: 200, json: async () => data });
const failure = status => ({ ok: false, status, headers: {}, json: async () => ({ title: "SECRET detail", detail: "SECRET" }) });

test("the documented comment and page are accepted, including no rating and an empty queue", () => {
  assert.equal(isModerationComment(comment()), true);
  assert.equal(isModerationComment(comment({ rating: null })), true);
  assert.equal(isModerationComment(comment({ status: "Approved", moderatedAt: "2026-11-10T09:00:00Z", moderationNote: "ok" })), true);
  assert.equal(isModerationPage(page()), true);
  assert.equal(isModerationPage(page({ comments: [], nextAfterId: null })), true);
  assert.equal(isModerationPage(page({ comments: [comment({ id: 50 })], nextAfterId: 50 }), "Pending", 31), true);
});

test("a malformed comment or page is refused", () => {
  for (const changes of [{ id: 0 }, { productName: null }, { authorName: 7 }, { rating: 0 }, { rating: 6 }, { rating: 4.5 },
    { rating: "4" }, { body: undefined }, { status: "Waiting" }, { createdAt: "yesterday" }, { moderatedAt: "never" }]) {
    assert.equal(isModerationComment(comment(changes)), false, JSON.stringify(changes));
  }
  assert.equal(isModerationPage(page({ status: "Approved" })), false, "another status than asked");
  assert.equal(isModerationPage(page({ comments: [comment({ status: "Approved" })] })), false, "a decided comment in the Pending list");
  assert.equal(isModerationPage(page({ nextAfterId: 0 })), false);
  assert.equal(isModerationPage(page({ comments: Array.from({ length: 21 }, (_, i) => comment({ id: i + 1 })) })), false, "more than 20");
  assert.equal(isModerationPage(page(), "Pending", 31), false, "a comment at or before the cursor");
  assert.equal(isModerationPage(page({ comments: [comment({ id: 40 })], nextAfterId: 31 }), "Pending", 31), false, "a cursor that does not move");
  assert.equal(isModerationPage(null), false);
});

test("the list calls the documented path with status and cursor", async t => {
  const calls = [];
  t.mock.method(globalThis, "fetch", async (url, init) => { calls.push([url, init.method, init.headers]); return ok(page({ comments: [comment({ id: 41 })] })); });
  const data = await fetchComments({ afterId: 40 });
  assert.equal(data.comments[0].id, 41);
  assert.deepEqual(calls, [["/api/product-management/comments?status=Pending&afterId=40", "GET", { Accept: "application/json" }]]);
  assert.throws(() => fetchComments({ afterId: -1 }), { status: 400 });
  assert.throws(() => fetchComments({ status: "pending" }), { status: 400 });
});

test("a decision is a POST with the decision body and the X-CSRF-TOKEN header", async t => {
  const calls = [];
  t.mock.method(globalThis, "fetch", async (url, init) => {
    calls.push({ url, method: init.method, headers: init.headers, body: JSON.parse(init.body) });
    const decided = JSON.parse(init.body).decision === "Approve" ? "Approved" : "Rejected";
    return ok(comment({ status: decided, moderatedAt: "2026-11-10T09:00:00Z" }));
  });
  assert.equal((await decideComment(31, "Approve", "csrf-1")).status, "Approved");
  assert.equal((await decideComment(31, "Reject", "csrf-2")).status, "Rejected");
  assert.deepEqual(calls.map(call => [call.url, call.method, call.headers["X-CSRF-TOKEN"], call.headers["Content-Type"], call.body]), [
    ["/api/product-management/comments/31/decision", "POST", "csrf-1", "application/json", { decision: "Approve" }],
    ["/api/product-management/comments/31/decision", "POST", "csrf-2", "application/json", { decision: "Reject" }]
  ]);
  assert.throws(() => decideComment(31, "approve", "x"), { status: 400 });
  assert.throws(() => decideComment(0, "Approve", "x"), { status: 400 });
  assert.equal(calls.length, 2, "an invalid decision never reaches the server");
});

test("a failed decision keeps only the status (401, 403, 404, 409, 400), never the error body", async t => {
  for (const status of [400, 401, 403, 404, 409]) {
    t.mock.method(globalThis, "fetch", async () => failure(status));
    await assert.rejects(decideComment(31, "Approve", "csrf"), error => error.status === status && !JSON.stringify(error).includes("SECRET"));
  }
  t.mock.method(globalThis, "fetch", async () => failure(403));
  await assert.rejects(fetchComments(), error => error.status === 403 && !JSON.stringify(error).includes("SECRET"));
});

test("an invalid or mismatched response is reported as invalid, not shown", async t => {
  t.mock.method(globalThis, "fetch", async () => ok({ status: "Pending", comments: "nope", nextAfterId: null }));
  await assert.rejects(fetchComments(), { kind: "invalid-response" });
  t.mock.method(globalThis, "fetch", async () => ({ ok: true, status: 200, json: async () => { throw new SyntaxError("bad"); } }));
  await assert.rejects(fetchComments(), { kind: "invalid-response" });
  // 200 for another comment, or a status that does not match the decision, is not a success.
  t.mock.method(globalThis, "fetch", async () => ok(comment({ id: 32, status: "Approved" })));
  await assert.rejects(decideComment(31, "Approve", "csrf"), { kind: "invalid-response" });
  t.mock.method(globalThis, "fetch", async () => ok(comment({ status: "Rejected" })));
  await assert.rejects(decideComment(31, "Approve", "csrf"), { kind: "invalid-response" });
});

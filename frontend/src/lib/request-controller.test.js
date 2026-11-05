import assert from "node:assert/strict";
import test from "node:test";
import { createRequestController } from "./request-controller.js";

function deferred() {
  let resolve, reject;
  const promise = new Promise((yes, no) => { resolve = yes; reject = no; });
  return { promise, resolve, reject };
}

test("idle, loading and success lifecycle; no work happens until explicitly run", async () => {
  const request = createRequestController();
  const work = deferred();
  const states = [];
  let calls = 0;
  request.subscribe(() => states.push(request.getSnapshot().status));
  assert.equal(request.getSnapshot().status, "idle");
  const running = request.run(({ signal }) => { calls++; assert.equal(signal.aborted, false); return work.promise; });
  assert.equal(request.getSnapshot().status, "loading");
  await Promise.resolve();
  assert.equal(calls, 1);
  work.resolve(["product"]);
  assert.equal((await running).status, "success");
  assert.deepEqual(request.getSnapshot().data, ["product"]);
  assert.deepEqual(states, ["loading", "success"]);
});

test("a failed mutation is invoked once; retry is an explicit new run", async () => {
  const request = createRequestController();
  let calls = 0;
  const mutation = () => { calls++; throw { status: 503, detail: "SECRET" }; };
  await request.run(mutation);
  assert.equal(calls, 1);
  assert.equal(request.getSnapshot().status, "error");
  assert.equal(request.getSnapshot().error.status, 503);
  assert.doesNotMatch(JSON.stringify(request.getSnapshot()), /SECRET/);
  await new Promise(resolve => setTimeout(resolve, 10));
  assert.equal(calls, 1);
  await request.run(mutation);
  assert.equal(calls, 2);
});

for (const lateFailure of [false, true]) {
  test(`outdated ${lateFailure ? "failure" : "success"} cannot overwrite the newer result`, async () => {
    const request = createRequestController();
    const old = deferred();
    let signal;
    const first = request.run(context => { signal = context.signal; return old.promise; });
    await Promise.resolve();
    await request.run(() => "new result");
    assert.equal(signal.aborted, true);
    assert.equal((await first).status, "cancelled");
    if (lateFailure) old.reject(new Error("old failure")); else old.resolve("old result");
    await new Promise(resolve => setTimeout(resolve, 0));
    assert.equal(request.getSnapshot().data, "new result");
  });
}

test("superseded work is not started if cancelled before execution", async () => {
  const request = createRequestController();
  let calls = 0;
  const old = request.run(() => { calls++; });
  await request.run(() => "latest");
  await old;
  assert.equal(calls, 0);
});

test("explicit cancellation aborts work, clears data and suppresses cancellation errors", async () => {
  const request = createRequestController();
  const work = deferred();
  let signal;
  const running = request.run(context => { signal = context.signal; return work.promise; });
  await Promise.resolve();
  request.cancel();
  assert.equal(signal.aborted, true);
  assert.equal((await running).status, "cancelled");
  assert.deepEqual(request.getSnapshot(), { status: "idle", data: null, error: null });
  work.resolve("late");
  await Promise.resolve();
  assert.equal(request.getSnapshot().status, "idle");
  await request.run(() => { throw new DOMException("Cancelled", "AbortError"); });
  assert.equal(request.getSnapshot().status, "idle");
});

test("unmount aborts without publishing; StrictMode reactivation accepts fresh work", async () => {
  const request = createRequestController();
  const old = deferred();
  let updates = 0, callsAfterUnmount = 0;
  const unsubscribe = request.subscribe(() => updates++);
  const running = request.run(() => old.promise);
  await Promise.resolve();
  request.deactivate();
  assert.equal((await running).status, "cancelled");
  await request.run(() => { callsAfterUnmount++; });
  assert.equal(updates, 1);
  assert.equal(callsAfterUnmount, 0);
  request.activate();
  await request.run(() => "fresh");
  old.resolve("stale");
  await Promise.resolve();
  assert.equal(request.getSnapshot().data, "fresh");
  unsubscribe();
  const previousUpdates = updates;
  request.reset();
  assert.equal(updates, previousUpdates);
  assert.equal(request.getSnapshot().status, "idle");
});

test("timeout aborts even an executor that ignores cancellation and never retries", async () => {
  const request = createRequestController();
  const work = deferred();
  let signal, calls = 0;
  await request.run(context => { calls++; signal = context.signal; return work.promise; }, { timeoutMs: 5 });
  assert.equal(signal.aborted, true);
  assert.equal(request.getSnapshot().error.kind, "timeout");
  work.resolve("too late");
  await new Promise(resolve => setTimeout(resolve, 0));
  assert.equal(request.getSnapshot().status, "error");
  assert.equal(calls, 1);
});

test("malformed JSON and unexpected success shapes become safe errors", async () => {
  const request = createRequestController();
  await request.run(() => JSON.parse("SECRET"));
  assert.equal(request.getSnapshot().error.kind, "invalid-response");
  await request.run(() => ({ secret: "SECRET" }), { validate: Array.isArray });
  assert.equal(request.getSnapshot().error.kind, "invalid-response");
  assert.doesNotMatch(JSON.stringify(request.getSnapshot()), /SECRET/);
  await request.run(() => [], { validate: Array.isArray });
  assert.equal(request.getSnapshot().status, "success");
});

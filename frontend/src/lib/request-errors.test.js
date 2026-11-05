import assert from "node:assert/strict";
import test from "node:test";
import { normalizeRequestError, parseRetryAfter } from "./request-errors.js";

const expected = new Map([
  [400, "The request is not valid. Check your details and try again."],
  [401, "Your session ended. Log in again before continuing."],
  [403, "Your account does not have access to this action."],
  [404, "The requested resource could not be found."],
  [409, "The request conflicts with the current state. Reload before trying again."],
  [429, "Too many requests. Please try again shortly."],
  [500, "The request could not be completed. Please try again."],
  [503, "The service is temporarily unavailable. Please try again shortly."]
]);

test("HTTP statuses use consistent safe messages, never server text or submitted values", () => {
  for (const [status, message] of expected) {
    const error = normalizeRequestError({ status, message: "SECRET", stack: "SECRET", problem: {
      title: "SECRET", detail: "SECRET", errors: { card: ["SECRET"] }, traceId: "SECRET"
    } });
    assert.equal(error.kind, "http");
    assert.equal(error.status, status);
    assert.equal(error.message, message);
    assert.doesNotMatch(JSON.stringify(error), /SECRET/);
  }
});

test("accepts transport errors, Response objects and problem details; transport status wins", () => {
  assert.equal(normalizeRequestError(new Response(null, { status: 503 })).status, 503);
  assert.equal(normalizeRequestError({ problem: { status: 404 } }).status, 404);
  assert.equal(normalizeRequestError({ status: 403, problem: { status: 401 } }).status, 403);
  assert.equal(normalizeRequestError({ response: { status: 500 }, status: 400 }).status, 500);
  assert.equal(normalizeRequestError({ status: 200, problem: { status: 503 } }).status, null);
  assert.equal(normalizeRequestError({ status: "500", problem: { status: 503 } }).status, null);
});

test("malformed, empty and unexpected errors have safe fallbacks", () => {
  for (const input of [null, undefined, "SECRET", "<html>SECRET</html>", {}, [],
    { status: "500" }, { status: 200 }, { status: 400.5 }, { status: 600 }, { problem: "SECRET" },
    new Error("SECRET")]) {
    const error = normalizeRequestError(input);
    assert.equal(error.status, null);
    assert.equal(error.message, "The request failed. Please try again.");
    assert.doesNotMatch(JSON.stringify(error), /SECRET/);
  }
  assert.equal(normalizeRequestError({ status: 418 }).message, "The request failed. Please try again.");
  assert.equal(normalizeRequestError({ status: 400, problem: "<html>SECRET</html>" }).status, 400);
});

test("network, timeout, cancellation and invalid response failures are distinguished safely", () => {
  for (const [input, kind] of [
    [new TypeError("SECRET"), "network"],
    [new DOMException("SECRET", "TimeoutError"), "timeout"],
    [new DOMException("SECRET", "AbortError"), "cancelled"],
    [new SyntaxError("SECRET"), "invalid-response"],
    [{ kind: "invalid-response", detail: "SECRET" }, "invalid-response"]
  ]) {
    const error = normalizeRequestError(input);
    assert.equal(error.kind, kind);
    assert.doesNotMatch(JSON.stringify(error), /SECRET/);
  }
});

test("Retry-After supports non-negative delta seconds and valid HTTP dates", () => {
  const now = Date.parse("Thu, 05 Nov 2026 12:00:00 GMT");
  assert.equal(parseRetryAfter("30", now), 30);
  assert.equal(parseRetryAfter("0", now), 0);
  assert.equal(parseRetryAfter(" 1 ", now), 1);
  assert.equal(parseRetryAfter("Thu, 05 Nov 2026 12:00:30 GMT", now + 1), 30);
  assert.equal(parseRetryAfter("Thu, 05 Nov 2026 11:59:00 GMT", now), 0);
  for (const value of [null, undefined, 30, "", "-1", "1.5", "1e3", "tomorrow", "9007199254740992",
    "Thu, 32 Nov 2026 12:00:00 GMT", "Thu, 05 Nov 2026 25:00:00 GMT"]) {
    assert.equal(parseRetryAfter(value, now), null, String(value));
  }
});

test("rate-limit messages use only valid headers and retain metadata when normalized again", () => {
  const error = normalizeRequestError({ status: 429, headers: new Headers({ "Retry-After": "30" }) });
  assert.equal(error.retryAfterSeconds, 30);
  assert.equal(error.message, "Too many requests. Try again in 30 seconds.");
  assert.equal(normalizeRequestError(error), error);
  assert.ok(Object.isFrozen(error));
  assert.match(normalizeRequestError({ status: 429, headers: { "RETRY-AFTER": "1" } }).message, /1 second\./);
  assert.equal(normalizeRequestError({ status: 429, headers: { "Retry-After": "SECRET" } }).message, expected.get(429));
  assert.equal(normalizeRequestError({ status: 503, headers: { "Retry-After": "30" } }).retryAfterSeconds, null);
});

test("Retry-After guidance is bounded to 24 hours for both seconds and dates", () => {
  const now = Date.parse("Thu, 05 Nov 2026 12:00:00 GMT");
  assert.equal(parseRetryAfter("86400", now), 86400);
  assert.equal(parseRetryAfter("Fri, 06 Nov 2026 12:00:00 GMT", now), 86400);
  for (const value of ["86401", "9007199254740991", "Sat, 07 Nov 2026 12:00:00 GMT", "0".repeat(129)]) {
    assert.equal(parseRetryAfter(value, now), null);
    const error = normalizeRequestError({ status: 429, headers: { "Retry-After": value } }, { now });
    assert.equal(error.retryAfterSeconds, null);
    assert.equal(error.message, expected.get(429));
  }
});

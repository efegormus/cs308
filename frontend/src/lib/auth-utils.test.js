import test from "node:test";
import assert from "node:assert/strict";
import {
  NETWORK_ERROR, isLoginPath, loginErrorMessage, loginHref, parseAccount, parseRetryAfter,
  registerHref, runHooks, safeReturnUrl, validateEmail, validatePassword
} from "./auth-utils.js";

test("safeReturnUrl keeps same-site paths exactly as given", () => {
  for (const path of ["/", "/products.html", "/products.html#category-3", "/product.html?id=7",
    "/product.html?id=7&from=a%2Fb#top", "/a/b/c", "/@user", "/" + "a".repeat(2047)]) {
    assert.equal(safeReturnUrl(path), path);
  }
});

test("safeReturnUrl rejects other sites and schemes", () => {
  for (const value of ["//evil.com", "///evil.com", "https://evil.com", "http://evil.com/a", "HTTPS://evil.com",
    "javascript:alert(1)", "JaVaScRiPt:alert(1)", "data:text/html,x", "evil.com", "products.html", "./a", "../a",
    " /products.html"]) {
    assert.equal(safeReturnUrl(value), "/", value);
  }
});

test("safeReturnUrl rejects backslashes", () => {
  for (const value of ["/\\evil.com", "\\\\evil.com", "\\/evil.com", "/a\\b", "/a?x=\\"]) {
    assert.equal(safeReturnUrl(value), "/", value);
  }
});

test("safeReturnUrl rejects control characters, which browsers strip from URLs", () => {
  for (const value of ["/\t/evil.com", "/\n/evil.com", "/\r/evil.com", "/a\u0000b", "/a\u007fb", "/a\u0085b", "/a?x=\n"]) {
    assert.equal(safeReturnUrl(value), "/", JSON.stringify(value));
  }
});

test("safeReturnUrl rejects encoded variants in the path", () => {
  for (const value of ["/%2F%2Fevil.com", "/%2f/evil.com", "/%5Cevil.com", "/%5cevil.com", "/%09/evil.com", "/%0A/evil.com",
    "/%00", "/%7F", "/%252F/evil.com", "/%2e%2e//evil.com", "/%2E%2E/%2E%2E/evil"]) {
    assert.equal(safeReturnUrl(value), "/", value);
  }
});

test("safeReturnUrl rejects paths that normalize to another host form", () => {
  for (const value of ["/..//evil.com", "/.//evil.com", "/a/..//evil.com"]) {
    assert.equal(safeReturnUrl(value), "/", value);
  }
});

test("safeReturnUrl rejects a percent-decoded query value once it is decoded", () => {
  // URLSearchParams.get decodes once, so these are what reach safeReturnUrl.
  const decoded = new URLSearchParams("returnUrl=%2F%2Fevil.com").get("returnUrl");
  assert.equal(safeReturnUrl(decoded), "/");
  const doubleEncoded = new URLSearchParams("returnUrl=%252F%252Fevil.com").get("returnUrl");
  assert.equal(safeReturnUrl(doubleEncoded), "/");
});

test("safeReturnUrl handles empty, missing and too long values", () => {
  for (const value of ["", null, undefined, 42, {}, [], "/" + "a".repeat(2048)]) {
    assert.equal(safeReturnUrl(value), "/");
  }
});

test("safeReturnUrl never returns to the login page, which would redirect forever", () => {
  for (const value of ["/login.html", "/LOGIN.HTML", "/login", "/login.html?returnUrl=/", "/login.html#x", "/a/../login.html", "/./login.html"]) {
    assert.equal(safeReturnUrl(value), "/", value);
  }
});

test("isLoginPath matches only the login page", () => {
  assert.equal(isLoginPath("/login.html"), true);
  assert.equal(isLoginPath("/Login.html"), true);
  assert.equal(isLoginPath("/products.html"), false);
  assert.equal(isLoginPath("/"), false);
});

test("loginHref carries the current path, query and hash, URL-encoded", () => {
  assert.equal(loginHref({ pathname: "/" }), "/login?returnUrl=%2F");
  assert.equal(loginHref({ pathname: "/products.html", hash: "#category-3" }),
    "/login?returnUrl=%2Fproducts.html%23category-3");
  assert.equal(loginHref({ pathname: "/product.html", search: "?id=7&a=b" }),
    "/login?returnUrl=%2Fproduct.html%3Fid%3D7%26a%3Db");
});

test("loginHref on the login page keeps its query and never nests itself", () => {
  assert.equal(loginHref({ pathname: "/login" }), "/login");
  assert.equal(loginHref({ pathname: "/login", search: "?returnUrl=%2Fcart.html" }), "/login?returnUrl=%2Fcart.html");
});

test("registerHref passes on only a safe returnUrl", () => {
  assert.equal(registerHref("/products.html#category-3"), "/register?returnUrl=%2Fproducts.html%23category-3");
  assert.equal(registerHref("/"), "/register");
  assert.equal(registerHref(null), "/register");
  assert.equal(registerHref("//evil.com"), "/register");
});

test("validateEmail messages", () => {
  for (const value of ["", "   ", null, undefined]) assert.equal(validateEmail(value), "Enter your email address.");
  for (const value of ["nope", "a@", "@b.com", "a b@c.com", "a@b@c.com", "a@b c.com"]) {
    assert.equal(validateEmail(value), "Enter a valid email address.", value);
  }
  for (const value of ["a@b.com", "  a@b.com  ", "first.last+tag@example.co.uk", "user@localhost"]) {
    assert.equal(validateEmail(value), "", value);
  }
});

test("validatePassword only rejects an empty value and does not reveal the policy", () => {
  assert.equal(validatePassword(""), "Enter your password.");
  assert.equal(validatePassword(null), "Enter your password.");
  assert.equal(validatePassword("x"), "");
  assert.equal(validatePassword("   "), "");
});

test("loginErrorMessage maps each status to its message", () => {
  assert.equal(loginErrorMessage(400), "Something went wrong. Please try again.");
  assert.equal(loginErrorMessage(401), "Invalid email or password.");
  assert.equal(loginErrorMessage(429), "Too many attempts. Please try again in a minute.");
  assert.equal(loginErrorMessage(503), "Service is temporarily unavailable. Please try again.");
  for (const status of [404, 500, 418, undefined]) {
    assert.equal(loginErrorMessage(status), "Something went wrong. Please try again.");
  }
  assert.equal(NETWORK_ERROR, "Could not reach the server. Check your connection and try again.");
});

test("parseRetryAfter uses the number when valid and 60 otherwise", () => {
  assert.equal(parseRetryAfter("30"), 30);
  assert.equal(parseRetryAfter(" 5 "), 5);
  assert.equal(parseRetryAfter(45), 45);
  assert.equal(parseRetryAfter("86400"), 3600);
  for (const value of [null, undefined, "", "0", "-5", "1.5", "abc", "10s", "Wed, 21 Oct 2026 07:28:00 GMT"]) {
    assert.equal(parseRetryAfter(value), 60, String(value));
  }
});

test("parseAccount keeps only the public fields and rejects bad shapes", () => {
  assert.deepEqual(parseAccount({ id: 1, name: "A", email: "a@b.com", role: "Customer", extra: "x" }),
    { id: 1, name: "A", email: "a@b.com", role: "Customer" });
  for (const value of [null, undefined, {}, [], "x", { id: "1", name: "A", email: "a@b.com", role: "Customer" },
    { id: 1, email: "a@b.com", role: "Customer" }]) {
    assert.equal(parseAccount(value), null);
  }
});

test("runHooks passes the account to every hook and waits for them", async () => {
  const seen = [];
  await runHooks([
    account => { seen.push(["sync", account.id]); },
    async account => {
      await new Promise(resolve => setTimeout(resolve, 10));
      seen.push(["async", account.id]);
    }
  ], { id: 7 }, 1000);
  assert.deepEqual(seen.sort(), [["async", 7], ["sync", 7]]);
});

test("runHooks is not blocked by a hook that throws or rejects", async t => {
  t.mock.method(console, "warn", () => {});
  let finished = false;
  await runHooks([
    () => { throw new Error("sync failure"); },
    () => Promise.reject(new Error("async failure")),
    () => { finished = true; }
  ], {}, 1000);
  assert.equal(finished, true);
  assert.equal(console.warn.mock.callCount(), 2);
});

test("runHooks stops waiting for a slow hook at the time limit", async () => {
  const started = Date.now();
  let release;
  const never = new Promise(resolve => { release = resolve; });
  await runHooks([() => never], {}, 30);
  assert.ok(Date.now() - started < 1000);
  release();
});

test("runHooks finishes straight away with no hooks", async () => {
  const started = Date.now();
  await runHooks([], {}, 5000);
  assert.ok(Date.now() - started < 1000);
});

import assert from "node:assert/strict";
import test from "node:test";
import { fetchCustomerProfile, isCustomerProfile, saveCustomerProfile, updateCustomerProfile } from "./customer-profile-api.js";

const profile = { id: 4, name: "Ada Lovelace", email: "ada@example.com", taxId: "TAX-1", homeAddress: "10 Example Street" };

function stubFetch(response) {
  const calls = [];
  globalThis.fetch = async (url, options) => { calls.push({ url, options }); return response; };
  return calls;
}

test("a complete profile is accepted", () => {
  assert.equal(isCustomerProfile(profile), true);
});

test("a profile with a missing or wrongly typed field is rejected", () => {
  assert.equal(isCustomerProfile(null), false);
  assert.equal(isCustomerProfile({ ...profile, id: 0 }), false);
  assert.equal(isCustomerProfile({ ...profile, email: undefined }), false);
  assert.equal(isCustomerProfile({ ...profile, homeAddress: 5 }), false);
});

test("fetchCustomerProfile reads the profile of the signed-in customer", async () => {
  const calls = stubFetch(new Response(JSON.stringify(profile), { status: 200 }));
  assert.deepEqual(await fetchCustomerProfile(), profile);
  assert.equal(calls[0].url, "/api/customer/profile");
});

test("fetchCustomerProfile reports the status of a failed request without the body", async () => {
  stubFetch(new Response("SECRET", { status: 401 }));
  await assert.rejects(fetchCustomerProfile(), error => error.status === 401 && !("message" in error));
});

test("fetchCustomerProfile rejects an invalid response", async () => {
  stubFetch(new Response(JSON.stringify({ id: 1 }), { status: 200 }));
  await assert.rejects(fetchCustomerProfile(), { kind: "invalid-response" });
  stubFetch(new Response("not json", { status: 200 }));
  await assert.rejects(fetchCustomerProfile(), { kind: "invalid-response" });
});

// SCRUM-103: PUT /api/customer/profile.
const edits = { name: "Ada Byron", taxId: "10000000146", homeAddress: "22 New Street" };

test("updateCustomerProfile sends exactly the three fields as JSON with the CSRF token", async () => {
  const calls = stubFetch(new Response(JSON.stringify({ ...profile, ...edits }), { status: 200 }));
  const saved = await updateCustomerProfile({ ...edits, email: "attacker@example.com", id: 99 }, { token: "csrf-1" });
  assert.deepEqual(saved, { ...profile, ...edits });
  assert.equal(calls[0].url, "/api/customer/profile");
  assert.equal(calls[0].options.method, "PUT");
  assert.equal(calls[0].options.headers["Content-Type"], "application/json");
  assert.equal(calls[0].options.headers["X-CSRF-TOKEN"], "csrf-1");
  assert.deepEqual(JSON.parse(calls[0].options.body), edits);
});

test("updateCustomerProfile turns a 400 with field errors into a validation failure without a status", async () => {
  stubFetch(new Response(JSON.stringify({ title: "SECRET", errors: { taxId: ["Use at most 64 characters."] } }), { status: 400 }));
  await assert.rejects(updateCustomerProfile(edits, { token: "t" }), error =>
    error.kind === "validation" && !("status" in error) && error.errors.taxId[0] === "Use at most 64 characters."
    && !JSON.stringify(error).includes("SECRET"));
});

test("updateCustomerProfile reports a 400 without field errors (a refused CSRF token) by status only", async () => {
  stubFetch(new Response(JSON.stringify({ title: "A valid anti-forgery token is required." }), { status: 400 }));
  await assert.rejects(updateCustomerProfile(edits, { token: "t" }), error => error.status === 400 && !("message" in error));
  stubFetch(new Response("SECRET", { status: 429, headers: { "Retry-After": "30" } }));
  await assert.rejects(updateCustomerProfile(edits, { token: "t" }), error =>
    error.status === 429 && error.headers.get("Retry-After") === "30" && !("message" in error));
});

test("updateCustomerProfile rejects an invalid success response", async () => {
  stubFetch(new Response(JSON.stringify({ id: 4, name: "Ada" }), { status: 200 }));
  await assert.rejects(updateCustomerProfile(edits, { token: "t" }), { kind: "invalid-response" });
  stubFetch(new Response("<html>", { status: 200 }));
  await assert.rejects(updateCustomerProfile(edits, { token: "t" }), { kind: "invalid-response" });
});

test("saveCustomerProfile renews a refused CSRF token once and sends again", async () => {
  const tokens = ["old", "new"];
  let refreshes = 0;
  const sent = [];
  globalThis.fetch = async (url, options) => {
    sent.push(options.headers["X-CSRF-TOKEN"]);
    return sent.length === 1
      ? new Response(JSON.stringify({ title: "A valid anti-forgery token is required." }), { status: 400 })
      : new Response(JSON.stringify({ ...profile, ...edits }), { status: 200 });
  };
  const saved = await saveCustomerProfile(edits, { csrfToken: async () => tokens[refreshes], refreshCsrf: async () => { refreshes += 1; } });
  assert.equal(saved.name, "Ada Byron");
  assert.deepEqual(sent, ["old", "new"]);
  assert.equal(refreshes, 1);
});

test("saveCustomerProfile never sends field errors again", async () => {
  let calls = 0;
  globalThis.fetch = async () => {
    calls += 1;
    return new Response(JSON.stringify({ errors: { name: ["This field is required."] } }), { status: 400 });
  };
  await assert.rejects(saveCustomerProfile(edits, { csrfToken: async () => "t", refreshCsrf: async () => {} }), { kind: "validation" });
  assert.equal(calls, 1);
});

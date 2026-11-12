import assert from "node:assert/strict";
import test from "node:test";
import { fetchCustomerProfile, isCustomerProfile } from "./customer-profile-api.js";

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

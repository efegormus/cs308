import test from "node:test";
import assert from "node:assert/strict";
import { createAttemptStore } from "./checkout-attempt.js";
const quote = { cartId: 1, quoteToken: "A".repeat(64) };
const keys = ["00000000-0000-0000-0000-000000000000", "00000000-0000-0000-0000-000000000001"];
function memory() {
  const values = new Map();
  return { values, getItem: key => values.get(key) ?? null, setItem: (key, value) => values.set(key, value), removeItem: key => values.delete(key) };
}

test("submitted attempt survives reload and changed prices without a new key", () => {
  const storage = memory();
  const first = createAttemptStore(7, storage, () => keys[0]);
  first.forQuote(quote); first.submitted();
  const next = createAttemptStore(7, storage, () => keys[1]);
  assert.equal(next.get().key, keys[0]);
  assert.equal(next.forQuote({ ...quote, quoteToken: "B".repeat(64) }).key, keys[0]);
  assert.throws(() => next.forQuote({ ...quote, cartId: 2 }), error => error.status === 409);
});
test("customers cannot inherit another customer's stored attempt and definite failure resets it", () => {
  const storage = memory();
  const first = createAttemptStore(7, storage, () => keys[0]);
  first.forQuote(quote);
  assert.equal(createAttemptStore(8, storage).get(), null);
  first.clear();
  assert.equal(createAttemptStore(7, storage).get(), null);
  assert.equal(createAttemptStore(7, storage, () => keys[1]).forQuote(quote).key, keys[1]);
});
test("blocked storage still retains retries in memory", () => {
  const blocked = { getItem() { throw new Error(); }, setItem() { throw new Error(); }, removeItem() { throw new Error(); } };
  const store = createAttemptStore(1, blocked, () => keys[0]);
  store.forQuote(quote);store.submitted();
  assert.equal(store.forQuote(quote).key, keys[0]);
  store.clear();assert.equal(store.get(), null);
});
test("invalid storage is ignored and only retry metadata is persisted", () => {
  const storage = memory();
  storage.setItem("store.checkout.attempt.7", JSON.stringify({ cartId: 1, key: "invalid", quoteToken: quote.quoteToken }));
  const store = createAttemptStore(7, storage, () => keys[0]);
  assert.equal(store.get(), null);
  store.forQuote({ ...quote, name: "Private customer", payment: { number: "not-to-be-stored" } });
  const saved = JSON.parse(storage.getItem("store.checkout.attempt.7"));
  assert.deepEqual(Object.keys(saved).sort(), ["cartId", "key", "quoteToken", "submitted"]);
});

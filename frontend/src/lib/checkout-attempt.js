import { validAttemptKey, validQuoteToken } from "./purchase-api.js";

// Only non-sensitive retry metadata is saved, isolated by the authenticated customer ID.
export function createAttemptStore(customerId, storage, uuid = () => crypto.randomUUID()) {
  const name = `store.checkout.attempt.${customerId}`;
  let current = null;
  try {
    const value = JSON.parse(storage?.getItem(name));
    if (Number.isInteger(value?.cartId) && value.cartId > 0 && validAttemptKey(value.key)
        && validQuoteToken(value.quoteToken)) current = { cartId: value.cartId, key: value.key, quoteToken: value.quoteToken, submitted: value.submitted === true };
  } catch { /* Storage may be disabled or contain old/corrupt data. */ }
  function save() {
    try { storage?.setItem(name, JSON.stringify(current)); } catch { /* Retain it in memory for this page. */ }
    return current;
  }
  return {
    get: () => current,
    forQuote(quote) {
      if (current?.submitted && current.cartId !== quote.cartId) throw { status: 409 };
      // An uncertain result always keeps its key. The server deduplicates before reading a new quote.
      if (!current || current.cartId !== quote.cartId || (!current.submitted && current.quoteToken !== quote.quoteToken))
        current = { cartId: quote.cartId, quoteToken: quote.quoteToken, key: uuid(), submitted: false };
      else current = { ...current, quoteToken: quote.quoteToken };
      return save();
    },
    submitted() { if (current) { current = { ...current, submitted: true }; save(); } },
    clear() {
      current = null;
      try { storage?.removeItem(name); } catch { /* No persistent storage. */ }
    }
  };
}

export function browserAttemptStore(customerId) {
  let storage;
  try { storage = window.sessionStorage; } catch { /* Browser privacy settings can deny access. */ }
  return createAttemptStore(customerId, storage);
}

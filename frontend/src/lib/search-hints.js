// The rotating hint in the header search (SCRUM-164, DESIGN.md Header > Search). Pure, so Node can test it.
//
// The hint types a term, holds it, deletes it and moves to the next one. Category names come from the categories the
// header already loads (GET /api/categories), so the hint costs no extra request. Product names are a short constant
// list of products from the seeded catalog (SeedData/seed-products.json): asking the API for best sellers on every page
// load would add a request to each page.

/** Products of the seeded catalog, used as product terms. */
export const PRODUCT_HINTS = Object.freeze(["iPhone 16", "Samsung Galaxy S25", "iPad Air", "Google Pixel 9"]);

/** Used when the categories cannot be loaded. */
export const CATEGORY_HINTS = Object.freeze(["Laptops", "Headphones", "Smartwatches"]);

/** Milliseconds: per typed character, the full term on screen, per deleted character. */
export const HINT_TIMING = Object.freeze({ type: 65, hold: 1500, erase: 28 });

const MAX_TERM_LENGTH = 32;

function clean(names) {
  return (Array.isArray(names) ? names : [])
    .filter(name => typeof name === "string")
    .map(name => name.trim())
    .filter(name => name.length > 0 && name.length <= MAX_TERM_LENGTH);
}

/**
 * The terms the hint rotates through: products and categories one after another, without duplicates, at most limit.
 * categoryNames are the real category names; an empty list falls back to CATEGORY_HINTS.
 */
export function searchHintTerms(categoryNames, { products = PRODUCT_HINTS, limit = 6 } = {}) {
  const productTerms = clean(products);
  const real = clean(categoryNames);
  const categoryTerms = real.length > 0 ? real : [...CATEGORY_HINTS];
  const terms = [];
  const seen = new Set();
  for (let index = 0; terms.length < limit && (index < productTerms.length || index < categoryTerms.length); index += 1) {
    for (const term of [productTerms[index], categoryTerms[index]]) {
      if (term === undefined || terms.length >= limit) continue;
      const key = term.toLocaleLowerCase("en");
      if (seen.has(key)) continue;
      seen.add(key);
      terms.push(term);
    }
  }
  return terms;
}

/** The first state: nothing typed of the first term yet. */
export const HINT_START = Object.freeze({ index: 0, length: 0, phase: "typing" });

/**
 * One step of the typewriter. Returns the next state and how long to wait before the step after it.
 * Lengths count characters (code points), so an emoji is never cut in half.
 */
export function hintStep(state, terms, timing = HINT_TIMING) {
  if (!Array.isArray(terms) || terms.length === 0) return { state: HINT_START, delay: null };
  const index = Number.isInteger(state?.index) && state.index >= 0 ? state.index % terms.length : 0;
  const size = Array.from(terms[index]).length;
  const length = Math.min(Math.max(Number.isInteger(state?.length) ? state.length : 0, 0), size);

  if (state?.phase === "holding") return { state: { index, length: size - 1, phase: "deleting" }, delay: timing.erase };
  if (state?.phase === "deleting") {
    if (length > 1) return { state: { index, length: length - 1, phase: "deleting" }, delay: timing.erase };
    return { state: { index: (index + 1) % terms.length, length: 0, phase: "typing" }, delay: timing.type };
  }
  if (length + 1 >= size) return { state: { index, length: size, phase: "holding" }, delay: timing.hold };
  return { state: { index, length: length + 1, phase: "typing" }, delay: timing.type };
}

/** The visible part of the current term. */
export function hintText(state, terms) {
  if (!Array.isArray(terms) || terms.length === 0) return "";
  const term = terms[(state?.index ?? 0) % terms.length];
  return Array.from(term).slice(0, Math.max(state?.length ?? 0, 0)).join("");
}

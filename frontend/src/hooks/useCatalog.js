import { useCallback, useEffect, useRef, useState } from "react";
import { fetchBrands, fetchCategories, fetchMatchingProducts, fetchPopularityOrder } from "../lib/catalog-api.js";

const REQUEST_TIMEOUT_MS = 15000;

// Loads /products (SCRUM-165): the categories (GET /api/categories, shared with the header), the brands and every
// product that matches the search, brand and price (lib/catalog-api.js, fetchMatchingProducts). The category, "In stock
// only" and the per-section sort are applied by the page (lib/catalog-sections.js), so changing them sends no request.
// status is "loading", "ready" or "error". While it reloads, the previous categories and brands stay in place.
export function useCatalog({ brandId, minPrice, maxPrice, search } = {}) {
  const [state, setState] = useState({ status: "loading", categories: [], brands: [], products: [] });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    // The cleanup below also runs when React StrictMode mounts the effect twice in development.
    // An answer to the aborted first request must not replace the state of the second one.
    let current = true;
    const timeout = setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS);
    setState(previous => ({ ...previous, status: "loading" }));
    Promise.all([
      fetchCategories({ signal: controller.signal }),
      fetchMatchingProducts({ brandId, minPrice, maxPrice, search, signal: controller.signal }),
      fetchBrands({ signal: controller.signal })
    ])
      .then(([categories, { items }, brands]) => {
        if (current) setState({ status: "ready", categories, brands, products: items });
      })
      .catch(() => {
        if (current) setState(previous => ({ ...previous, status: "error", categories: [], products: [] }));
      })
      .finally(() => clearTimeout(timeout));
    return () => {
      current = false;
      clearTimeout(timeout);
      controller.abort();
    };
  }, [brandId, minPrice, maxPrice, search, attempt]);

  const retry = useCallback(() => setAttempt(count => count + 1), []);
  return { ...state, retry };
}

// The popularity order for the same filters, asked for only while wanted is true (a section picked "Popularity") and
// kept per filter set, so going back and forth between sorts does not ask again. Returns { status, order }, where order
// is a list of product ids, or null until it arrives. A failure is "error"; the sections then keep the name order.
export function usePopularityOrder(wanted, { brandId, minPrice, maxPrice, search } = {}) {
  const key = JSON.stringify([brandId ?? "", minPrice ?? "", maxPrice ?? "", search ?? ""]);
  const cache = useRef(new Map());
  const [result, setResult] = useState({ key: null, status: "idle", order: null });

  useEffect(() => {
    if (!wanted) return undefined;
    if (cache.current.has(key)) {
      setResult({ key, status: "ready", order: cache.current.get(key) });
      return undefined;
    }
    const controller = new AbortController();
    let current = true;
    const timeout = setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS);
    setResult({ key, status: "loading", order: null });
    fetchPopularityOrder({ brandId, minPrice, maxPrice, search, signal: controller.signal })
      .then(order => {
        cache.current.set(key, order);
        if (current) setResult({ key, status: "ready", order });
      })
      .catch(() => { if (current) setResult({ key, status: "error", order: null }); })
      .finally(() => clearTimeout(timeout));
    return () => {
      current = false;
      clearTimeout(timeout);
      controller.abort();
    };
    // key holds the filters, so they are not listed separately.
  }, [wanted, key]);

  return result.key === key ? result : { status: wanted ? "loading" : "idle", order: null };
}

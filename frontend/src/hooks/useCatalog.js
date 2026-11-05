import { useCallback, useEffect, useState } from "react";
import { fetchCatalog } from "../lib/catalog-api.js";

const REQUEST_TIMEOUT_MS = 15000;

// Loads the catalog for GET /api/categories and /api/products. The filters are the ones fetchProducts accepts, so the
// search bar and sort dropdown (SCRUM-67) only have to pass them in. A new filter value loads the catalog again.
// status is "loading", "ready" or "error". While it reloads, the previous categories stay in place.
export function useCatalog({ categoryId, search, sort, order } = {}) {
  const [state, setState] = useState({ status: "loading", categories: [] });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    // The cleanup below also runs when React StrictMode mounts the effect twice in development.
    // An answer to the aborted first request must not replace the state of the second one.
    let current = true;
    const timeout = setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS);
    setState(previous => ({ status: "loading", categories: previous.categories }));
    fetchCatalog({ categoryId, search, sort, order, signal: controller.signal })
      .then(categories => { if (current) setState({ status: "ready", categories }); })
      .catch(() => { if (current) setState({ status: "error", categories: [] }); })
      .finally(() => clearTimeout(timeout));
    return () => {
      current = false;
      clearTimeout(timeout);
      controller.abort();
    };
  }, [categoryId, search, sort, order, attempt]);

  const retry = useCallback(() => setAttempt(count => count + 1), []);
  return { ...state, retry };
}

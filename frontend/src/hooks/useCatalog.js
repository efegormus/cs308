import { useCallback, useEffect, useState } from "react";
import { fetchBrands, fetchCatalogPage } from "../lib/catalog-api.js";

const REQUEST_TIMEOUT_MS = 15000;

// Loads the catalog for GET /api/categories and /api/products. The filters are the ones fetchProducts accepts, so the
// search bar and sort dropdown (SCRUM-67) only have to pass them in. A new filter value loads the catalog again.
// status is "loading", "ready" or "error". While it reloads, the previous categories stay in place.
export function useCatalog({ categoryId, brandId, minPrice, maxPrice, search, sort, order, page, pageSize } = {}) {
  const [state, setState] = useState({
    status: "loading", categories: [], brands: [], totalCount: 0, page: 1, pageSize: 24, totalPages: 0
  });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    // The cleanup below also runs when React StrictMode mounts the effect twice in development.
    // An answer to the aborted first request must not replace the state of the second one.
    let current = true;
    const timeout = setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS);
    setState(previous => ({ ...previous, status: "loading" }));
    Promise.all([
      fetchCatalogPage({ categoryId, brandId, minPrice, maxPrice, search, sort, order, page, pageSize, signal: controller.signal }),
      fetchBrands({ signal: controller.signal })
    ])
      .then(([catalog, brands]) => { if (current) setState({ status: "ready", brands, ...catalog }); })
      .catch(() => {
        if (current) setState(previous => ({ ...previous, status: "error", categories: [], totalCount: 0, totalPages: 0 }));
      })
      .finally(() => clearTimeout(timeout));
    return () => {
      current = false;
      clearTimeout(timeout);
      controller.abort();
    };
  }, [categoryId, brandId, minPrice, maxPrice, search, sort, order, page, pageSize, attempt]);

  const retry = useCallback(() => setAttempt(count => count + 1), []);
  return { ...state, retry };
}

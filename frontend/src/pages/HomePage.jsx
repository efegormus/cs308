import { useCallback, useEffect, useState } from "react";
import CategoryCoverflow from "../components/CategoryCoverflow.jsx";
import { ErrorState, LoadingState } from "../components/RequestStates.js";
import ViewCursor from "../components/ViewCursor.jsx";
import { fetchCategories, fetchProducts, isValidProduct } from "../lib/catalog-api.js";
import { DEFAULT_SORT, sortQuery } from "../lib/coverflow.js";
import { useRequest } from "../lib/useRequest.js";
import "../styles/request-states.css";

function isValidCategory(category) {
  return Number.isInteger(category?.id) && typeof category.name === "string";
}

// One category and its own request. A new sort cancels the previous request, and the cards stay where they are until
// the new order arrives, so they can slide to it. A failure in one row leaves the other rows alone.
function CategoryRow({ category, rowIndex }) {
  const [sort, setSort] = useState(DEFAULT_SORT);
  const [products, setProducts] = useState(null);
  const { status, error, run } = useRequest();

  const load = useCallback(async () => {
    const result = await run(
      ({ signal }) => fetchProducts({ signal, categoryId: category.id, ...sortQuery(sort) }),
      { validate: rows => rows.every(isValidProduct) }
    );
    if (result.status === "success") setProducts(result.data);
    else if (result.status === "error") setProducts(null);
  }, [run, category.id, sort]);

  useEffect(() => { load(); }, [load]);

  return (
    <CategoryCoverflow category={category} products={products} sort={sort} onSortChange={setSort}
      loading={status === "idle" || status === "loading"} error={status === "error" ? error : null}
      onRetry={load} rowIndex={rowIndex} />
  );
}

export default function HomePage() {
  const { status, data: categories, error, run } = useRequest();

  const load = useCallback(() => run(
    ({ signal }) => fetchCategories({ signal }),
    { validate: rows => rows.every(isValidCategory) }
  ), [run]);

  useEffect(() => { load(); }, [load]);

  return (
    <div className="route-page home-page">
      <title>Online Store</title>
      <main id="main-content">
        <div className="intro">
          <h1 className="r"><span className="ri" style={{ "--i": 0 }}>Shop by category</span></h1>
          <p className="r"><span className="ri" style={{ "--i": 1 }}>Find the best phones and tablets at great prices.</span></p>
        </div>
        {(status === "idle" || status === "loading") && <LoadingState label="Loading categories…" />}
        <ErrorState error={error} onRetry={load} />
        {status === "success" && categories.length === 0 && <p className="cf-empty">No products available yet. Please check back soon.</p>}
        {status === "success" && categories.length > 0 && (
          <div className="rows">
            {categories.map((category, index) => (
              <CategoryRow key={category.id} category={category} rowIndex={index} />
            ))}
          </div>
        )}
      </main>
      <ViewCursor />
    </div>
  );
}

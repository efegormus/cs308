import { useEffect, useRef } from "react";
import { useLocation } from "react-router";
import CategoryNav from "../components/CategoryNav.jsx";
import CategorySection from "../components/CategorySection.jsx";
import { useCatalog } from "../hooks/useCatalog.js";
import { catalogSummary, categoryAnchorId, categoryIdFromHash } from "../lib/category-display.js";

// The product list, grouped by category (SCRUM-109). The home page links here as /products#category-<id>.
export default function ProductsPage() {
  // SCRUM-67: keep the search text and the sort choice in this component and pass them to useCatalog
  // as { search, sort, order }. The catalog reloads when they change.
  const { status, categories, retry } = useCatalog();
  const { hash } = useLocation();
  const scrolledTo = useRef(null);

  // A page opened with a saved category link scrolls to that category once its section exists.
  // After that, loading again (for a new search, say) must not pull the page back.
  useEffect(() => {
    if (status !== "ready" || scrolledTo.current === hash) return;
    scrolledTo.current = hash;
    const categoryId = categoryIdFromHash(hash);
    if (categoryId !== null) document.getElementById(categoryAnchorId(categoryId))?.scrollIntoView();
  }, [status, hash]);

  let message = "Loading products…";
  if (status === "ready") message = catalogSummary(categories);
  if (status === "error") message = "We couldn't load the products. Please try again.";

  return (
    <div className="route-page catalog-page">
      <title>Products by category | Online Store</title>
      <main id="main-content">
        <h1>Shop by category</h1>
        <p className="intro">Explore our phones and tablets. Prices are in Turkish lira.</p>
        {/* SCRUM-67 puts the search bar and the sort dropdown here. */}
        <CategoryNav categories={categories} />
        <p role="status" aria-live="polite">{message}</p>
        {status === "error" && <button type="button" onClick={retry}>Try again</button>}
        <div aria-busy={status === "loading"}>
          {categories.map(category => <CategorySection key={category.id} category={category} />)}
        </div>
      </main>
    </div>
  );
}

import { useEffect, useRef } from "react";
import { useLocation, useSearchParams } from "react-router";
import CategoryNav from "../components/CategoryNav.jsx";
import CategorySection from "../components/CategorySection.jsx";
import { useCatalog } from "../hooks/useCatalog.js";
import { catalogSummary, categoryAnchorId, categoryIdFromHash, visibleCategories } from "../lib/category-display.js";

// The product list, grouped by category (SCRUM-109). The home page links here as /products#category-<id>.
export default function ProductsPage() {
  // The header's search form opens /products?search=<text> (SCRUM-123); the same name as the API parameter.
  // SCRUM-67/126: add the sort choice the same way and pass { search, sort, order } to useCatalog.
  const [searchParams] = useSearchParams();
  const search = searchParams.get("search") ?? "";
  const { status, categories, retry } = useCatalog({ search });
  const displayedCategories = visibleCategories(categories, search);
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
  if (status === "ready") message = catalogSummary(displayedCategories, search);
  if (status === "error") message = "We couldn't load the products. Please try again.";

  return (
    <div className="route-page catalog-page">
      <title>Products by category | Online Store</title>
      <main id="main-content">
        <h1>Shop by category</h1>
        <p className="intro">Explore our phones and tablets. Prices are in Turkish lira.</p>
        {/* SCRUM-67 puts the search bar and the sort dropdown here. */}
        <CategoryNav categories={displayedCategories} />
        <p id="catalog-status" role="status" aria-live="polite">{message}</p>
        {status === "error" && <button type="button" onClick={retry}>Try again</button>}
        <div id="catalog" aria-busy={status === "loading"}>
          {displayedCategories.map(category => <CategorySection key={category.id} category={category} />)}
        </div>
      </main>
    </div>
  );
}

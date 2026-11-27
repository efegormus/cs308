import { useEffect, useRef } from "react";
import { useLocation, useSearchParams } from "react-router";
import CategoryNav from "../components/CategoryNav.jsx";
import CategorySection from "../components/CategorySection.jsx";
import ProductCard from "../components/ProductCard.jsx";
import Button from "../components/ui/Button.jsx";
import { useCatalog } from "../hooks/useCatalog.js";
import { catalogSummary, categoryAnchorId, categoryIdFromHash, visibleCategories } from "../lib/category-display.js";
import {
  catalogFiltersFromSearchParams, clearCatalogSearchParams, hasCatalogFilters, sortDescription, updateCatalogSearchParams
} from "../lib/catalog-query.js";

// The product list, grouped by category (SCRUM-109). Category links filter all pages through ?categoryId=<id>; saved hash links are supported.
export default function ProductsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const { search, categoryId, brandId, minPrice, maxPrice, sort, order, page } =
    catalogFiltersFromSearchParams(searchParams);
  const { hash } = useLocation();
  const legacyCategory = categoryIdFromHash(hash);
  const activeCategoryId = categoryId || (legacyCategory === null ? "" : String(legacyCategory));
  const { status, categories, brands, items = [], totalCount, totalPages, retry } = useCatalog({
    categoryId: activeCategoryId || undefined,
    brandId: brandId || undefined,
    minPrice: minPrice || undefined,
    maxPrice: maxPrice || undefined,
    search,
    sort: sort || undefined,
    order: order || undefined,
    page,
    pageSize: 24
  });
  const hasFilters = hasCatalogFilters({ search, categoryId: activeCategoryId, brandId, minPrice, maxPrice });
  // These groups contain one page, never the whole category's inventory.
  const displayedCategories = visibleCategories(categories, "current page");
  const scrolledTo = useRef(null);

  function updateQuery(name, value, resetPage = true) {
    setSearchParams(previous => updateCatalogSearchParams(previous, name, value, resetPage), { replace: true });
  }

  function clearFilters() {
    setSearchParams(previous => clearCatalogSearchParams(previous), { replace: true });
  }

  // A page opened with a saved category link scrolls to that category once its section exists.
  // After that, loading again (for a new search, say) must not pull the page back.
  useEffect(() => {
    if (status !== "ready" || scrolledTo.current === hash) return;
    scrolledTo.current = hash;
    const categoryId = categoryIdFromHash(hash);
    if (categoryId !== null) document.getElementById(categoryAnchorId(categoryId))?.scrollIntoView();
  }, [status, hash]);

  let message = "Loading products…";
  if (status === "ready") {
    message = totalCount === 0 && hasFilters ? "No products match these filters."
      : totalCount === 0 ? catalogSummary(displayedCategories)
        : `${totalCount} ${totalCount === 1 ? "product" : "products"} found.`;
  }
  if (status === "error") message = "We couldn't load the products. Please try again.";

  return (
    <div className="route-page catalog-page">
      <title>Products by category | Online Store</title>
      <main id="main-content">
        <h1>Shop by category</h1>
        <p className="intro">Explore electronics across all departments. Prices are in Turkish lira.</p>
        <form className="product-filters" role="search" onSubmit={event => event.preventDefault()}>
          <label>Search products
            <input type="search" value={search} onChange={event => updateQuery("search", event.target.value)} />
          </label>
          <label>Category
            <select value={activeCategoryId} onChange={event => updateQuery("categoryId", event.target.value)}>
              <option value="">All categories</option>
              {categories.map(category => <option key={category.id} value={category.id}>{category.name}</option>)}
            </select>
          </label>
          <label>Brand
            <select value={brandId} onChange={event => updateQuery("brandId", event.target.value)}>
              <option value="">All brands</option>
              {brands.map(brand => <option key={brand.id} value={brand.id}>{brand.name}</option>)}
            </select>
          </label>
          <label>Minimum price
            <input type="number" min="0" step="0.01" value={minPrice}
              onChange={event => updateQuery("minPrice", event.target.value)} />
          </label>
          <label>Maximum price
            <input type="number" min="0" step="0.01" value={maxPrice}
              onChange={event => updateQuery("maxPrice", event.target.value)} />
          </label>
          <label>Sort by
            <select value={sort} onChange={event => updateQuery("sort", event.target.value)}>
              <option value="">Name</option>
              <option value="price">Price</option>
              <option value="popularity">Popularity (units sold)</option>
            </select>
          </label>
          <label>Order
            <select value={order} onChange={event => updateQuery("order", event.target.value)}>
              <option value="">Default</option>
              <option value="asc">Ascending</option>
              <option value="desc">Descending</option>
            </select>
          </label>
        </form>
        <CategoryNav categories={categories} />
        <p id="catalog-status" role="status" aria-live="polite">{message}</p>
        {status === "ready" && totalCount > 0 && <p className="sort-note">{sortDescription(sort, order)}</p>}
        {status === "ready" && hasFilters && (
          <Button variant="secondary" className="clear-filters" onClick={clearFilters}>Clear filters</Button>
        )}
        {status === "ready" && totalCount === 0 && hasFilters && (
          <p className="empty-hint">Try a different word, a wider price range or another category, or clear the filters to see everything.</p>
        )}
        {status === "error" && <Button variant="secondary" onClick={retry}>Try again</Button>}
        {status === "ready" && totalCount > 0 && <p>Showing {items.length} products on this page.</p>}
        <div id="catalog" aria-busy={status === "loading"}>
          {sort ? items.length > 0 && <section className="category-section" aria-label="Sorted products">
            <h2>Products</h2><ul className="product-grid">{items.map(product => <ProductCard key={product.id} product={product} />)}</ul>
          </section> : displayedCategories.map(category => <CategorySection key={category.id} category={category} />)}
        </div>
        {status === "ready" && totalPages > 1 && (
          <nav className="catalog-pagination" aria-label="Product pages">
            <Button variant="secondary" disabled={Number(page) <= 1} onClick={() => updateQuery("page", String(Number(page) - 1), false)}>
              Previous
            </Button>
            <span>Page {page} of {totalPages}</span>
            <Button variant="secondary" disabled={Number(page) >= totalPages} onClick={() => updateQuery("page", String(Number(page) + 1), false)}>
              Next
            </Button>
          </nav>
        )}
      </main>
    </div>
  );
}

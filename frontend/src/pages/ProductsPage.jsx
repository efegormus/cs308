import { useEffect, useMemo, useState } from "react";
import { Link, useLocation, useNavigate, useSearchParams } from "react-router";
import CatalogSidebar from "../components/CatalogSidebar.jsx";
import CategorySection from "../components/CategorySection.jsx";
import Button from "../components/ui/Button.jsx";
import { useCatalog, usePopularityOrder } from "../hooks/useCatalog.js";
import {
  activeFilterChips, buildSections, catalogHeading, catalogTree, categoryCounts, filterInStock, isSectionSort,
  rankById, sectionsStatus, sidebarCategories, sortSection
} from "../lib/catalog-sections.js";
import {
  applyPriceRange, catalogFiltersFromSearchParams, categoryProductsPath, isInStockOnly, queryForCategoryHash,
  removeCatalogParams, updateCatalogSearchParams
} from "../lib/catalog-query.js";
import { formatPrice } from "../lib/product-display.js";

const withQuery = params => {
  const query = params.toString();
  return query ? `/products?${query}` : "/products";
};

// The filters "Clear filters" removes. The search stays: the header's × clears it.
const SIDEBAR_FILTERS = ["categoryId", "brandId", "minPrice", "maxPrice", "inStock", "sort", "order"];

// All products (SCRUM-165, DESIGN.md Products page): the filter sidebar on the left, the category sections on the
// right. The URL holds the filters (?categoryId, brandId, minPrice, maxPrice, inStock, search); each section's sort and
// "View all" are page state. Changing the category or "In stock only" sends no request: the page loads every product
// matching the search, brand and price once and builds the sections itself (lib/catalog-sections.js).
export default function ProductsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const location = useLocation();
  const navigate = useNavigate();
  const { search, categoryId, brandId, minPrice, maxPrice, inStock } = catalogFiltersFromSearchParams(searchParams);
  const inStockOnly = isInStockOnly(inStock);
  const { status, categories, brands, products, retry } = useCatalog({
    brandId: brandId || undefined, minPrice: minPrice || undefined, maxPrice: maxPrice || undefined, search
  });
  const [sorts, setSorts] = useState({});
  const [expanded, setExpanded] = useState({});

  // An old /products#category-<id> link chooses that category, the same as ?categoryId=<id>.
  useEffect(() => {
    const query = queryForCategoryHash(location.search, location.hash);
    if (query !== null) navigate({ pathname: location.pathname, search: query ? `?${query}` : "" }, { replace: true });
  }, [location.pathname, location.search, location.hash, navigate]);

  const tree = useMemo(() => catalogTree(categories), [categories]);
  const shown = useMemo(() => filterInStock(products, inStockOnly), [products, inStockOnly]);
  const counts = useMemo(() => categoryCounts(tree, shown), [tree, shown]);
  const sections = useMemo(() => buildSections(tree, shown, { categoryId }), [tree, shown, categoryId]);
  const searching = search.trim() !== "";
  const rows = sidebarCategories(tree, counts, { categoryId, searching });
  const heading = catalogHeading(tree, { categoryId, search, resultCount: sections.reduce((n, s) => n + s.products.length, 0) });
  const chips = activeFilterChips(tree, { categoryId, brandId, brands, minPrice, maxPrice, inStock: inStockOnly }, formatPrice);
  const hasFilters = chips.length > 0 || Boolean(brandId);
  // Only a category is chosen: an empty result means the category has no products, not that the filters are too narrow.
  const categoryOnly = !searching && chips.length > 0 && chips.every(chip => chip.key === "category") && !brandId;

  const sortOf = id => (isSectionSort(sorts[id]) ? sorts[id] : "name");
  const wantsPopularity = sections.some(section => sortOf(section.id) === "popularity");
  const popularity = usePopularityOrder(wantsPopularity, {
    brandId: brandId || undefined, minPrice: minPrice || undefined, maxPrice: maxPrice || undefined, search
  });
  const popularityRank = useMemo(() => (popularity.order ? rankById(popularity.order) : null), [popularity.order]);

  function setParams(update) {
    setSearchParams(previous => update(previous), { replace: true });
  }

  function applyPrice(min, max) {
    const result = applyPriceRange(searchParams, min, max);
    if (result.error) return result.error;
    setParams(() => result.params);
    return null;
  }

  let message = "Loading products…";
  if (status === "error") message = "We couldn't load the products. Please try again.";
  else if (status === "ready") {
    message = sections.length > 0 ? sectionsStatus(sections)
      : products.length === 0 && !searching && !hasFilters ? "No products available yet. Please check back soon."
        : categoryOnly ? "No products available in this category yet."
          : "No products match these filters.";
  }
  const empty = status === "ready" && sections.length === 0 && (searching || hasFilters) && !categoryOnly;

  return (
    <div className="route-page catalog-page">
      <title>{`${heading.title} | Siesta`}</title>
      <main id="main-content" className="catalog-main">
        <nav className="catalog-breadcrumb" aria-label="Breadcrumb">
          <ol>
            {heading.crumbs.map(crumb => (
              <li key={`${crumb.label}-${crumb.to}`}>
                {crumb.to ? <Link to={crumb.to}>{crumb.label}</Link> : <span aria-current="page">{crumb.label}</span>}
              </li>
            ))}
          </ol>
        </nav>
        <h1>{heading.title}</h1>
        <p id="catalog-status" className="catalog-status" role="status" aria-live="polite">{message}</p>
        {chips.length > 0 && (
          <ul className="catalog-chips" aria-label="Active filters">
            {chips.map(chip => (
              <li key={chip.key}>
                <button type="button" className="catalog-chip" aria-label={`Remove filter: ${chip.label}`}
                  onClick={() => setParams(previous => removeCatalogParams(previous, chip.remove))}>
                  <span>{chip.label}</span>
                  <svg viewBox="0 0 12 12" width="12" height="12" aria-hidden="true" focusable="false">
                    <path d="M3 3l6 6M9 3l-6 6" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
                  </svg>
                </button>
              </li>
            ))}
          </ul>
        )}

        <div className="catalog-layout">
          <CatalogSidebar rows={rows} allCount={shown.length}
            allHref={withQuery(removeCatalogParams(searchParams, ["categoryId"]))}
            categoryLinkFor={id => categoryProductsPath(id, searchParams)} selectedCategory={rows.some(row => row.selected)}
            brands={brands} brandId={brandId} onBrandChange={value => setParams(previous => updateCatalogSearchParams(previous, "brandId", value))}
            minPrice={minPrice} maxPrice={maxPrice} onApplyPrice={applyPrice}
            inStock={inStockOnly} onInStockChange={checked => setParams(previous => updateCatalogSearchParams(previous, "inStock", checked ? "1" : ""))}
            hasFilters={hasFilters} onClear={() => setParams(previous => removeCatalogParams(previous, SIDEBAR_FILTERS))} />

          <div id="catalog" className="catalog-results" aria-busy={status === "loading"}>
            {status === "error" && <Button variant="secondary" onClick={retry}>Try again</Button>}
            {empty && (
              <p className="empty-hint">Try a different word, a wider price range or another category, or clear the filters to see everything.</p>
            )}
            {status !== "error" && sections.map(section => {
              const sort = sortOf(section.id);
              return (
                <CategorySection key={section.id} section={section}
                  products={sortSection(section.products, sort, sort === "popularity" ? popularityRank : null)}
                  sort={sort} sortPending={sort === "popularity" && popularity.status === "loading"}
                  onSortChange={value => setSorts(previous => ({ ...previous, [section.id]: value }))}
                  expanded={Boolean(expanded[section.id])}
                  onToggle={() => setExpanded(previous => ({ ...previous, [section.id]: !previous[section.id] }))} />
              );
            })}
          </div>
        </div>
      </main>
    </div>
  );
}

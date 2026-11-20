import { Link, useLocation } from "react-router";
import { updateCatalogSearchParams } from "../lib/catalog-query.js";

// Links that jump to the category sections. Nothing is shown when there are no categories.
export default function CategoryNav({ categories }) {
  const location = useLocation();
  if (categories.length === 0) return null;
  return (
    <nav id="category-nav" className="category-nav" aria-label="Product categories">
      {categories.map(category => (
        <Link key={category.id} to={`/products?${updateCatalogSearchParams(new URLSearchParams(location.search), "categoryId", String(category.id))}`}>{category.name}</Link>
      ))}
    </nav>
  );
}

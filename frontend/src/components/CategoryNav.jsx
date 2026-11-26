import { Link, useLocation } from "react-router";
import { categoryProductsPath } from "../lib/catalog-query.js";

// Links that fetch the selected category across every page. Nothing is shown when there are no categories.
export default function CategoryNav({ categories }) {
  const location = useLocation();
  if (categories.length === 0) return null;
  return (
    <nav id="category-nav" className="category-nav" aria-label="Product categories">
      {categories.map(category => (
        <Link key={category.id} to={categoryProductsPath(category.id, location.search)}>{category.name}</Link>
      ))}
    </nav>
  );
}

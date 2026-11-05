import { categoryAnchorId } from "../lib/category-display.js";

// Links that jump to the category sections. Nothing is shown when there are no categories.
export default function CategoryNav({ categories }) {
  if (categories.length === 0) return null;
  return (
    <nav className="category-nav" aria-label="Product categories">
      {categories.map(category => (
        <a key={category.id} href={`#${categoryAnchorId(category.id)}`}>{category.name}</a>
      ))}
    </nav>
  );
}

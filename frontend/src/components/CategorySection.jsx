import { categoryAnchorId, productCountLabel } from "../lib/category-display.js";
import ProductCard from "./ProductCard.jsx";

// One category with its products. The section id is what /products#category-<id> links point at.
export default function CategorySection({ category }) {
  const id = categoryAnchorId(category.id);
  const count = category.products.length;

  return (
    <section className="category-section" id={id} aria-labelledby={`${id}-heading`}>
      <h2 id={`${id}-heading`}>
        {category.name}
        <span className="product-count"> · {productCountLabel(count)}</span>
      </h2>
      {category.description && <p className="category-description">{category.description}</p>}
      {count === 0 ? (
        <p>No products available in this category yet.</p>
      ) : (
        <ul className="product-grid">
          {category.products.map(product => <ProductCard key={product.id} product={product} />)}
        </ul>
      )}
    </section>
  );
}

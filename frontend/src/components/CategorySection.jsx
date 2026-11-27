import { categoryAnchorId, productCountLabel } from "../lib/category-display.js";
import { SECTION_SORTS, sectionToggleLabel, visibleProducts } from "../lib/catalog-sections.js";
import ProductCard from "./ProductCard.jsx";
import Button from "./ui/Button.jsx";
import Input from "./ui/Input.jsx";

// One category section of /products (SCRUM-165, DESIGN.md Products page). The header holds the name, "· N products",
// the section's own sort and, for more than three products, "View all N" / "Show less". products arrive already sorted
// (lib/catalog-sections.js, sortSection). The section id stays category-<id>, which tests and old links know.
export default function CategorySection({ section, products, sort, onSortChange, expanded, onToggle, sortPending = false }) {
  const id = categoryAnchorId(section.id);
  const count = products.length;
  const toggle = sectionToggleLabel(count, expanded);
  const listId = `${id}-products`;

  return (
    <section className="category-section" id={id} aria-labelledby={`${id}-heading`} aria-busy={sortPending}>
      <div className="category-section-header">
        <h2 id={`${id}-heading`}>
          {section.name}
          <span className="product-count"> · {productCountLabel(count)}</span>
        </h2>
        <div className="category-section-controls">
          <Input select className="category-section-sort" label={`Sort ${section.name}`} hideLabel value={sort}
            onChange={event => onSortChange(event.target.value)}>
            {SECTION_SORTS.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}
          </Input>
          {toggle && (
            <Button variant="secondary" className="category-section-toggle" aria-expanded={expanded} aria-controls={listId}
              onClick={onToggle}>{toggle}</Button>
          )}
        </div>
      </div>
      <ul className="product-grid" id={listId}>
        {visibleProducts(products, expanded).map(product => <ProductCard key={product.id} product={product} />)}
      </ul>
    </section>
  );
}

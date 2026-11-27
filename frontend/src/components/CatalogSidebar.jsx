import { useEffect, useId, useState } from "react";
import { Link } from "react-router";
import Button from "./ui/Button.jsx";
import Input from "./ui/Input.jsx";

// The filter sidebar of /products (SCRUM-165, DESIGN.md Products page), 264px beside the results. Top to bottom:
// - Category: "All products" and the categories with their match counts (lib/catalog-sections.js, sidebarCategories);
//   subcategories are indented, and only under the chosen category's top-level category. Links keep the other filters
//   and use ?categoryId=<id>; the chosen one has aria-current="page".
// - Brand: a select of GET /api/brands.
// - Price: Min and Max, applied by the Apply button or Enter (one form), never while typing.
// - "In stock only".
// - "Clear filters", shown while any filter is on.
// Every link, field and button is at least 44px high.
export default function CatalogSidebar({
  rows, allCount, allHref, categoryLinkFor, selectedCategory, brands, brandId, onBrandChange,
  minPrice, maxPrice, onApplyPrice, inStock, onInStockChange, hasFilters, onClear
}) {
  const [min, setMin] = useState(minPrice);
  const [max, setMax] = useState(maxPrice);
  const [priceError, setPriceError] = useState(null);
  const inStockId = useId();
  // The box changes at once on a click; the URL (and so inStock) follows a moment later.
  const [checked, setChecked] = useState(inStock);
  useEffect(() => setChecked(inStock), [inStock]);

  // The fields follow the URL (a removed chip, "Clear filters", the back button).
  useEffect(() => {
    setMin(minPrice);
    setMax(maxPrice);
    setPriceError(null);
  }, [minPrice, maxPrice]);

  function applyPrice(event) {
    event.preventDefault();
    setPriceError(onApplyPrice(min, max));
  }

  return (
    <aside className="catalog-sidebar" aria-label="Filters">
      <nav className="catalog-sidebar-group" aria-labelledby="catalog-category-heading">
        <h2 id="catalog-category-heading" className="catalog-sidebar-heading">Category</h2>
        <ul className="catalog-category-list">
          <li>
            <Link to={allHref} className="catalog-category-link" aria-current={selectedCategory ? undefined : "page"}>
              <span>All products</span><span className="catalog-category-count">{allCount}</span>
            </Link>
          </li>
          {rows.map(row => (
            <li key={row.id} style={{ "--depth": row.depth }}>
              <Link to={categoryLinkFor(row.id)} className={row.depth > 0 ? "catalog-category-link is-sub" : "catalog-category-link"}
                aria-current={row.selected ? "page" : undefined}>
                <span>{row.name}</span><span className="catalog-category-count">{row.count}</span>
              </Link>
            </li>
          ))}
        </ul>
      </nav>

      <div className="catalog-sidebar-group">
        <Input select label="Brand" id="catalog-brand" value={brandId} onChange={event => onBrandChange(event.target.value)}>
          <option value="">All brands</option>
          {brands.map(brand => <option key={brand.id} value={brand.id}>{brand.name}</option>)}
        </Input>
      </div>

      <form className="catalog-sidebar-group catalog-price" onSubmit={applyPrice} noValidate>
        <fieldset>
          <legend className="catalog-sidebar-heading">Price</legend>
          <div className="catalog-price-fields">
            <Input label="Min" id="catalog-min-price" type="number" inputMode="decimal" min="0" step="0.01" value={min}
              onChange={event => setMin(event.target.value)} aria-describedby={priceError ? "catalog-price-error" : undefined} />
            <Input label="Max" id="catalog-max-price" type="number" inputMode="decimal" min="0" step="0.01" value={max}
              onChange={event => setMax(event.target.value)} aria-describedby={priceError ? "catalog-price-error" : undefined} />
          </div>
          {priceError && <p id="catalog-price-error" className="ui-field-error catalog-price-error" role="alert">{priceError}</p>}
          <Button type="submit" variant="secondary" className="catalog-price-apply">Apply</Button>
        </fieldset>
      </form>

      <div className="catalog-sidebar-group">
        {/* The real check box covers the whole 44px row (transparent); the box beside the text is drawn for it. */}
        <label className="catalog-check" htmlFor={inStockId}>
          <input id={inStockId} type="checkbox" checked={checked}
            onChange={event => { setChecked(event.target.checked); onInStockChange(event.target.checked); }} />
          <span className="catalog-check-box" aria-hidden="true" />
          <span>In stock only</span>
        </label>
      </div>

      {hasFilters && <Button variant="secondary" className="clear-filters" onClick={onClear}>Clear filters</Button>}
    </aside>
  );
}

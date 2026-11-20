import { formatPrice } from "../lib/product-display.js";
import { hasDiscount } from "../lib/pricing.js";
import "../styles/pricing.css";

export default function Price({ price, listPrice }) {
  return <span className="price-quote">
    {hasDiscount(price, listPrice) && <del className="price-original" aria-label="Original price">{formatPrice(listPrice)}</del>}
    <span className="price-current" aria-label={hasDiscount(price, listPrice) ? "Sale price" : undefined}>{formatPrice(price)}</span>
  </span>;
}

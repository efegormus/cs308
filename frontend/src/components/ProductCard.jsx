import { useState } from "react";
import { Link } from "react-router";
import { formatPrice, safeImageUrl, stockLabel } from "../lib/product-display.js";

// One product in a category grid. The list item is part of the card, so it can sit directly in a <ul>.
// Out-of-stock products are shown too (course requirement 7); the label says so.
export default function ProductCard({ product }) {
  const [imageFailed, setImageFailed] = useState(false);
  const imageUrl = safeImageUrl(product.imageUrl);
  const stock = stockLabel(product.stockQuantity);

  return (
    <li>
      <article className="product-card">
        {imageUrl && !imageFailed ? (
          <img className="product-image" src={imageUrl} alt={product.name} loading="lazy"
            onError={() => setImageFailed(true)} />
        ) : (
          <div className="image-placeholder">No image available</div>
        )}
        <h3><Link to={`/product?id=${product.id}`}>{product.name}</Link></h3>
        {product.brandName && <p className="product-brand">{product.brandName}</p>}
        {product.description && <p className="product-description">{product.description}</p>}
        <p className="price">{formatPrice(product.price)}</p>
        <p className={stock.inStock ? "stock" : "stock out-of-stock"}>{stock.text}</p>
      </article>
    </li>
  );
}

import { useState } from "react";
import { Link } from "react-router";
import { formatPrice, safeImageUrl, stockLabel } from "../lib/product-display.js";
import { specRows } from "../lib/product-detail.js";
import AddToCart from "./AddToCart.jsx";
import WishlistButton from "./WishlistButton.jsx";

// One product with its stock count (SCRUM-45 / SCRUM-110). Give it key={product.id}, so a different product
// starts without the "image failed" state of the previous one. Everything is rendered as text, never as HTML.
export default function ProductDetail({ product }) {
  const [imageFailed, setImageFailed] = useState(false);
  const imageUrl = safeImageUrl(product.imageUrl);
  const gallery = Array.isArray(product.images)
    ? product.images.map(image => ({ ...image, safeUrl: safeImageUrl(image.url) })).filter(image => image.safeUrl)
    : [];
  const stock = stockLabel(product.stockQuantity);

  return (
    <article className="product-detail">
      <div className="product-gallery">
        {gallery.length > 0 ? gallery.map((image, index) => (
          <img key={`${image.url}-${index}`} className="product-image" src={image.safeUrl}
            alt={image.altText || product.name} loading={index === 0 ? "eager" : "lazy"} />
        )) : imageUrl && !imageFailed ? (
          <img className="product-image" src={imageUrl} alt={product.name} onError={() => setImageFailed(true)} />
        ) : <div className="image-placeholder">No image available</div>}
      </div>
      <div className="product-info">
        <p className="product-category">
          <Link to={`/products#category-${product.categoryId}`}>{product.categoryName}</Link>
        </p>
        <h1>{product.name}</h1>
        {product.brandName && <p className="product-brand">{product.brandName}</p>}
        {product.description && <p className="product-description">{product.description}</p>}
        <p className="price">{formatPrice(product.price)}</p>
        <p className={stock.inStock ? "stock" : "stock out-of-stock"}>{stock.text}</p>
        <AddToCart product={product} />
        <WishlistButton productId={product.id} />
        <dl className="product-specs">
          {specRows(product).map(row => (
            <div key={row.label} style={{ display: "contents" }}>
              <dt>{row.label}</dt>
              <dd>{row.value}</dd>
            </div>
          ))}
        </dl>
      </div>
    </article>
  );
}

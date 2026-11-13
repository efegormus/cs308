import { useId, useState } from "react";
import { Link } from "react-router";
import { useAuth } from "../auth/AuthProvider.jsx";
import { addCartItem } from "../lib/cart-api.js";
import { addToCartError, withCsrfRetry } from "../lib/add-to-cart.js";
import { formatPrice, safeImageUrl, specRow, stockStatus } from "../lib/product-display.js";
import { canUseCart } from "../lib/role-routes.js";
import Button from "./ui/Button.jsx";
import Card from "./ui/Card.jsx";

// One product in a category grid (DESIGN.md, Product card on /products). The list item is part of the card, so it
// can sit directly in a <ul>. Top to bottom: 1:1 image, name, spec row, price, stock status, then the actions.
// Out-of-stock products are shown and found by search too (requirement 7), but their button is disabled; the server
// checks the stock again. The card shows the stock status only; the product page shows the count (requirement 3).
export default function ProductCard({ product }) {
  const { account, csrfToken, refreshCsrf } = useAuth();
  const nameId = useId();
  const [imageFailed, setImageFailed] = useState(false);
  const [busy, setBusy] = useState(false);
  const [added, setAdded] = useState(false);
  const [error, setError] = useState(null);
  const imageUrl = safeImageUrl(product.imageUrl);
  const spec = specRow(product);
  const stock = stockStatus(product.stockQuantity);
  // The cart API answers 403 to staff, so they get no cart button (the header hides their cart the same way).
  const showCart = canUseCart(account?.role ?? null);

  async function add() {
    setBusy(true);
    setAdded(false);
    setError(null);
    try {
      // One unit. The header's cart count follows through the cart-changed event that addCartItem sends.
      await withCsrfRetry(token => addCartItem(product.id, 1, token), csrfToken, refreshCsrf);
      setAdded(true);
    } catch (failure) {
      setError(addToCartError(failure?.status));
    } finally {
      setBusy(false);
    }
  }

  return (
    <li>
      <Card as="article" className="product-card">
        {imageUrl && !imageFailed ? (
          <img className="product-image" src={imageUrl} alt={product.name} loading="lazy"
            onError={() => setImageFailed(true)} />
        ) : (
          <div className="image-placeholder">No image available</div>
        )}
        <h3 id={nameId}><Link to={`/product?id=${product.id}`}>{product.name}</Link></h3>
        {spec && (
          <p className={`product-spec ${spec.kind === "brand" ? "product-brand" : "product-description"}`}>{spec.text}</p>
        )}
        <p className="price">{formatPrice(product.price)}</p>
        <p className={stock.inStock ? "stock" : "stock out-of-stock"}>{stock.text}</p>
        {added && (
          <p role="status" className="product-card-message">
            Added to your cart. <Link to="/cart">View cart</Link>
          </p>
        )}
        {error && <p role="alert" className="product-card-message product-card-error">{error}</p>}
        {/* The card's actions. More can go here later (a wishlist button, for example). */}
        <div className="product-card-actions">
          {showCart && (stock.inStock ? (
            <Button onClick={add} disabled={busy} aria-describedby={nameId}>{busy ? "Adding…" : "Add to cart"}</Button>
          ) : (
            <Button disabled aria-describedby={nameId}>Out of stock</Button>
          ))}
        </div>
      </Card>
    </li>
  );
}

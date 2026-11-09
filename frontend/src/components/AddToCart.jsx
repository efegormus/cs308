import { useState } from "react";
import { Link } from "react-router";
import { useAuth } from "../auth/AuthProvider.jsx";
import { addCartItem } from "../lib/cart-api.js";
import { addToCartError, clampQuantity, maxAddQuantity } from "../lib/add-to-cart.js";
import "../styles/add-to-cart.css";

// The "Add to cart" box of the product page (SCRUM-70). An out-of-stock product gets a disabled button; the server
// checks the stock again, so a stale page can never put more in the cart than exists.
export default function AddToCart({ product }) {
  const { csrfToken, refreshCsrf } = useAuth();
  const max = maxAddQuantity(product.stockQuantity);
  const [quantity, setQuantity] = useState(1);
  const [busy, setBusy] = useState(false);
  const [added, setAdded] = useState(null); // The cart after the last successful add.
  const [error, setError] = useState(null);

  if (max === 0) {
    return (
      <div className="add-to-cart">
        <button type="button" disabled>Out of stock</button>
      </div>
    );
  }

  async function add() {
    setBusy(true);
    setError(null);
    setAdded(null);
    try {
      let cart;
      try {
        cart = await addCartItem(product.id, quantity, await csrfToken());
      } catch (failure) {
        // A token from before a login or logout is refused with 400: get a new one and try once more.
        if (failure?.status !== 400) throw failure;
        await refreshCsrf();
        cart = await addCartItem(product.id, quantity, await csrfToken());
      }
      setAdded(cart);
    } catch (failure) {
      setError(addToCartError(failure?.status));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="add-to-cart">
      <label>
        Quantity
        <input type="number" inputMode="numeric" min="1" max={max} value={quantity} disabled={busy}
          onChange={event => setQuantity(clampQuantity(event.target.value, max))} />
      </label>
      <button type="button" onClick={add} disabled={busy}>{busy ? "Adding…" : "Add to cart"}</button>
      {added && (
        <p role="status" className="add-to-cart-ok">
          Added to your cart ({added.itemCount} {added.itemCount === 1 ? "item" : "items"} in total).{" "}
          <Link to="/cart">View cart</Link>
        </p>
      )}
      {error && <p role="alert" className="add-to-cart-error">{error}</p>}
    </div>
  );
}

import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router";
import { useAuth } from "../auth/AuthProvider.jsx";
import { ErrorState, LoadingState } from "../components/RequestStates.js";
import { fetchCart, removeCartItem } from "../lib/cart-api.js";
import { loginHref } from "../lib/auth-utils.js";
import { formatPrice, safeImageUrl } from "../lib/product-display.js";
import { useRequest } from "../lib/useRequest.js";
import "../styles/request-states.css";
import "../styles/cart.css";

function CartLine({ line, removing, onRemove }) {
  const image = safeImageUrl(line.imageUrl);
  return (
    <li className="cart-line">
      <div className="cart-line-image">
        {image ? <img src={image} alt="" /> : <span aria-hidden="true">No image</span>}
      </div>
      <div className="cart-line-info">
        <Link to={`/product?id=${line.productId}`}>{line.name}</Link>
        <p>{formatPrice(line.unitPrice)} &times; {line.quantity}</p>
        {!line.available && (
          <p className="cart-line-warning" role="status">
            This item is no longer available in the quantity you chose. Remove it before checkout.
          </p>
        )}
      </div>
      <p className="cart-line-total">{formatPrice(line.lineTotal)}</p>
      <button type="button" disabled={removing} onClick={() => onRemove(line)}
        aria-label={`Remove ${line.name} from the cart`}>
        Remove
      </button>
    </li>
  );
}

// The cart page (SCRUM-69). It shows GET /api/cart and removes a whole line with DELETE /api/cart/items/{id}.
export default function CartPage() {
  const { account, authReady, csrfToken, refreshCsrf } = useAuth();
  const { status, data, error, run, cancel } = useRequest();
  const [cart, setCart] = useState(null); // The newest cart, from the first load or from a removal.
  const [removingId, setRemovingId] = useState(null);
  const [removeError, setRemoveError] = useState(null);

  const load = useCallback(() => run(({ signal }) => fetchCart({ signal })), [run]);

  useEffect(() => {
    void load();
    return cancel;
  }, [load, cancel]);

  useEffect(() => {
    if (status === "success") setCart(data);
  }, [status, data]);

  async function handleRemove(line) {
    setRemovingId(line.productId);
    setRemoveError(null);
    try {
      let result;
      try {
        result = await removeCartItem(line.productId, await csrfToken());
      } catch (failure) {
        if (failure?.status !== 400) throw failure;
        // A token from before a login or logout is refused with 400: get a new one and try once more.
        await refreshCsrf();
        result = await removeCartItem(line.productId, await csrfToken());
      }
      setCart(result);
    } catch (failure) {
      setRemoveError(failure);
    } finally {
      setRemovingId(null);
    }
  }

  const empty = cart !== null && cart.items.length === 0;
  const blocked = cart !== null && cart.items.some(line => !line.available);

  return (
    <div className="route-page catalog-page cart-page">
      <title>Cart | Online Store</title>
      <main id="main-content">
        <h1>Your cart</h1>
        <div aria-busy={status === "loading"}>
          {status === "loading" && cart === null && <LoadingState label="Loading your cart…" />}
          {status === "error" && cart === null && <ErrorState error={error} onRetry={load} />}
          {removeError && <ErrorState error={removeError} />}
          {empty && (
            <p role="status">
              Your cart is empty. <Link to="/products">Browse products</Link>
            </p>
          )}
          {cart !== null && !empty && (
            <>
              <ul className="cart-lines">
                {cart.items.map(line => (
                  <CartLine key={line.productId} line={line} removing={removingId === line.productId}
                    onRemove={handleRemove} />
                ))}
              </ul>
              <p className="cart-total">
                <span>Total ({cart.itemCount} {cart.itemCount === 1 ? "item" : "items"})</span>
                <strong>{formatPrice(cart.total)}</strong>
              </p>
              {blocked
                ? <p role="status">Remove the unavailable items to continue to checkout.</p>
                : account
                  ? <p><Link className="cart-checkout" to="/checkout">Proceed to checkout</Link></p>
                  : authReady && (
                    <p>
                      <Link className="cart-checkout" to={loginHref({ pathname: "/checkout" })}>Log in to check out</Link>
                    </p>
                  )}
            </>
          )}
        </div>
      </main>
    </div>
  );
}

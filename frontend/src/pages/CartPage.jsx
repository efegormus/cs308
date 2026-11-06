import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router";
import { useAuth } from "../auth/AuthProvider.jsx";
import { ErrorState, LoadingState } from "../components/RequestStates.js";
import { fetchCart, MAX_LINE_QUANTITY, removeCartItem, setCartItemQuantity } from "../lib/cart-api.js";
import { loginHref } from "../lib/auth-utils.js";
import { formatPrice, safeImageUrl } from "../lib/product-display.js";
import { useRequest } from "../lib/useRequest.js";
import "../styles/request-states.css";
import "../styles/cart.css";

function CartLine({ line, busy, onRemove, onQuantity }) {
  const image = safeImageUrl(line.imageUrl);
  return (
    <li className="cart-line">
      <div className="cart-line-image">
        {image ? <img src={image} alt="" /> : <span aria-hidden="true">No image</span>}
      </div>
      <div className="cart-line-info">
        <Link to={`/product?id=${line.productId}`}>{line.name}</Link>
        <p>{formatPrice(line.unitPrice)} each</p>
        <div className="cart-quantity" role="group" aria-label={`Quantity of ${line.name}`}>
          <button type="button" disabled={busy || line.quantity <= 1} aria-label={`Decrease quantity of ${line.name}`}
            onClick={() => onQuantity(line, line.quantity - 1)}>&minus;</button>
          <output aria-live="polite">{line.quantity}</output>
          <button type="button" disabled={busy || line.quantity >= Math.min(line.stockQuantity, MAX_LINE_QUANTITY)}
            aria-label={`Increase quantity of ${line.name}`} onClick={() => onQuantity(line, line.quantity + 1)}>+</button>
        </div>
        {!line.available && (
          <p className="cart-line-warning" role="status">
            This item is no longer available in the quantity you chose. Remove it before checkout.
          </p>
        )}
      </div>
      <p className="cart-line-total">{formatPrice(line.lineTotal)}</p>
      <button type="button" className="cart-remove" disabled={busy} onClick={() => onRemove(line)}
        aria-label={`Remove ${line.name} from the cart`}>
        Remove
      </button>
    </li>
  );
}

// The cart page (SCRUM-69). It shows GET /api/cart and removes a whole line with DELETE /api/cart/items/{id}.
export default function CartPage() {
  const { account, authReady, csrfToken, refreshCsrf, cartMergeVersion, cartMergeConflicts, cartMergeError, restoreGuestCart, cartMergeBusy } = useAuth();
  const { status, data, error, run, cancel } = useRequest();
  const [cart, setCart] = useState(null); // The newest cart, from the first load or from a removal.
  const [removingId, setRemovingId] = useState(null);
  const [removeError, setRemoveError] = useState(null);

  const load = useCallback(() => run(({ signal }) => fetchCart({ signal })), [run]);

  useEffect(() => {
    setCart(null);
    if (authReady) void load();
    return cancel;
  }, [load, cancel, authReady, account?.id, cartMergeVersion]);

  useEffect(() => {
    if (status === "success") setCart(data);
  }, [status, data]);

  // Runs one change of a line. A token from before a login or logout is refused with 400: get a new one, try once more.
  async function change(line, send) {
    setRemovingId(line.productId);
    setRemoveError(null);
    try {
      let result;
      try {
        result = await send(await csrfToken());
      } catch (failure) {
        if (failure?.status !== 400) throw failure;
        await refreshCsrf();
        result = await send(await csrfToken());
      }
      setCart(result);
    } catch (failure) {
      setRemoveError(failure);
      // The stock or the cart changed under us: show what the server has now.
      if (failure?.status === 404 || failure?.status === 409) void load();
    } finally {
      setRemovingId(null);
    }
  }

  const handleRemove = line => change(line, token => removeCartItem(line.productId, token));
  const handleQuantity = (line, quantity) => change(line, token => setCartItemQuantity(line.productId, quantity, token));

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
          <ErrorState error={cartMergeError} onRetry={restoreGuestCart} retrying={cartMergeBusy} />
          {cartMergeConflicts.length > 0 && <section aria-label="Guest cart conflicts">
            <h2>Review your guest selections</h2>
            <ul>{cartMergeConflicts.map(conflict => <li key={conflict.productId}>
              {conflict.name} · Quantity {conflict.quantity} · {conflict.reason === "CartFull"
                ? "Your account cart is full; this selection is still in your guest cart."
                : "This selection is unavailable in the requested quantity."}
            </li>)}</ul>
          </section>}
          {empty && (
            <p role="status">
              Your cart is empty. <Link to="/products">Browse products</Link>
            </p>
          )}
          {cart !== null && !empty && (
            <>
              <ul className="cart-lines">
                {cart.items.map(line => (
                  <CartLine key={line.productId} line={line} busy={removingId === line.productId}
                    onRemove={handleRemove} onQuantity={handleQuantity} />
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

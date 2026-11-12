import { useCallback, useEffect, useState } from "react";
import { Link, useLocation } from "react-router";
import { useAuth } from "../auth/AuthProvider.jsx";
import { useSessionExpiry } from "../auth/useSessionExpiry.js";
import PurchaseAccess from "../components/PurchaseAccess.jsx";
import { ErrorState, LoadingState } from "../components/RequestStates.js";
import { loginHref } from "../lib/auth-utils.js";
import { formatPrice, safeImageUrl } from "../lib/product-display.js";
import { fetchWishlist, removeWishlistItem, WishlistError } from "../lib/wishlist-api.js";
import { useRequest } from "../lib/useRequest.js";
import "../styles/request-states.css";
import "../styles/products.css";

function WishlistContents() {
  const { csrfToken } = useAuth();
  const location = useLocation();
  const request = useRequest();
  const [items, setItems] = useState([]);
  const [loaded, setLoaded] = useState(false);
  const [removingId, setRemovingId] = useState(null);
  const [writeError, setWriteError] = useState(null);
  useSessionExpiry(request.error, writeError);

  const load = useCallback(async () => {
    const result = await request.run(({ signal }) => fetchWishlist({ signal }), { validate: Array.isArray });
    if (result.status === "success") {
      setItems(result.data);
      setLoaded(true);
    } else if (result.status === "error" && [401, 403].includes(result.error.status)) {
      setItems([]);
      setLoaded(false);
    }
  }, [request.run]);

  useEffect(() => { void load(); return request.cancel; }, [load, request.cancel]);

  async function remove(productId) {
    setRemovingId(productId);
    setWriteError(null);
    try {
      await removeWishlistItem(productId, await csrfToken());
      setItems(previous => previous.filter(item => item.productId !== productId));
    } catch (error) {
      setWriteError({
        status: error instanceof WishlistError ? error.status : undefined,
        message: error instanceof WishlistError && error.status === 401
          ? "Your session expired. Log in again to manage your wishlist."
          : "We couldn't remove this product. Please try again."
      });
    } finally {
      setRemovingId(null);
    }
  }

  return (
    <>
      {request.status === "loading" && items.length === 0 && <LoadingState label="Loading your wishlist…" />}
      <ErrorState error={request.error} onRetry={load} />
      {writeError && <p role="alert">{writeError.message}
        {writeError.status === 401 && <> <Link to={loginHref(location)}>Log in</Link></>}
      </p>}
      {loaded && items.length === 0 && <p role="status">Your wishlist is empty. <Link to="/products">Browse products</Link></p>}
      <ul className="wishlist-grid" aria-busy={request.status === "loading"}>
        {items.map(({ productId, product }) => {
          const image = safeImageUrl(product.imageUrl);
          return (
            <li key={productId}>
              <article className="wishlist-card">
                {image ? <img src={image} alt={product.name} loading="lazy" /> : <div className="image-placeholder">No image available</div>}
                <h2><Link to={`/product?id=${product.id}`}>{product.name}</Link></h2>
                {product.brandName && <p>{product.brandName}</p>}
                <p className="price">{formatPrice(product.price)}</p>
                <button type="button" disabled={removingId !== null} onClick={() => remove(productId)}>
                  {removingId === productId ? "Removing…" : "Remove"}
                </button>
              </article>
            </li>
          );
        })}
      </ul>
    </>
  );
}

export default function WishlistPage() {
  return (
    <div className="route-page catalog-page wishlist-page">
      <title>My wishlist | Online Store</title>
      <main id="main-content">
        <h1>My wishlist</h1>
        <PurchaseAccess role="Customer">{account => <WishlistContents key={account.id} />}</PurchaseAccess>
      </main>
    </div>
  );
}

import { useCallback, useEffect, useId, useState } from "react";
import { Link, useLocation } from "react-router";
import { useAuth } from "../auth/AuthProvider.jsx";
import { useSessionExpiry } from "../auth/useSessionExpiry.js";
import PurchaseAccess from "../components/PurchaseAccess.jsx";
import { ErrorState, LoadingState } from "../components/RequestStates.js";
import { loginHref } from "../lib/auth-utils.js";
import { formatPrice, safeImageUrl, specRow, stockStatus } from "../lib/product-display.js";
import { fetchWishlist, removeWishlistItem, WishlistError } from "../lib/wishlist-api.js";
import { newestFirst, removeButtonLabel, removeErrorMessage, wishlistCountLabel } from "../lib/wishlist-display.js";
import Button from "../components/ui/Button.jsx";
import Card from "../components/ui/Card.jsx";
import { useRequest } from "../lib/useRequest.js";
import "../styles/request-states.css";
import "../styles/products.css";

// One saved product. It has the look of the product card on /products (same Card, same grid, same stock line); its
// one action is to take the product off the list.
function WishlistCard({ product, removing, disabled, onRemove }) {
  const nameId = useId();
  const [imageFailed, setImageFailed] = useState(false);
  const image = safeImageUrl(product.imageUrl);
  const spec = specRow(product);
  const stock = stockStatus(product.stockQuantity);
  return (
    <li>
      <Card as="article" className="product-card">
        {image && !imageFailed ? (
          <img className="product-image" src={image} alt={product.name} loading="lazy" onError={() => setImageFailed(true)} />
        ) : (
          <div className="image-placeholder">No image available</div>
        )}
        <h2 id={nameId}><Link to={`/product?id=${product.id}`}>{product.name}</Link></h2>
        {spec && <p className={`product-spec ${spec.kind === "brand" ? "product-brand" : "product-description"}`}>{spec.text}</p>}
        <p className="price">{formatPrice(product.price)}</p>
        <p className={stock.inStock ? "stock" : "stock out-of-stock"}>{stock.text}</p>
        <div className="product-card-actions">
          <Button disabled={disabled} onClick={onRemove} aria-describedby={nameId}>{removeButtonLabel(removing)}</Button>
        </div>
      </Card>
    </li>
  );
}

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
        message: removeErrorMessage(error instanceof WishlistError ? error.status : undefined)
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
      {loaded && items.length === 0 && <p role="status">{wishlistCountLabel(0)} <Link to="/products">Browse products</Link></p>}
      {loaded && items.length > 0 && <p role="status" className="intro">{wishlistCountLabel(items.length)}</p>}
      <ul className="product-grid" aria-busy={request.status === "loading"}>
        {newestFirst(items).map(({ productId, product }) => (
          <WishlistCard key={productId} product={product} removing={removingId === productId}
            disabled={removingId !== null} onRemove={() => remove(productId)} />
        ))}
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

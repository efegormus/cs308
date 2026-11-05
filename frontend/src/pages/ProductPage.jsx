import { useCallback, useEffect } from "react";
import { Link, useSearchParams } from "react-router";
import ProductDetail from "../components/ProductDetail.jsx";
import { ErrorState, LoadingState } from "../components/RequestStates.js";
import { fetchProduct } from "../lib/catalog-api.js";
import { isLoadedProduct, parseProductId } from "../lib/product-detail.js";
import { useRequest } from "../lib/useRequest.js";
import "../styles/request-states.css";

// The product detail page (SCRUM-110), opened as /product?id=3. It loads GET /api/products/{id}.
export default function ProductPage() {
  const [searchParams] = useSearchParams();
  const id = parseProductId(searchParams.get("id"));
  const { status, data, error, run, cancel } = useRequest();

  // fetchProduct returns null for an unknown product. The hook only accepts a non-null result, so it is wrapped.
  const load = useCallback(() => run(
    ({ signal }) => fetchProduct(id, { signal }).then(product => ({ product })),
    { validate: isLoadedProduct }
  ), [id, run]);

  useEffect(() => {
    if (id === null) return undefined;
    void load();
    return cancel;
  }, [id, load, cancel]);

  const product = status === "success" ? data.product : null;
  const notFound = id === null || (status === "success" && product === null);

  return (
    <div className="route-page catalog-page">
      <title>{product ? `${product.name} | Online Store` : "Product | Online Store"}</title>
      <main id="main-content">
        <p><Link to="/products">&larr; Back to all products</Link></p>
        <div aria-busy={status === "loading"}>
          {status === "loading" && <LoadingState label="Loading product…" />}
          {status === "error" && <ErrorState error={error} onRetry={load} />}
          {notFound && <p role="status">This product could not be found.</p>}
          {product && <ProductDetail key={product.id} product={product} />}
        </div>
      </main>
    </div>
  );
}

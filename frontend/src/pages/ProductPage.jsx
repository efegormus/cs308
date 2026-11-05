import { Link, useSearchParams } from "react-router";

// Skeleton. SCRUM-110 builds the product detail page.
export default function ProductPage() {
  const [searchParams] = useSearchParams();
  // The id comes from the address bar (/product?id=3), so it is validated before it is used, as in js/product.js.
  const rawId = searchParams.get("id");
  const id = /^\d{1,9}$/.test(rawId ?? "") ? Number(rawId) : null;

  return (
    <div className="route-page catalog-page">
      <title>Product | Online Store</title>
      <main id="main-content">
        <p><Link to="/products">&larr; Back to all products</Link></p>
        <p role="status">{id === null ? "This product could not be found." : `Product #${id}`}</p>
      </main>
    </div>
  );
}
